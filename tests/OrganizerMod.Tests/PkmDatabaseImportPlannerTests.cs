using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class PkmDatabaseImportPlannerTests
{
    private readonly PkmDatabaseImportPlanner planner = new();

    [Fact]
    public void FiltersAreSequentialAndCombineWithAnd()
    {
        var input = new[]
        {
            Db("illegal", legal: false),
            Db("origin", origin: 2),
            Db("level", level: 49),
            Db("gender", gender: PokemonGenderPreference.Male),
            Db("pass", level: 50, gender: PokemonGenderPreference.Female),
        };
        var plan = Plan(input, filters: new(LegalityFilterMode.OnlyLegal, 1, 50, PokemonGenderPreference.Female));
        Assert.Equal(1, plan.Summary.EligibleAfterFilters);
        Assert.Equal(new DatabaseFilterStatistics(1, 1, 1, 1), plan.Summary.Filters);
    }

    [Theory]
    [InlineData(49, 0)]
    [InlineData(50, 1)]
    [InlineData(51, 1)]
    public void MinimumLevelIsInclusive(int level, int expected) =>
        Assert.Equal(expected, Plan([Db("x", level: level)], filters: new(LegalityFilterMode.Regardless, null, 50, null)).Summary.EligibleAfterFilters);

    [Fact]
    public void UnrestrictedLegalityIncludesIllegal() =>
        Assert.Equal(2, Plan([Db("a", legal: true), Db("b", legal: false)]).Summary.EligibleAfterFilters);

    [Theory]
    [InlineData(PokemonGenderPreference.Male)]
    [InlineData(PokemonGenderPreference.Female)]
    [InlineData(PokemonGenderPreference.Genderless)]
    public void GenderFilterUsesStableGender(PokemonGenderPreference gender)
    {
        var plan = Plan([Db("yes", gender: gender), Db("no", gender: gender == PokemonGenderPreference.Male ? PokemonGenderPreference.Female : PokemonGenderPreference.Male)],
            filters: new(LegalityFilterMode.Regardless, null, null, gender));
        Assert.Single(plan.Imports);
        Assert.Equal("yes", plan.Imports[0].Candidate.StableId);
    }

    [Fact]
    public void SamePidImportAdditionallyUsesEmptySlotAndNeverReplaces()
    {
        var plan = Plan([Db("db", pid: 7)], [Save("save", pid: 7)], pid: SamePidImportMode.ImportAdditionally);
        Assert.Single(plan.Imports); Assert.Empty(plan.Replacements);
    }

    [Fact]
    public void SamePidDifferentSpeciesImportsAdditionallyInReplacementMode()
    {
        var plan = Plan([Db("db", species: 2, pid: 7)], [Save("save", species: 1, pid: 7)]);
        Assert.Single(plan.Imports); Assert.Empty(plan.Replacements); Assert.NotEmpty(plan.Warnings);
    }

    [Theory]
    [InlineData(51, 100, true)]
    [InlineData(50, 101, true)]
    [InlineData(50, 100, false)]
    [InlineData(49, 1000, false)]
    [InlineData(50, 99, false)]
    public void SamePidReplacementUsesLevelThenExperience(int level, ulong exp, bool replace)
    {
        var plan = Plan([Db("db", pid: 7, level: level, exp: exp)], [Save("save", pid: 7, level: 50, exp: 100)]);
        Assert.Equal(replace ? 1 : 0, plan.Replacements.Count);
        Assert.Equal(replace ? 0 : 1, plan.Summary.Skipped);
    }

    [Fact]
    public void MultiplePidMatchesSelectBestExistingThenEarliest()
    {
        var existing = new[] { Save("weak", pid: 7, level: 20), Save("late", pid: 7, level: 50, box: 1), Save("early", pid: 7, level: 50, slot: 2) };
        var plan = Plan([Db("db", pid: 7, level: 60)], existing);
        Assert.Equal("early", plan.Replacements[0].Existing.StableId);
    }

    [Fact]
    public void SamePidSkipHasPrecedenceOverSpeciesImport()
    {
        var plan = Plan([Db("db", pid: 7)], [Save("save", pid: 7)], pid: SamePidImportMode.DoNotImport,
            speciesMode: SameSpeciesShinyImportMode.ImportAdditionally);
        Assert.Empty(plan.Imports); Assert.Empty(plan.Replacements); Assert.Equal(DatabaseDecisionRule.SamePid, plan.Decisions[0].Rule);
    }

    [Fact]
    public void SpeciesShinyAdditionalIgnoresFormAndImportsAll()
    {
        var plan = Plan([Db("a", form: 0), Db("b", form: 3)], [Save("save", form: 2)],
            pid: SamePidImportMode.ImportAdditionally);
        Assert.Equal(2, plan.Imports.Count);
    }

    [Fact]
    public void DifferentShinyStatusDoesNotMatchSpeciesSkip()
    {
        var plan = Plan([Db("shiny", pid: 2, shiny: true)], [Save("normal")],
            speciesMode: SameSpeciesShinyImportMode.DoNotImportWhenExisting);
        Assert.Single(plan.Imports);
    }

    [Fact]
    public void BestDatabaseRepresentativeUsesLevelExperienceThenPath()
    {
        var plan = Plan([Db("z", path: "z.pk9", level: 50, exp: 100), Db("a", path: "a.pk9", level: 50, exp: 100)],
            speciesMode: SameSpeciesShinyImportMode.BestDatabaseRepresentativeReplaceWhenBetter);
        Assert.Single(plan.Imports); Assert.Equal("a", plan.Imports[0].Candidate.StableId); Assert.Equal(1, plan.Summary.Skipped);
    }

    [Fact]
    public void BestRepresentativeReplacesOnlyWhenBetter()
    {
        var stronger = Plan([Db("db", level: 60)], [Save("save", level: 50)],
            speciesMode: SameSpeciesShinyImportMode.BestDatabaseRepresentativeReplaceWhenBetter);
        var equal = Plan([Db("db", level: 50, exp: 100)], [Save("save", level: 50, exp: 100)],
            speciesMode: SameSpeciesShinyImportMode.BestDatabaseRepresentativeReplaceWhenBetter);
        Assert.Single(stronger.Replacements); Assert.Empty(equal.Replacements);
    }

    [Fact]
    public void DeterministicAllocationUsesSelectedEmptySlotsOnly()
    {
        var slots = new[] { new EmptySaveSlot(2, 1), new EmptySaveSlot(0, 5), new EmptySaveSlot(1, 0) };
        var plan = Plan([Db("b", path: "b"), Db("a", path: "a")], slots: slots, selected: new HashSet<int> { 0, 2 });
        Assert.Equal(new EmptySaveSlot(0, 5), plan.Imports[0].Destination);
        Assert.Equal(new EmptySaveSlot(2, 1), plan.Imports[1].Destination);
    }

    [Fact]
    public void ReplacementConsumesNoEmptyCapacity()
    {
        var plan = Plan([Db("replace", pid: 7, level: 60), Db("import", species: 2)], [Save("save", pid: 7, level: 20)],
            slots: [new(0, 4)]);
        Assert.Single(plan.Replacements); Assert.Single(plan.Imports); Assert.True(plan.IsValid);
    }

    [Fact]
    public void InsufficientCapacityInvalidatesPlanAndDestinationsAreUnique()
    {
        var plan = Plan([Db("a", species: 1), Db("b", species: 2)], slots: [new(0, 3)]);
        Assert.False(plan.IsValid);
        Assert.Equal(plan.Imports.Count, plan.Imports.Select(x => x.Destination).Distinct().Count());
    }

    [Fact]
    public void IncompatibleCandidateIsSkipped()
    {
        var plan = Plan([Db("bad", compatible: false)]);
        Assert.Empty(plan.Imports); Assert.Equal(1, plan.Summary.IncompatiblePokemon);
    }

    [Fact]
    public void EveryCandidateHasOneDecisionAndRepeatedRunsAreDeterministic()
    {
        var db = Enumerable.Range(0, 20).Select(i => Db($"{i}", species: 1 + i % 4, pid: (uint)i)).Reverse().ToArray();
        var first = Plan(db, slots: Enumerable.Range(0, 30).Select(i => new EmptySaveSlot(0, i)).ToArray());
        var second = Plan(db.Reverse().ToArray(), slots: Enumerable.Range(0, 30).Select(i => new EmptySaveSlot(0, i)).Reverse().ToArray());
        Assert.Equal(db.Length, first.Decisions.Count);
        Assert.Equal(first.Decisions.Select(Id), second.Decisions.Select(Id));
    }

    private DatabaseImportPlan Plan(
        IReadOnlyList<DatabasePokemonCandidate> db,
        IReadOnlyList<ExistingSavePokemon>? existing = null,
        SamePidImportMode pid = SamePidImportMode.ReplaceWhenMoreAdvanced,
        SameSpeciesShinyImportMode speciesMode = SameSpeciesShinyImportMode.ImportAdditionally,
        PkmDatabaseFilterOptions? filters = null,
        IReadOnlyList<EmptySaveSlot>? slots = null,
        IReadOnlySet<int>? selected = null) =>
        planner.CreatePlan(db, existing ?? [], slots ?? [new(0, 10), new(0, 11), new(0, 12), new(0, 13)],
            new(pid, speciesMode, filters ?? new(LegalityFilterMode.Regardless, null, null, null), selected ?? new HashSet<int> { 0, 1 }));

    private static DatabasePokemonCandidate Db(string id, string? path = null, uint pid = 1, int species = 1, int form = 0, bool shiny = false,
        int level = 50, ulong exp = 100, int origin = 1, PokemonGenderPreference gender = PokemonGenderPreference.Female, bool? legal = true, bool compatible = true) =>
        new(id, path ?? $"{id}.pk9", pid, species, form, shiny, level, exp, origin, gender, legal, compatible);
    private static ExistingSavePokemon Save(string id, uint pid = 1, int species = 1, int form = 0, bool shiny = false,
        int level = 50, ulong exp = 100, int origin = 1, PokemonGenderPreference gender = PokemonGenderPreference.Female, int box = 0, int slot = 0) =>
        new(id, pid, species, form, shiny, level, exp, origin, gender, box, slot);
    private static string Id(DatabaseImportDecision d) => $"{d.Candidate.StableId}:{d.Kind}:{d.Rule}:{d.ImportDestination}:{d.ReplacementTarget?.StableId}";
}
