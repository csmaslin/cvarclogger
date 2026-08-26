using System.Globalization;
using System.IO;
using System.Net.Http;
using Microsoft.Data.Sqlite;

namespace CvarcLogger.App.Services;

/// <summary>One row looked up from a reference database by exact code -- a summit or park's name and a
/// short detail string (altitude/points for SOTA, region/grid for POTA).</summary>
public record RefInfo(string Reference, string Name, string Detail)
{
    /// <summary>Single-line display, e.g. "Mount Umunhum, 817 m, 10 pts".</summary>
    public string Display => Detail.Length > 0 ? $"{Name}, {Detail}" : Name;
}

/// <summary>Base for the on-device SOTA/POTA reference databases -- adapted from CvarcCellLog's identical
/// scheme (see that project's Services/ReferenceDatabase.cs, which also supports a GPS nearest-search this
/// WPF app doesn't need: CvarcLogger is chaser-focused, so the only lookup that matters here is "the
/// operator typed a SOTA/POTA reference code, show its name/location"). Import is user-triggered and
/// atomic: the CSV is downloaded and parsed into a temp .db file which replaces the live one only after
/// every record is in and counted, so an interrupted download or parse leaves the existing database
/// untouched.</summary>
public abstract class ReferenceDatabase
{
    private readonly HttpClient _httpClient;

    protected ReferenceDatabase(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>File name inside App.DataDirectory, e.g. "sota-ref.db".</summary>
    protected abstract string FileName { get; }

    /// <summary>Where the reference CSV is downloaded from.</summary>
    protected abstract string SourceUrl { get; }

    /// <summary>Parses the downloaded CSV into (reference, name, detail) rows. Rows the parser skips
    /// (malformed, inactive) are simply not yielded.</summary>
    protected abstract IEnumerable<(string Reference, string Name, string Detail)> Parse(string csvPath);

    public string DbPath => Path.Combine(App.DataDirectory, FileName);

    public bool IsAvailable => File.Exists(DbPath);

    /// <summary>Download + rebuild. Returns the record count, or 0 on failure (with the reason
    /// reported through progress). The live db file is replaced only on complete success.</summary>
    public async Task<int> UpdateAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string tmpCsv = DbPath + ".csv.tmp";
        string tmpDb = DbPath + ".tmp";
        try
        {
            Directory.CreateDirectory(App.DataDirectory);
            progress?.Report("Downloading...");
            // pota.app sits behind a CDN that returns 403 to requests with no User-Agent -- identify
            // ourselves like any browser or well-behaved client would.
            using var request = new HttpRequestMessage(HttpMethod.Get, SourceUrl);
            request.Headers.UserAgent.ParseAdd($"CvarcLogger/{AppVersion.Current} (Windows; +https://www.cvarc.org)");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using (var remote = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            using (var file = File.Create(tmpCsv))
                await remote.CopyToAsync(file, ct).ConfigureAwait(false);

            long mb = new FileInfo(tmpCsv).Length / (1024 * 1024);
            progress?.Report($"Downloaded {mb} MB. Importing...");

            File.Delete(tmpDb);
            int count = await Task.Run(() => BuildDatabase(tmpCsv, tmpDb, progress, ct), ct).ConfigureAwait(false);
            if (count == 0)
            {
                progress?.Report("Import failed: no records parsed. Existing database unchanged.");
                return 0;
            }

            // Atomic swap: everything parsed and counted, so replace the live file in one move.
            SqliteConnection.ClearAllPools();
            File.Delete(DbPath);
            File.Move(tmpDb, DbPath);
            progress?.Report($"Done: {count:N0} records.");
            return count;
        }
        catch (Exception ex)
        {
            progress?.Report($"Update failed: {ex.Message}. Existing database unchanged.");
            return 0;
        }
        finally
        {
            try { File.Delete(tmpCsv); } catch { }
            try { File.Delete(tmpDb); } catch { }
        }
    }

    private int BuildDatabase(string csvPath, string dbPath, IProgress<string>? progress, CancellationToken ct)
    {
        using var db = new SqliteConnection($"Data Source={dbPath}");
        db.Open();
        Exec(db, "PRAGMA journal_mode=DELETE");   // single-file db: no -wal/-shm sidecars to complicate the swap
        Exec(db, "PRAGMA synchronous=OFF");       // safe: a crash mid-build just discards the temp file
        Exec(db, "CREATE TABLE records (ref TEXT NOT NULL, name TEXT NOT NULL, detail TEXT NOT NULL)");
        Exec(db, "CREATE TABLE meta (source TEXT, downloaded_utc TEXT, record_count INTEGER)");

        int count = 0;
        using (var tx = db.BeginTransaction())
        {
            using var insert = db.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT INTO records (ref, name, detail) VALUES ($r, $n, $d)";
            var pR = insert.CreateParameter(); pR.ParameterName = "$r"; insert.Parameters.Add(pR);
            var pN = insert.CreateParameter(); pN.ParameterName = "$n"; insert.Parameters.Add(pN);
            var pD = insert.CreateParameter(); pD.ParameterName = "$d"; insert.Parameters.Add(pD);

            foreach (var (reference, name, detail) in Parse(csvPath))
            {
                ct.ThrowIfCancellationRequested();
                pR.Value = reference; pN.Value = name; pD.Value = detail;
                insert.ExecuteNonQuery();
                count++;
                if (count % 20000 == 0) progress?.Report($"Imported {count:N0} records...");
            }

            using var meta = db.CreateCommand();
            meta.Transaction = tx;
            meta.CommandText = "INSERT INTO meta (source, downloaded_utc, record_count) VALUES ($s, $d, $c)";
            meta.Parameters.AddWithValue("$s", SourceUrl);
            meta.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            meta.Parameters.AddWithValue("$c", count);
            meta.ExecuteNonQuery();

            tx.Commit();
        }

        Exec(db, "CREATE INDEX idx_records_ref ON records (ref)");
        return count;
    }

    /// <summary>Exact-code lookup (case-insensitive) -- the operator typed/pasted a SOTA/POTA reference
    /// on the entry form and this resolves its name/detail for display, see QsoEntryViewModel.</summary>
    public async Task<RefInfo?> LookupAsync(string reference, CancellationToken ct = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(reference)) return null;

        return await Task.Run(() =>
        {
            using var db = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT ref, name, detail FROM records WHERE ref = $r COLLATE NOCASE LIMIT 1";
            cmd.Parameters.AddWithValue("$r", reference.Trim());
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? new RefInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2)) : null;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Reverse lookup by the "name" column instead of "ref" -- for SkccRefDatabase, where ref is
    /// the SKCC number and name is the callsign (see SkccRefDatabase.MapRow), so this resolves a callsign
    /// to its SKCC record. Not indexed (no idx on name), but the roster is small enough for a table scan
    /// to be fast in practice.</summary>
    public async Task<RefInfo?> LookupByNameAsync(string name, CancellationToken ct = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(name)) return null;

        return await Task.Run(() =>
        {
            using var db = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT ref, name, detail FROM records WHERE name = $n COLLATE NOCASE LIMIT 1";
            cmd.Parameters.AddWithValue("$n", name.Trim());
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? new RefInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2)) : null;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Batched version of LookupAsync: opens the connection once and resolves every reference
    /// in a single pass, instead of one new SqliteConnection per call. Prefer this over calling
    /// LookupAsync in a loop for anything beyond a handful of lookups -- a caller resolving hundreds of
    /// references (e.g. cross-referencing every unique member in a QSO log) would otherwise open and
    /// tear down hundreds of connections sequentially.</summary>
    public async Task<Dictionary<string, RefInfo>> LookupManyAsync(IEnumerable<string> references, CancellationToken ct = default)
    {
        var result = new Dictionary<string, RefInfo>(StringComparer.OrdinalIgnoreCase);
        if (!IsAvailable) return result;

        var distinctRefs = references
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctRefs.Count == 0) return result;

        await Task.Run(() =>
        {
            using var db = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT ref, name, detail FROM records WHERE ref = $r COLLATE NOCASE LIMIT 1";
            var param = cmd.CreateParameter();
            param.ParameterName = "$r";
            cmd.Parameters.Add(param);

            foreach (var reference in distinctRefs)
            {
                param.Value = reference;
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    result[reference] = new RefInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            }
        }, ct).ConfigureAwait(false);

        return result;
    }

    /// <summary>"181,406 records, updated 2026-07-31" for status display, or null if no db yet.</summary>
    public async Task<string?> GetInfoAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        return await Task.Run(() =>
        {
            using var db = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT record_count, downloaded_utc FROM meta LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            long recs = reader.GetInt64(0);
            string date = reader.GetString(1).Split(' ')[0];
            return $"{recs:N0} records, updated {date}";
        }, ct).ConfigureAwait(false);
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Splits one CSV line respecting double-quoted fields (embedded commas, doubled quotes).</summary>
    protected static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    protected static bool TryParseDouble(string s, out double value) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
