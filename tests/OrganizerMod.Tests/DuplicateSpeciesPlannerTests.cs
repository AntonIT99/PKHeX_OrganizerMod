using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class DuplicateSpeciesPlannerTests
{
    private readonly DuplicateSpeciesPlanner planner = new();

    [Fact]
    public void EmptyInputProducesNoRemovals()
    {
        var plan = Plan([]);
        Assert.Empty(plan.Decisions);
        Assert.Empty(plan.RemovalCandidates);
        Assert.True(plan.IsValid);
    }

    [Fact]
    public void SinglePokemonProducesNoDuplicateGroup() =>
        Assert.Empty(Plan([Candidate("a", 25)]).Decisions);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void DuplicateSpeciesKeepExactlyOne(int count, int removed)
    {
        var input = Enumerable.Range(0, count)
            .Select(index => Candidate($"{index}", 25, slot: index))
            .ToArray();
        var plan = Plan(input);
        Assert.Single(plan.Decisions);
        Assert.Equal(removed, plan.RemovalCandidates.Count);
    }

    [Fact]
    public void DifferentSpeciesAreNotGrouped() =>
        Assert.Empty(Plan([Candidate("a", 1), Candidate("b", 2, slot: 1)]).Decisions);

    [Fact]
    public void AlternateFormsOfSameSpeciesAreGrouped()
    {
        var plan = Plan([Candidate("a", 479, form: 0), Candidate("b", 479, form: 3, slot: 1)]);
        Assert.Single(plan.Decisions);
    }

    [Fact]
    public void UnselectedBoxesAreExcluded()
    {
        var options = Options(selected: new HashSet<int> { 0 });
        var plan = planner.CreatePlan(
            [Candidate("a", 25), Candidate("b", 25, box: 1)],
            options);
        Assert.Empty(plan.Decisions);
    }

    [Fact]
    public void HighestLevelWinsRegardlessOfInputOrder()
    {
        var criterion = Criterion(DuplicateSelectionCriterionType.HighestLevel);
        var low = Candidate("low", 25, level: 10);
        var high = Candidate("high", 25, level: 80, slot: 1);
        Assert.Equal("high", Plan([low, high], [criterion]).Decisions[0].Kept.Reference.StableId);
        Assert.Equal("high", Plan([high, low], [criterion]).Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void TiedLevelsProceedToOrigin()
    {
        var plan = Plan(
            [
                Candidate("a", 25, level: 50, origin: 1),
                Candidate("b", 25, level: 50, origin: 12, slot: 1),
            ],
            [
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
                Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 12),
            ]);
        Assert.Equal("b", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void PreferredOriginMatchesStableIdentifier()
    {
        var plan = Plan(
            [Candidate("a", 25, origin: 12), Candidate("b", 25, origin: 50, slot: 1)],
            [Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 12)]);
        Assert.Equal("a", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void MissingPreferredOriginDoesNotEliminateCandidates()
    {
        var plan = Plan(
            [Candidate("a", 25, level: 20), Candidate("b", 25, level: 80, slot: 1)],
            [
                Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 12),
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
            ]);
        Assert.Equal("b", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void PreferredGenderMatchesAndGenderlessIsSupported()
    {
        var plan = Plan(
            [
                Candidate("a", 81, gender: PokemonGenderPreference.Male),
                Candidate("b", 81, gender: PokemonGenderPreference.Genderless, slot: 1),
            ],
            [Criterion(DuplicateSelectionCriterionType.PreferredGender, gender: PokemonGenderPreference.Genderless)]);
        Assert.Equal("b", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void MissingPreferredGenderContinuesToNextCriterion()
    {
        var plan = Plan(
            [
                Candidate("a", 25, level: 20, gender: PokemonGenderPreference.Male),
                Candidate("b", 25, level: 80, gender: PokemonGenderPreference.Male, slot: 1),
            ],
            [
                Criterion(DuplicateSelectionCriterionType.PreferredGender, gender: PokemonGenderPreference.Female),
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
            ]);
        Assert.Equal("b", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void CriterionOrderChangesRepresentative()
    {
        var a = Candidate("a", 25, level: 40, origin: 12, gender: PokemonGenderPreference.Male);
        var b = Candidate("b", 25, level: 80, origin: 50, gender: PokemonGenderPreference.Female, slot: 1);
        var originFirst = Plan(
            [a, b],
            [
                Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 12),
                Criterion(DuplicateSelectionCriterionType.PreferredGender, gender: PokemonGenderPreference.Female),
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
            ]);
        var genderFirst = Plan(
            [a, b],
            [
                Criterion(DuplicateSelectionCriterionType.PreferredGender, gender: PokemonGenderPreference.Female),
                Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 12),
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
            ]);
        Assert.Equal("a", originFirst.Decisions[0].Kept.Reference.StableId);
        Assert.Equal("b", genderFirst.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void DisabledCriterionHasNoEffectOrValueRequirement()
    {
        var plan = Plan(
            [Candidate("early", 25, level: 1), Candidate("late", 25, level: 100, slot: 1)],
            [new DuplicateSelectionCriterion(DuplicateSelectionCriterionType.PreferredOriginGame, false)]);
        Assert.True(plan.IsValid);
        Assert.Equal("early", plan.Decisions[0].Kept.Reference.StableId);
    }

    [Fact]
    public void FallbackUsesBoxThenSlotAndIsInputOrderIndependent()
    {
        var later = Candidate("later", 25, box: 1, slot: 0);
        var earlier = Candidate("earlier", 25, box: 0, slot: 5);
        var first = Plan([later, earlier]);
        var second = Plan([earlier, later]);
        Assert.Equal("earlier", first.Decisions[0].Kept.Reference.StableId);
        Assert.Equal(
            first.Decisions.Select(DecisionIdentity),
            second.Decisions.Select(DecisionIdentity));
    }

    [Fact]
    public void CombinedShinyModeKeepsOneTotal()
    {
        var plan = Plan(
            [Candidate("normal", 25), Candidate("shiny", 25, shiny: true, slot: 1)],
            shiny: ShinyDuplicateMode.CombinedWithNonShiny);
        Assert.Single(plan.Decisions);
        Assert.Single(plan.RemovalCandidates);
    }

    [Fact]
    public void SeparateShinyModeCreatesIndependentGroups()
    {
        var plan = Plan(
            [
                Candidate("n1", 25),
                Candidate("n2", 25, slot: 1),
                Candidate("s1", 25, shiny: true, slot: 2),
                Candidate("s2", 25, shiny: true, slot: 3),
            ],
            shiny: ShinyDuplicateMode.SeparateShinyGroup);
        Assert.Equal(2, plan.Decisions.Count);
        Assert.Equal(2, plan.RemovalCandidates.Count);
        Assert.Contains(plan.Decisions, item => item.Key.IsShiny == false);
        Assert.Contains(plan.Decisions, item => item.Key.IsShiny == true);
    }

    [Fact]
    public void IgnoreShinyLeavesAllShiniesUntouched()
    {
        var plan = Plan(
            [
                Candidate("n1", 25),
                Candidate("n2", 25, slot: 1),
                Candidate("s1", 25, shiny: true, slot: 2),
                Candidate("s2", 25, shiny: true, slot: 3),
            ],
            shiny: ShinyDuplicateMode.IgnoreShiny);
        Assert.Single(plan.RemovalCandidates);
        Assert.All(plan.RemovalCandidates, item => Assert.False(item.IsShiny));
        Assert.Equal(2, plan.Summary.ShinyPokemonIgnored);
    }

    [Fact]
    public void EggsAndInvalidEntriesAreExcluded()
    {
        var plan = Plan(
            [
                Candidate("valid", 25),
                Candidate("egg", 25, egg: true, slot: 1),
                Candidate("invalid", 25, valid: false, slot: 2),
            ]);
        Assert.Empty(plan.Decisions);
        Assert.Equal(1, plan.Summary.EggsIgnored);
        Assert.Equal(1, plan.Summary.InvalidEntriesIgnored);
    }

    [Fact]
    public void RemovalSlotsAreUniqueAndKeptIsNeverRemoved()
    {
        var plan = Plan(
            Enumerable.Range(0, 12)
                .Select(index => Candidate($"{index}", 1 + (index % 3), slot: index))
                .ToArray());
        Assert.Equal(
            plan.RemovalCandidates.Count,
            plan.RemovalCandidates.Select(item => item.Reference).Distinct().Count());
        Assert.All(
            plan.Decisions,
            decision => Assert.DoesNotContain(
                decision.Removed,
                item => item.Reference == decision.Kept.Reference));
        Assert.All(plan.Decisions, decision => Assert.NotEmpty(decision.Reasons));
    }

    [Fact]
    public void PlannerDoesNotMutateInputAndRepeatedRunsAreEqual()
    {
        var input = new[]
        {
            Candidate("a", 25, level: 10),
            Candidate("b", 25, level: 20, slot: 1),
        };
        var original = input.ToArray();
        var first = Plan(input, [Criterion(DuplicateSelectionCriterionType.HighestLevel)]);
        var second = Plan(input, [Criterion(DuplicateSelectionCriterionType.HighestLevel)]);
        Assert.Equal(original, input);
        Assert.Equal(
            first.Decisions.Select(DecisionIdentity),
            second.Decisions.Select(DecisionIdentity));
        Assert.Equal(first.RemovalCandidates, second.RemovalCandidates);
    }

    [Fact]
    public void MissingParameterizedValuesAndUnsupportedOriginAreRejected()
    {
        var missingOrigin = Plan(
            [Candidate("a", 25), Candidate("b", 25, slot: 1)],
            [Criterion(DuplicateSelectionCriterionType.PreferredOriginGame)]);
        var missingGender = Plan(
            [Candidate("a", 25), Candidate("b", 25, slot: 1)],
            [Criterion(DuplicateSelectionCriterionType.PreferredGender)]);
        var unsupportedOptions = Options(
            [Criterion(DuplicateSelectionCriterionType.PreferredOriginGame, origin: 99)],
            supported: new HashSet<int> { 12 });
        var unsupported = planner.CreatePlan(
            [Candidate("a", 25), Candidate("b", 25, slot: 1)],
            unsupportedOptions);
        Assert.False(missingOrigin.IsValid);
        Assert.False(missingGender.IsValid);
        Assert.False(unsupported.IsValid);
    }

    [Fact]
    public void DuplicateCriterionTypesAreRejected()
    {
        var plan = Plan(
            [Candidate("a", 25), Candidate("b", 25, slot: 1)],
            [
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
                Criterion(DuplicateSelectionCriterionType.HighestLevel),
            ]);
        Assert.False(plan.IsValid);
    }

    [Fact]
    public void CriterionMoveRespectsOrderAndBoundaries()
    {
        string[] values = ["a", "b", "c"];
        Assert.Equal(["b", "a", "c"], DuplicateCriterionList.Move(values, 1, -1));
        Assert.Equal(["a", "c", "b"], DuplicateCriterionList.Move(values, 1, 1));
        Assert.Equal(values, DuplicateCriterionList.Move(values, 0, -1));
        Assert.Equal(values, DuplicateCriterionList.Move(values, 2, 1));
    }

    private SpeciesDuplicateRemovalPlan Plan(
        IReadOnlyList<DuplicateCandidate> candidates,
        IReadOnlyList<DuplicateSelectionCriterion>? criteria = null,
        ShinyDuplicateMode shiny = ShinyDuplicateMode.CombinedWithNonShiny) =>
        planner.CreatePlan(candidates, Options(criteria, shiny));

    private static DuplicateSpeciesOptions Options(
        IReadOnlyList<DuplicateSelectionCriterion>? criteria = null,
        ShinyDuplicateMode shiny = ShinyDuplicateMode.CombinedWithNonShiny,
        IReadOnlySet<int>? selected = null,
        IReadOnlySet<int>? supported = null) =>
        new(
            shiny,
            criteria ?? [],
            selected ?? new HashSet<int> { 0, 1 },
            supported);

    private static DuplicateSelectionCriterion Criterion(
        DuplicateSelectionCriterionType type,
        int? origin = null,
        PokemonGenderPreference? gender = null) =>
        new(type, true, origin, gender);

    private static DuplicateCandidate Candidate(
        string id,
        int species,
        int form = 0,
        bool shiny = false,
        int level = 1,
        int origin = 1,
        PokemonGenderPreference gender = PokemonGenderPreference.Male,
        int box = 0,
        int slot = 0,
        bool egg = false,
        bool valid = true) =>
        new(
            new PokemonReference(id, box, slot),
            species,
            form,
            shiny,
            level,
            origin,
            gender,
            egg,
            valid);

    private static string DecisionIdentity(DuplicateRemovalDecision decision) =>
        $"{decision.Key}:{decision.Kept.Reference.StableId}:" +
        $"{string.Join(",", decision.Removed.Select(item => item.Reference.StableId))}:" +
        string.Join("|", decision.Reasons);
}
