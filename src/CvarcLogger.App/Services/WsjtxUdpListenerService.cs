using System.Net;
using System.Net.Sockets;
using System.Windows;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Adif;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Geo;
using CvarcLogger.Core.Models;
using CvarcLogger.Core.Wsjtx;
using Serilog;

namespace CvarcLogger.App.Services;

/// <summary>Listens for WSJT-X's logged-QSO ADIF data and adds each one to the log automatically. In
/// this setup the data doesn't come directly from WSJT-X -- WSJT-X broadcasts to GridTracker2 (its own
/// default port, 2237), GridTracker2 logs it, and then GridTracker2 itself relays/forwards the same
/// message on to this listener's port (2238, configured under GridTracker2's own General tab: "Forward
/// UDP messages" enabled, port 2238). Deliberately does NOT forward these back to GridTracker2 --
/// GridTrackerBroadcastService.BroadcastQso is never called from here, since GridTracker2 already has
/// this QSO from WSJT-X directly and forwarding it back would create a duplicate.
///
/// Port history (2026-07-19): confirmed working end-to-end with real WSJT-X traffic on 2026-07-19 via
/// this GridTracker2-relay path. Earlier attempts to have this listener bind WSJT-X's own broadcast
/// port (2237) directly failed silently -- GridTracker2 was also bound to 2237, and on Windows, when
/// two processes both bind a UDP port with SO_REUSEADDR, incoming datagrams are delivered to only ONE
/// of them, not both (not detectable from inside this process, since the bind() call itself still
/// succeeds either way). If a user reports "nothing is being logged from WSJT-X" again, first check
/// GridTracker2's General tab still has "Forward UDP messages" enabled on port 2238, then check
/// `netstat -ano -p UDP | findstr :2238` for an unexpected competing process.</summary>
public class WsjtxUdpListenerService : IDisposable
{
    private readonly SettingsService _settings;
    private readonly IQsoRepository _qsoRepository;
    private readonly ICallsignEntityResolver _entityResolver;
    private readonly LookupCoordinator _lookupCoordinator;
    private readonly IGridZoneResolver _gridZoneResolver;

    private UdpClient? _client;
    private CancellationTokenSource? _cts;

    /// <summary>Raised after a QSO from WSJT-X has been added to the log, on the UI thread, so the
    /// log grid can refresh -- mirrors QsoEntryViewModel.QsoLogged.</summary>
    public event EventHandler? QsoLogged;

    public WsjtxUdpListenerService(
        SettingsService settings,
        IQsoRepository qsoRepository,
        ICallsignEntityResolver entityResolver,
        LookupCoordinator lookupCoordinator,
        IGridZoneResolver gridZoneResolver)
    {
        _settings = settings;
        _qsoRepository = qsoRepository;
        _entityResolver = entityResolver;
        _lookupCoordinator = lookupCoordinator;
        _gridZoneResolver = gridZoneResolver;
    }

    /// <summary>Starts or stops the listener to match the current setting. Called once at startup
    /// (to resume a previously-enabled listener) and again immediately whenever the checkbox in
    /// Settings changes, since this feature has no separate "Connect" step like CAT does.</summary>
    public void ApplyEnabledState()
    {
        Log.Information("WSJT-X UDP: ApplyEnabledState called, WsjtxEnabled={Enabled}.", _settings.WsjtxEnabled);
        if (_settings.WsjtxEnabled) Start();
        else Stop();
    }

    public void Start()
    {
        if (_client is not null) return; // already running

        try
        {
            _client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.Client.Bind(new IPEndPoint(IPAddress.Any, _settings.WsjtxPort));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to bind the WSJT-X UDP listener on port {Port}.", _settings.WsjtxPort);
            _client?.Dispose();
            _client = null;
            return;
        }

        _cts = new CancellationTokenSource();
        _ = ListenLoopAsync(_client, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _client?.Close();
        _client?.Dispose();
        _client = null;
    }

    private async Task ListenLoopAsync(UdpClient client, CancellationToken ct)
    {
        Log.Information("WSJT-X UDP: listen loop starting, bound to {Endpoint}.", client.Client.LocalEndPoint);
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await client.ReceiveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "WSJT-X UDP receive loop stopped unexpectedly.");
                break; // socket closed (Stop() was called) or otherwise unrecoverable -- exit the loop
            }

            Log.Debug("WSJT-X UDP: received {Bytes} byte datagram.", result.Buffer.Length);

            try
            {
                await HandleDatagramAsync(result.Buffer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to process a WSJT-X UDP datagram.");
            }
        }
    }

    private async Task HandleDatagramAsync(byte[] data)
    {
        string? adifText = WsjtxMessageParser.TryExtractLoggedAdif(data);
        if (adifText is null)
        {
            Log.Debug("WSJT-X UDP: datagram was not a recognized Logged-ADIF message.");
            return; // not a "Logged ADIF" message -- other WSJT-X chatter on this port (Heartbeat,
                     // Status, Decode messages arrive on this same port far more often than QSOs do)
        }

        Log.Information("WSJT-X UDP: received a Logged ADIF message: {Adif}", adifText);

        var records = AdifReader.ReadAll(adifText);
        if (records.Count == 0)
        {
            Log.Warning("WSJT-X UDP: Logged ADIF message parsed to zero ADIF records: {Adif}", adifText);
            return;
        }

        var qso = AdifFieldMapper.ToQso(records[0]);

        // Never trust an externally-sourced DXCC number -- same rationale as ADIF import and pasted
        // QSOs (see QsoLogViewModel.AddPastedQsosAsync): re-resolve from the callsign so it always
        // references a row that actually exists in our own DxccEntities table.
        var resolvedEntity = await _entityResolver.ResolveAsync(qso.Callsign).ConfigureAwait(false);
        qso.DxccEntityCode = resolvedEntity?.EntityCode;

        // GridTracker2's relayed ADIF rarely carries more than callsign/band/mode/time -- run the same
        // online callsign lookup the manual entry form runs (see QsoEntryViewModel.PerformLookupAsync),
        // but only to fill fields WSJT-X left blank. Unlike the manual form, never overwrite a field
        // WSJT-X/GridTracker2 already supplied (e.g. GridSquare from the actual over-the-air exchange)
        // with a stale value from a lookup database. Best-effort: a lookup miss must never block logging.
        await FillFromCallsignLookupAsync(qso).ConfigureAwait(false);

        await _qsoRepository.AddAsync(qso).ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(() => QsoLogged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Backfills Name/GridSquare/Country/State/County/City/ArrlSection/CqZone/ItuZone from the
    /// configured lookup services (see LookupCoordinator), same fields QsoEntryViewModel.PerformLookupAsync
    /// fills for manual entry. Only ever fills a field that's currently null -- see the caller for why.</summary>
    private async Task FillFromCallsignLookupAsync(Qso qso)
    {
        try
        {
            var result = await _lookupCoordinator.LookupAsync(qso.Callsign).ConfigureAwait(false);
            if (result.Found)
            {
                qso.Name ??= result.Name;
                qso.GridSquare ??= result.GridSquare;
                qso.Country ??= result.Country;
                qso.State ??= result.State;
                qso.County ??= result.County;
                qso.City ??= result.City;
            }

            qso.ArrlSection ??= ArrlSectionResolver.Resolve(qso.State, qso.County);

            var (gridCqZone, gridItuZone) = _gridZoneResolver.Resolve(qso.GridSquare);
            qso.CqZone ??= gridCqZone;
            qso.ItuZone ??= gridItuZone;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WSJT-X UDP: callsign lookup failed for {Callsign}; logging without it.", qso.Callsign);
        }
    }

    public void Dispose() => Stop();
}
