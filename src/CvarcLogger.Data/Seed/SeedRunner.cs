using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Seed;

/// <summary>Seeds the bundled DXCC entity/prefix table (built from AD1C's cty country file, all current
/// DXCC entities with ADIF entity codes, CQ/ITU zones, and deleted-entity status) into the database. An
/// empty table gets the full list; an existing table gets topped up with any entity codes it doesn't have
/// yet -- so a database seeded back when the bundled list was smaller (or missing zone data) gains the
/// missing entities/fields on its next launch instead of being stuck forever with first-run data.
/// Entities already present are never modified (the operator may have hand-corrected them), and prefixes
/// already claimed in the database are never reassigned. Note: this means CqZone/ItuZone are only
/// populated on entities newly added by the top-up -- an entity that already existed before this seed
/// file gained zone data (e.g. from the original 71/338-entity bundled lists) keeps null zones until
/// something else corrects it, since the top-up cannot distinguish "never had zone data" from "operator
/// deliberately cleared it."</summary>
public static class SeedRunner
{
    public static async Task SeedIfEmptyAsync(CvarcLoggerDbContext db, CancellationToken ct = default)
    {
        var existingCodes = await db.DxccEntities.Select(e => e.EntityCode).ToListAsync(ct).ConfigureAwait(false);
        var existingCodeSet = existingCodes.ToHashSet();

        var existingPrefixes = await db.Set<PrefixMapping>().Select(p => p.Prefix).ToListAsync(ct).ConfigureAwait(false);
        var existingPrefixSet = new HashSet<string>(existingPrefixes, StringComparer.OrdinalIgnoreCase);

        bool anyAdded = false;
        foreach (var dto in LoadSeedEntities())
        {
            if (existingCodeSet.Contains(dto.EntityCode)) continue;

            var entity = new DxccEntity
            {
                EntityCode = dto.EntityCode,
                EntityName = dto.EntityName,
                Continent = dto.Continent,
                CqZone = dto.CqZone,
                ItuZone = dto.ItuZone,
                IsDeleted = dto.IsDeleted,
            };
            foreach (var prefix in dto.Prefixes)
            {
                if (!existingPrefixSet.Add(prefix)) continue; // already claimed by an existing entity
                entity.Prefixes.Add(new PrefixMapping { Prefix = prefix, DxccEntityCode = dto.EntityCode });
            }
            db.DxccEntities.Add(entity);
            anyAdded = true;
        }

        if (anyAdded)
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static List<SeedEntityDto> LoadSeedEntities()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("dxcc_prefixes.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        return JsonSerializer.Deserialize<List<SeedEntityDto>>(stream) ?? new List<SeedEntityDto>();
    }

    private record SeedEntityDto(
        [property: JsonPropertyName("entityCode")] int EntityCode,
        [property: JsonPropertyName("entityName")] string EntityName,
        [property: JsonPropertyName("continent")] string? Continent,
        [property: JsonPropertyName("cqZone")] int? CqZone,
        [property: JsonPropertyName("ituZone")] int? ItuZone,
        [property: JsonPropertyName("isDeleted")] bool IsDeleted,
        [property: JsonPropertyName("prefixes")] List<string> Prefixes);
}
