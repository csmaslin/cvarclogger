using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Seed;

/// <summary>Repairs schema drift left by a 2026-07-30 mistake: migration 20260730160000_AddContestFields
/// was gutted to a no-op on the belief its columns were already present elsewhere in the schema -- true
/// for only 3 of its 8 original columns (Qsos.ContestId/StxSerial/SrxSerial, which really are in
/// InitialCreate), false for the other 5 (Qsos.Check/Precedence/Class/SkccNr/MySkccNr and
/// StationProfiles.SkccNr, which only that migration ever created).
///
/// Any database whose __EFMigrationsHistory recorded AddContestFields as applied before the gutting
/// keeps its columns -- they're already physically present, and EF never re-runs a migration once
/// recorded. But EF's migration history is exactly what makes this unsafe to "fix" with a normal new
/// migration: a fresh database (or any older one that hadn't applied AddContestFields yet) is missing
/// the columns and needs them added, while every database that already has them would throw "duplicate
/// column" if a new migration tried to add them again. Checking the live schema via PRAGMA table_info
/// at every startup, instead of trusting migration history, is safe for both cases and needs no new
/// migration at all.</summary>
public static class SchemaRepairRunner
{
    private static readonly (string Table, string Column)[] ColumnsToRestore =
    {
        ("Qsos", "Check"),
        ("Qsos", "Class"),
        ("Qsos", "MySkccNr"),
        ("Qsos", "Precedence"),
        ("Qsos", "SkccNr"),
        ("StationProfiles", "SkccNr"),
    };

    public static async Task RepairAsync(CvarcLoggerDbContext db, CancellationToken ct = default)
    {
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        bool wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var (table, column) in ColumnsToRestore)
                await AddColumnIfMissingAsync(conn, table, column, ct).ConfigureAwait(false);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection conn, string table, string column, CancellationToken ct)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $col";
            check.Parameters.AddWithValue("$col", column);
            long exists = (long)(await check.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
            if (exists > 0) return;
        }

        using var alter = conn.CreateCommand();
        // All original columns were nullable TEXT (Check/Class/Precedence/SkccNr/MySkccNr) -- SQLite
        // ignores the maxLength EF used to declare (TEXT has no real length limit in SQLite), so a
        // plain "TEXT NULL" restores the same effective column.
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" TEXT NULL";
        await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
