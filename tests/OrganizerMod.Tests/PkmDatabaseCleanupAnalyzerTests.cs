using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class PkmDatabaseCleanupAnalyzerTests
{
    private readonly PkmDatabaseCleanupAnalyzer analyzer = new();

    [Fact]
    public void MutableDifferencesStillGroupSameInstance()
    {
        var first = Candidate("a", level: 20, exp: 100, nickname: "Old", moves: [1, 2]);
        var evolved = Candidate("b", level: 80, exp: 5000, nickname: "New", moves: [3, 4, 5, 6]);
        var group = Assert.Single(analyzer.Analyze([first, evolved]).Groups);
        Assert.Equal("b", group.SuggestedKeeperId);
    }

    [Theory]
    [InlineData("pid")]
    [InlineData("species")]
    [InlineData("origin")]
    [InlineData("date")]
    [InlineData("place")]
    [InlineData("metlevel")]
    [InlineData("trainer")]
    [InlineData("ivs")]
    public void InvariantDifferencePreventsGrouping(string difference)
    {
        var identity = Identity();
        var changed = difference switch
        {
            "pid" => identity with { PersonalityId = 2 },
            "species" => identity with { Species = 2 },
            "origin" => identity with { OriginGame = 2 },
            "date" => identity with { MetDate = new DateOnly(2021, 1, 1) },
            "place" => identity with { MetLocation = 2 },
            "metlevel" => identity with { MetLevel = 6 },
            "trainer" => identity with { TrainerId = 2 },
            "ivs" => identity with { IvHp = 30 },
            _ => identity,
        };
        Assert.Empty(analyzer.Analyze([Candidate("a"), Candidate("b", identity: changed)]).Groups);
    }

    [Fact]
    public void SuggestedKeeperUsesLevelExperienceThenPath()
    {
        var analysis = analyzer.Analyze([
            Candidate("z", path: "z.pk9", level: 50, exp: 100),
            Candidate("a", path: "a.pk9", level: 50, exp: 100),
            Candidate("lower", level: 49, exp: 999999),
        ]);
        Assert.Equal("a", Assert.Single(analysis.Groups).SuggestedKeeperId);
        Assert.Equal(2, analysis.DuplicateFiles);
    }

    [Fact]
    public void RepeatedRunsAreDeterministic()
    {
        var candidates = new[] { Candidate("b"), Candidate("a"), Candidate("c", identity: Identity() with { PersonalityId = 2 }) };
        var first = analyzer.Analyze(candidates);
        var second = analyzer.Analyze(candidates.Reverse().ToArray());
        Assert.Equal(first.Groups.Select(x => (x.GroupId, x.SuggestedKeeperId)),
            second.Groups.Select(x => (x.GroupId, x.SuggestedKeeperId)));
    }

    private static PkmDatabaseCleanupCandidate Candidate(string id, string? path = null, int level = 50, ulong exp = 100,
        string nickname = "Test", IReadOnlyList<int>? moves = null, PkmInstanceIdentity? identity = null) =>
        new(id, path ?? $"{id}.pk9", id, identity ?? Identity(), "Bulbasaur", 0, level, exp, nickname, moves ?? [1, 2, 3, 4]);

    private static PkmInstanceIdentity Identity() =>
        new(1, 1, 1, new DateOnly(2020, 1, 1), 1, 5, null, 0, 123, 456,
            "Trainer", 0, 2, 4, 0, 1, 31, 31, 31, 31, 31, 31, false);
}
