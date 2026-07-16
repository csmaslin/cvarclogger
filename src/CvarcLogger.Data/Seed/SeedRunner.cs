using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Seed;

/// <summary>Seeds the bundled (approximate, community-assembled) DXCC prefix table into the database
/// on first run. Only runs when the DxccEntities table is empty, so a later, more authoritative
/// country-file import can replace this data without code changes.</summary>
public static class SeedRunner
{
    public static async Task SeedIfEmptyAsync(CvarcLoggerDbContext db, CancellationToken ct = default)
    {
        if (await db.DxccEntities.AnyAsync(ct).ConfigureAwait(false))
            return;

        foreach (var dto in LoadSeedEntities())
        {
            var entity = new DxccEntity
            {
                EntityCode = dto.EntityCode,
                EntityName = dto.EntityName,
                Continent = dto.Continent,
            };
            foreach (var prefix in dto.Prefixes)
            {
                entity.Prefixes.Add(new PrefixMapping { Prefix = prefix, DxccEntityCode = dto.EntityCode });
            }
            db.DxccEntities.Add(entity);
        }

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
        [property: JsonPropertyName("prefixes")] List<string> Prefixes);
}
