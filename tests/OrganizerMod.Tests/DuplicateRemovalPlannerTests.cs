using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class DuplicateRemovalPlannerTests
{
    [Fact]
    public void HighestLevelIsKept()
    {
        var lower = Candidate(123, level: 20, experience: 10_000, party: true, slot: 0);
        var higher = Candidate(123, level: 21, experience: 9_000, party: false, slot: 1);

        var removal = Assert.Single(CreatePlan(lower, higher).Removals);

        Assert.Equal(higher, removal.Kept);
        Assert.Equal(lower, removal.Removed);
    }

    [Fact]
    public void HighestExperienceBreaksLevelTie()
    {
        var lowerExperience = Candidate(123, level: 20, experience: 10_000, party: true, slot: 0);
        var higherExperience = Candidate(123, level: 20, experience: 10_001, party: false, slot: 1);

        var removal = Assert.Single(CreatePlan(lowerExperience, higherExperience).Removals);

        Assert.Equal(higherExperience, removal.Kept);
    }

    [Fact]
    public void PartyPokemonBreaksLevelAndExperienceTie()
    {
        var boxed = Candidate(123, level: 20, experience: 10_000, party: false, slot: 0);
        var party = Candidate(123, level: 20, experience: 10_000, party: true, slot: 1);

        var removal = Assert.Single(CreatePlan(boxed, party).Removals);

        Assert.Equal(party, removal.Kept);
    }

    [Fact]
    public void SamePidFromDifferentSpeciesIsNotDuplicate()
    {
        var pikachu = Candidate(123, species: 25, level: 20, experience: 10_000, PokemonStorageArea.Box, slot: 0);
        var raichu = Candidate(123, species: 26, level: 20, experience: 10_000, PokemonStorageArea.Box, slot: 1);

        var plan = CreatePlan(pikachu, raichu);

        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void PensionPokemonHasPriorityOverLevelAndExperience()
    {
        var pension = Candidate(123, species: 25, level: 5, experience: 500, PokemonStorageArea.Pension, slot: 0);
        var boxed = Candidate(123, species: 25, level: 100, experience: 1_000_000, PokemonStorageArea.Box, slot: 1);
        var party = Candidate(123, species: 25, level: 100, experience: 1_000_000, PokemonStorageArea.Party, slot: 2);

        var plan = CreatePlan(pension, boxed, party);

        Assert.Equal(2, plan.Removals.Count);
        Assert.All(plan.Removals, removal => Assert.Equal(pension, removal.Kept));
        Assert.DoesNotContain(plan.Removals, removal => removal.Removed.Location.IsPension);
    }

    [Fact]
    public void PensionDuplicatesAreNeverScheduledForRemoval()
    {
        var first = Candidate(123, species: 25, level: 5, experience: 500, PokemonStorageArea.Pension, slot: 0);
        var second = Candidate(123, species: 25, level: 6, experience: 600, PokemonStorageArea.Pension, slot: 1);

        var plan = CreatePlan(first, second);

        Assert.Empty(plan.Removals);
    }

    [Fact]
    public void FinalTieUsesProvidedRandomSource()
    {
        var first = Candidate(123, level: 20, experience: 10_000, party: false, slot: 0);
        var second = Candidate(123, level: 20, experience: 10_000, party: false, slot: 1);
        var third = Candidate(123, level: 20, experience: 10_000, party: false, slot: 2);

        var firstPlan = DuplicateRemovalPlanner.CreatePlan([first, second, third], new Random(42));
        var secondPlan = DuplicateRemovalPlanner.CreatePlan([first, second, third], new Random(42));

        Assert.Equal(firstPlan.Removals, secondPlan.Removals);
        Assert.Equal(2, firstPlan.Removals.Count);
    }

    [Fact]
    public void EveryDuplicateGroupKeepsExactlyOnePokemon()
    {
        var candidates = new[]
        {
            Candidate(100, level: 10, experience: 1_000, party: false, slot: 0),
            Candidate(100, level: 11, experience: 1_100, party: false, slot: 1),
            Candidate(100, level: 12, experience: 1_200, party: false, slot: 2),
            Candidate(200, level: 10, experience: 1_000, party: false, slot: 3),
            Candidate(200, level: 11, experience: 1_100, party: false, slot: 4),
            Candidate(300, level: 10, experience: 1_000, party: false, slot: 5),
        };

        var plan = DuplicateRemovalPlanner.CreatePlan(candidates, new Random(1));

        Assert.Equal(3, plan.Removals.Count);
        Assert.Equal(2, plan.DuplicateGroupCount);
    }

    [Fact]
    public void UniquePokemonProducesEmptyPlan()
    {
        var plan = CreatePlan(
            Candidate(100, level: 10, experience: 1_000, party: false, slot: 0),
            Candidate(200, level: 10, experience: 1_000, party: false, slot: 1));

        Assert.Empty(plan.Removals);
        Assert.Equal(0, plan.DuplicateGroupCount);
    }

    private static DuplicateRemovalPlan CreatePlan(params DuplicatePokemon[] candidates) =>
        DuplicateRemovalPlanner.CreatePlan(candidates, new Random(7));

    private static DuplicatePokemon Candidate(
        uint pid,
        byte level,
        uint experience,
        bool party,
        int slot) =>
        Candidate(
            pid,
            species: 25,
            level,
            experience,
            party ? PokemonStorageArea.Party : PokemonStorageArea.Box,
            slot);

    private static DuplicatePokemon Candidate(
        uint pid,
        ushort species,
        byte level,
        uint experience,
        PokemonStorageArea area,
        int slot) =>
        new(
            pid,
            species,
            level,
            experience,
            area switch
            {
                PokemonStorageArea.Party => PokemonStorageLocation.Party(slot),
                PokemonStorageArea.Box => PokemonStorageLocation.BoxSlot(0, slot),
                PokemonStorageArea.Pension => PokemonStorageLocation.Pension(0, slot),
                _ => throw new ArgumentOutOfRangeException(nameof(area)),
            });
}
