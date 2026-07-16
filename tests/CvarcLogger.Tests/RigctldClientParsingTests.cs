using System.Net.Sockets;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.Tests;

public class RigctldClientParsingTests
{
    [Fact]
    public async Task Poll_HappyPath_ParsesFrequencyModeAndBand()
    {
        var transport = new FakeRigctldTransport(new[] { "14250000.000000", "USB", "2400" });
        var client = new RigctldClient(transport);
        await client.ConnectAsync("127.0.0.1", 4532);

        var result = await client.PollAsync();

        Assert.True(result.Success);
        Assert.Equal(14.25m, result.FrequencyMhz);
        Assert.Equal("USB", result.RawMode);
        Assert.Equal("SSB", result.MappedMode);
        Assert.Equal("20m", result.Band);
    }

    [Fact]
    public async Task Poll_RprtErrorOnFrequency_FailsGracefully()
    {
        var transport = new FakeRigctldTransport(new[] { "RPRT -1" });
        var client = new RigctldClient(transport);
        await client.ConnectAsync("127.0.0.1", 4532);

        var result = await client.PollAsync();

        Assert.False(result.Success);
        Assert.Contains("RPRT", result.Error);
    }

    [Fact]
    public async Task Poll_RprtErrorOnMode_FailsGracefully()
    {
        var transport = new FakeRigctldTransport(new[] { "14250000.000000", "RPRT -6" });
        var client = new RigctldClient(transport);
        await client.ConnectAsync("127.0.0.1", 4532);

        var result = await client.PollAsync();

        Assert.False(result.Success);
        Assert.Contains("RPRT", result.Error);
    }

    [Fact]
    public async Task Poll_MalformedFrequency_FailsWithoutThrowing()
    {
        var transport = new FakeRigctldTransport(new[] { "not-a-number" });
        var client = new RigctldClient(transport);
        await client.ConnectAsync("127.0.0.1", 4532);

        var result = await client.PollAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Poll_WithoutConnecting_ReturnsNotConnectedError()
    {
        var client = new RigctldClient(new FakeRigctldTransport(Array.Empty<string>()));

        var result = await client.PollAsync();

        Assert.False(result.Success);
        Assert.Equal("Not connected.", result.Error);
    }

    [Fact]
    public async Task Connect_TransportThrows_ReturnsFailureInsteadOfThrowing()
    {
        var transport = new FakeRigctldTransport(Array.Empty<string>(), connectException: new SocketException());
        var client = new RigctldClient(transport);

        var result = await client.ConnectAsync("127.0.0.1", 4532);

        Assert.False(result.Success);
        Assert.Equal(RigConnectionState.Error, client.State);
    }

    private class FakeRigctldTransport : IRigctldTransport
    {
        private readonly Queue<string> _responses;
        private readonly Exception? _connectException;

        public bool IsConnected { get; private set; }

        public FakeRigctldTransport(IEnumerable<string> responses, Exception? connectException = null)
        {
            _responses = new Queue<string>(responses);
            _connectException = connectException;
        }

        public Task ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            if (_connectException is not null) throw _connectException;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteLineAsync(string line, CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> ReadLineAsync(CancellationToken ct = default)
        {
            if (_responses.Count == 0) throw new IOException("No more fixture responses.");
            return Task.FromResult(_responses.Dequeue());
        }

        public void Disconnect() => IsConnected = false;

        public ValueTask DisposeAsync()
        {
            Disconnect();
            return ValueTask.CompletedTask;
        }
    }
}
