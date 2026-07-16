using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Awards;
using CvarcLogger.Core.Models;

namespace CvarcLogger.Tests;

public class CallsignEntityResolverTests
{
    private static ICallsignEntityResolver CreateResolver() => new CallsignEntityResolver(new FakeDxccEntityRepository());

    [Theory]
    [InlineData("W1AW", 291)]
    [InlineData("N0CALL", 291)]
    [InlineData("VE3ABC", 1)]
    [InlineData("JA1ABC", 339)]
    [InlineData("KL7AB", 6)]
    [InlineData("KH6XX", 110)]
    public async Task Resolve_MatchesExpectedEntity(string callsign, int expectedEntityCode)
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync(callsign);
        Assert.NotNull(result);
        Assert.Equal(expectedEntityCode, result!.EntityCode);
    }

    [Fact]
    public async Task Resolve_PortableCallsign_PrefersMoreSpecificSegment()
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync("W1AW/KH6");
        Assert.NotNull(result);
        Assert.Equal(110, result!.EntityCode); // Hawaii (3-char "KH6" match) beats home call's 1-char "W" match
    }

    [Fact]
    public async Task Resolve_PortableSuffix_IgnoresOperatingModeIndicator()
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync("W1AW/P");
        Assert.NotNull(result);
        Assert.Equal(291, result!.EntityCode);
    }

    [Fact]
    public async Task Resolve_UnknownPrefix_ReturnsNull()
    {
        var resolver = CreateResolver();
        var result = await resolver.ResolveAsync("ZZ9ZZZ");
        Assert.Null(result);
    }

    private class FakeDxccEntityRepository : IDxccEntityRepository
    {
        private readonly List<DxccEntity> _entities = new()
        {
            Entity(291, "United States of America", "K", "N", "W", "AA"),
            Entity(6, "Alaska", "AL", "KL", "NL", "WL"),
            Entity(110, "Hawaii", "KH6", "AH6", "NH6", "WH6"),
            Entity(1, "Canada", "VE", "VA", "VO", "VY"),
            Entity(339, "Japan", "JA", "JE", "JF", "JG"),
        };

        private static DxccEntity Entity(int code, string name, params string[] prefixes)
        {
            var entity = new DxccEntity { EntityCode = code, EntityName = name };
            foreach (var p in prefixes)
                entity.Prefixes.Add(new PrefixMapping { Prefix = p, DxccEntityCode = code });
            return entity;
        }

        public Task<List<DxccEntity>> GetAllWithPrefixesAsync(CancellationToken ct = default) =>
            Task.FromResult(_entities);

        public Task<DxccEntity?> GetByCodeAsync(int entityCode, CancellationToken ct = default) =>
            Task.FromResult(_entities.FirstOrDefault(e => e.EntityCode == entityCode));
    }
}
