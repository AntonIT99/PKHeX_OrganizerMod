using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class LivingDexOrganizationPlannerTests
{
    private readonly LivingDexOrganizationPlanner planner = new();

    [Fact]
    public void EmptyInputProducesValidMissingAnalysis()
    {
        var plan = Plan([], [Definition(1), Definition(2)], 1);

        Assert.True(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Equal(2, plan.MissingEntries.Count);
        Assert.Equal(0, plan.Summary.CompletionPercentage);
    }

    [Fact]
    public void OnePokemonFillsSpeciesEntry()
    {
        var plan = Plan([Candidate(0, 1)], [Definition(1)], 1);

        Assert.Equal(1, plan.Summary.FilledEntries);
        Assert.Equal(100, plan.Summary.CompletionPercentage);
        Assert.False(Assert.Single(plan.Assignments).IsOverflow);
    }

    [Fact]
    public void DuplicateSpeciesProducesOverflowWithoutLoss()
    {
        var plan = Plan(
            [Candidate(0, 25), Candidate(1, 25)],
            [Definition(25)],
            2);

        Assert.Equal(1, plan.Summary.MainPokemon);
        Assert.Equal(1, plan.Summary.DuplicatePokemon);
        Assert.Equal(1, plan.Summary.OverflowPokemon);
        Assert.Equal(2, plan.Assignments.Count);
    }

    [Fact]
    public void MainEntriesUseNationalDexOrder()
    {
        var plan = Plan(
            [Candidate(0, 25), Candidate(1, 1), Candidate(2, 7)],
            [Definition(25), Definition(7), Definition(1)],
            1);

        Assert.Equal(
            [1, 7, 25],
            plan.Assignments
                .Where(item => !item.IsOverflow)
                .OrderBy(item => item.TargetSlotIndex)
                .Select(item => item.Entry!.Value.Species));
    }

    [Fact]
    public void MissingSpeciesAndCompletionAreCorrect()
    {
        var plan = Plan(
            [Candidate(0, 1), Candidate(1, 3)],
            [Definition(1), Definition(2), Definition(3), Definition(4)],
            1);

        Assert.Equal([2, 4], plan.MissingEntries.Select(item => item.Definition.Key.Species));
        Assert.Equal(50, plan.Summary.CompletionPercentage);
    }

    [Fact]
    public void FormDexOrdersBaseBeforeAlternateForms()
    {
        var options = Options(mode: LivingDexMode.Form);
        var plan = Plan(
            [Candidate(0, 386, form: 2), Candidate(1, 386, form: 0), Candidate(2, 386, form: 1)],
            [Definition(386, 2), Definition(386, 0), Definition(386, 1)],
            1,
            options);

        Assert.Equal(
            [0, 1, 2],
            plan.Assignments
                .Where(item => !item.IsOverflow)
                .OrderBy(item => item.TargetSlotIndex)
                .Select(item => item.Entry!.Value.Form));
    }

    [Fact]
    public void DistinctCollectibleFormsReceiveSeparateEntries()
    {
        var plan = Plan(
            [Candidate(0, 479, form: 0), Candidate(1, 479, form: 1)],
            [Definition(479, 0), Definition(479, 1)],
            1,
            Options(mode: LivingDexMode.Form));

        Assert.Equal(2, plan.Summary.FilledEntries);
        Assert.Equal(0, plan.Summary.OverflowPokemon);
    }

    [Fact]
    public void DuplicateSameFormGoesToOverflow()
    {
        var plan = Plan(
            [Candidate(0, 479, form: 1), Candidate(1, 479, form: 1)],
            [Definition(479, 0), Definition(479, 1)],
            2,
            Options(mode: LivingDexMode.Form));

        Assert.Equal(1, plan.Summary.FilledEntries);
        Assert.Equal(1, plan.Summary.OverflowPokemon);
    }

    [Fact]
    public void FormAbsentFromDefinitionCannotFillDifferentForm()
    {
        var plan = Plan(
            [Candidate(0, 479, form: 2)],
            [Definition(479, 0), Definition(479, 1)],
            2,
            Options(mode: LivingDexMode.Form));

        Assert.Equal(0, plan.Summary.FilledEntries);
        Assert.Equal(1, plan.Summary.OverflowPokemon);
        Assert.Equal(2, plan.MissingEntries.Count);
    }

    [Fact]
    public void ShinyPokemonFillsShinyEntryAndNonShinyOverflows()
    {
        var plan = Plan(
            [Candidate(0, 1, shiny: false), Candidate(1, 1, shiny: true)],
            [Definition(1, shiny: true)],
            2,
            Options(mode: LivingDexMode.Shiny));

        var main = Assert.Single(plan.Assignments, item => !item.IsOverflow);
        Assert.Equal("P001", main.Pokemon.StableId);
        Assert.Equal(1, plan.Summary.OverflowPokemon);
        Assert.NotEmpty(plan.Warnings);
    }

    [Fact]
    public void DuplicateShiniesUseOneRepresentative()
    {
        var plan = Plan(
            [Candidate(0, 4, shiny: true), Candidate(1, 4, shiny: true)],
            [Definition(4, shiny: true)],
            2,
            Options(mode: LivingDexMode.Shiny));

        Assert.Equal(1, plan.Summary.FilledEntries);
        Assert.Equal(1, plan.Summary.DuplicatePokemon);
        Assert.Equal(1, plan.Summary.OverflowPokemon);
    }

    [Fact]
    public void ShinySpeciesScopeIgnoresFormDifference()
    {
        var plan = Plan(
            [Candidate(0, 6, form: 1, shiny: true)],
            [Definition(6, shiny: true)],
            1,
            Options(mode: LivingDexMode.Shiny, shinyScope: LivingDexShinyScope.Species));

        Assert.Equal(1, plan.Summary.FilledEntries);
    }

    [Fact]
    public void ShinyFormScopeDistinguishesForms()
    {
        var plan = Plan(
            [Candidate(0, 6, form: 1, shiny: true)],
            [Definition(6, 0, true), Definition(6, 1, true)],
            1,
            Options(mode: LivingDexMode.Shiny, shinyScope: LivingDexShinyScope.Form));

        Assert.Equal(1, plan.Summary.FilledEntries);
        Assert.Equal(0, Assert.Single(plan.MissingEntries).Definition.Key.Form);
    }

    [Fact]
    public void LegalCandidateIsPreferred()
    {
        var plan = Plan(
            [Candidate(0, 1, legal: false, level: 100), Candidate(1, 1, legal: true, level: 1)],
            [Definition(1)],
            2);

        Assert.Equal("P001", MainReference(plan));
    }

    [Fact]
    public void DefaultPreferenceFavorsCurrentTrainer()
    {
        var plan = Plan(
            [Candidate(0, 1, owned: false, level: 100), Candidate(1, 1, owned: true, level: 1)],
            [Definition(1)],
            2);

        Assert.Equal("P001", MainReference(plan));
    }

    [Fact]
    public void StrongestPreferenceUsesLevelIvAndEv()
    {
        var plan = Plan(
            [
                Candidate(0, 1, level: 50, ivTotal: 180, evTotal: 300),
                Candidate(1, 1, level: 60, ivTotal: 100, evTotal: 0),
            ],
            [Definition(1)],
            2,
            Options(preference: LivingDexRepresentativePreference.Strongest));

        Assert.Equal("P001", MainReference(plan));
    }

    [Fact]
    public void OldestPreferenceUsesEarliestValidDate()
    {
        var plan = Plan(
            [
                Candidate(0, 1, date: new DateOnly(2020, 1, 1)),
                Candidate(1, 1, date: new DateOnly(2010, 1, 1)),
            ],
            [Definition(1)],
            2,
            Options(preference: LivingDexRepresentativePreference.OldestObtained));

        Assert.Equal("P001", MainReference(plan));
    }

    [Fact]
    public void FinalTieUsesOriginalLocationDeterministically()
    {
        var pokemon = new[]
        {
            Candidate(1, 1, sourceBox: 1, sourceSlot: 0),
            Candidate(0, 1, sourceBox: 0, sourceSlot: 4),
        };

        var first = Plan(pokemon, [Definition(1)], 2);
        var second = Plan(pokemon, [Definition(1)], 2);

        Assert.Equal("P000", MainReference(first));
        Assert.Equal(first.Assignments, second.Assignments);
    }

    [Fact]
    public void EggsNeverFillEntriesAndDefaultToOverflow()
    {
        var plan = Plan(
            [Candidate(0, 1, egg: true)],
            [Definition(1)],
            2);

        Assert.Equal(0, plan.Summary.FilledEntries);
        Assert.True(Assert.Single(plan.Assignments).IsOverflow);
    }

    [Fact]
    public void ExcludedEggRemainsFixedAndItsSlotIsNotUsed()
    {
        var plan = Plan(
            [Candidate(0, 1, egg: true), Candidate(1, 2)],
            [Definition(2)],
            1,
            Options(eggHandling: LivingDexEggHandling.ExcludeAndPreserve));

        Assert.Equal("P000", Assert.Single(plan.PreservedPokemon).StableId);
        Assert.DoesNotContain(
            plan.Assignments,
            item => item.TargetBoxIndex == 0 && item.TargetSlotIndex == 0);
    }

    [Fact]
    public void InvalidPokemonOverflowsOrIsPreservedAccordingToOption()
    {
        var overflow = Plan(
            [Candidate(0, 1, validData: false)],
            [Definition(1)],
            2);
        var preserved = Plan(
            [Candidate(0, 1, validData: false)],
            [Definition(1)],
            1,
            Options(invalidHandling: LivingDexInvalidHandling.ExcludeAndPreserve));

        Assert.True(Assert.Single(overflow.Assignments).IsOverflow);
        Assert.Empty(preserved.Assignments);
        Assert.Single(preserved.PreservedPokemon);
    }

    [Fact]
    public void OverflowOrderingModesAreDeterministic()
    {
        var pokemon = new[]
        {
            Candidate(0, 25, sourceBox: 0, sourceSlot: 8),
            Candidate(1, 1, sourceBox: 0, sourceSlot: 4),
            Candidate(2, 7, sourceBox: 0, sourceSlot: 2),
        };
        foreach (var order in Enum.GetValues<LivingDexOverflowOrder>())
        {
            var options = Options(overflowOrder: order);
            var first = Plan(pokemon, [], 2, options);
            var second = Plan(pokemon, [], 2, options);
            Assert.Equal(first.Assignments, second.Assignments);
        }
    }

    [Fact]
    public void NextBoundarySeparatesMainAndOverflow()
    {
        var plan = Plan(
            [Candidate(0, 1), Candidate(1, 1)],
            [Definition(1)],
            2);

        var overflow = Assert.Single(plan.Assignments, item => item.IsOverflow);
        Assert.Equal(1, overflow.TargetBoxIndex);
        Assert.Equal(0, overflow.TargetSlotIndex);
    }

    [Fact]
    public void ImmediateOverflowUsesNextFreeSlot()
    {
        var plan = Plan(
            [Candidate(0, 1), Candidate(1, 1)],
            [Definition(1)],
            1,
            Options(overflowStart: LivingDexOverflowStart.ImmediatelyAfterEntries));

        var overflow = Assert.Single(plan.Assignments, item => item.IsOverflow);
        Assert.Equal(0, overflow.TargetBoxIndex);
        Assert.Equal(1, overflow.TargetSlotIndex);
    }

    [Fact]
    public void BoundaryCapacityFailureProducesNoApplicablePlan()
    {
        var pokemon = Enumerable.Range(0, 30).Select(index => Candidate(index, 1)).ToArray();
        var plan = Plan(pokemon, [Definition(1)], 1);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Contains("overflow boundary", Assert.Single(plan.Errors));
    }

    [Fact]
    public void InsufficientTotalCapacityProducesNoApplicablePlan()
    {
        var pokemon = Enumerable.Range(0, 31)
            .Select(index => Candidate(index, 1, sourceBox: 0, sourceSlot: index))
            .ToArray();
        var plan = Plan(
            pokemon,
            [],
            1,
            Options(overflowStart: LivingDexOverflowStart.ImmediatelyAfterEntries));

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Contains("only 30 selected target slots", Assert.Single(plan.Errors));
    }

    [Fact]
    public void UnselectedSourceBoxIsRejected()
    {
        var plan = planner.CreatePlan(
            [Candidate(0, 1, sourceBox: 1)],
            [Definition(1)],
            [new BoxState(0, "Selected")],
            Options());

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void RenameConventionsAndLimitsAreApplied()
    {
        var pokemon = Enumerable.Range(0, 65)
            .Select(index => Candidate(index, index + 1))
            .Concat(Enumerable.Range(65, 35).Select(index => Candidate(index, 1)))
            .ToArray();
        var definitions = Enumerable.Range(1, 65).Select(species => Definition(species)).ToArray();
        var options = Options(rename: true, maxNameLength: 10);
        var plan = Plan(pokemon, definitions, 5, options);

        Assert.Contains(plan.RenameOperations, item => item.NewName == "Living  1" || item.NewName == "Living D 1");
        Assert.Contains(plan.RenameOperations, item => item.NewName.StartsWith("Overflow", StringComparison.Ordinal));
        Assert.All(plan.RenameOperations, item => Assert.True(item.NewName.Length <= 10));
        Assert.All(plan.RenameOperations, item => Assert.Contains(item.BoxIndex, plan.Boxes.Select(box => box.TargetBoxIndex)));
    }

    [Fact]
    public void RenamingDisabledCreatesNoOperations()
    {
        var plan = Plan([Candidate(0, 1)], [Definition(1)], 1);

        Assert.Empty(plan.RenameOperations);
    }

    [Fact]
    public void OneOverflowBoxUsesUnnumberedName()
    {
        var plan = Plan(
            [Candidate(0, 1), Candidate(1, 1)],
            [Definition(1)],
            2,
            Options(rename: true));

        Assert.Contains(plan.RenameOperations, item => item.NewName == "Overflow");
    }

    [Fact]
    public void OneMainBoxUsesUnnumberedName()
    {
        var plan = Plan(
            [Candidate(0, 1)],
            [Definition(1)],
            1,
            Options(rename: true));

        Assert.Contains(plan.RenameOperations, item => item.NewName == "Living Dex");
    }

    [Fact]
    public void MultipleOverflowBoxesAreNumberedConsistently()
    {
        var pokemon = Enumerable.Range(0, 61)
            .Select(index => Candidate(index, 1))
            .ToArray();
        var plan = Plan(
            pokemon,
            [],
            3,
            Options(
                overflowStart: LivingDexOverflowStart.ImmediatelyAfterEntries,
                rename: true));

        Assert.Equal(
            ["Overflow 1", "Overflow 2", "Overflow 3"],
            plan.RenameOperations.Select(item => item.NewName));
    }

    [Fact]
    public void RandomizedPlanPreservesEveryIncludedPokemonExactlyOnce()
    {
        var random = new Random(8675309);
        var pokemon = Enumerable.Range(0, 300)
            .Select(index => Candidate(
                index,
                random.Next(1, 151),
                form: random.Next(0, 3),
                shiny: random.Next(8) == 0,
                sourceBox: index / 30,
                sourceSlot: index % 30))
            .ToArray();
        var definitions = Enumerable.Range(1, 150).Select(species => Definition(species)).ToArray();

        var first = Plan(pokemon, definitions, 12);
        var second = Plan(pokemon, definitions, 12);

        Assert.True(first.IsValid);
        Assert.Equal(pokemon.Length, first.Assignments.Count + first.PreservedPokemon.Count);
        Assert.Equal(
            first.Assignments.Count,
            first.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());
        Assert.Equal(
            pokemon.Select(item => item.Reference.StableId).Order(),
            first.Assignments.Select(item => item.Pokemon.StableId)
                .Concat(first.PreservedPokemon.Select(item => item.StableId))
                .Order());
        Assert.Equal(first.Assignments, second.Assignments);
        Assert.All(
            first.Assignments.Where(item => !item.IsOverflow)
                .GroupBy(item => item.Entry),
            group => Assert.Single(group));
        Assert.All(first.Boxes, box => Assert.True(box.PokemonCount <= 30));
    }

    private LivingDexOrganizationPlan Plan(
        IReadOnlyList<LivingDexCandidate> pokemon,
        IReadOnlyList<LivingDexEntryDefinition> definitions,
        int boxCount,
        LivingDexOrganizerOptions? options = null)
    {
        var boxes = Enumerable.Range(0, boxCount)
            .Select(index => new BoxState(index, $"Box {index + 1}"))
            .ToArray();
        return planner.CreatePlan(pokemon, definitions, boxes, options ?? Options());
    }

    private static string MainReference(LivingDexOrganizationPlan plan) =>
        Assert.Single(plan.Assignments, item => !item.IsOverflow).Pokemon.StableId;

    private static LivingDexEntryDefinition Definition(
        int species,
        int form = 0,
        bool shiny = false) =>
        new(
            new LivingDexEntryKey(species, form, shiny),
            $"Species {species}",
            form == 0 ? null : $"Form {form}");

    private static LivingDexCandidate Candidate(
        int id,
        int species,
        int form = 0,
        bool shiny = false,
        bool egg = false,
        bool validData = true,
        bool legal = true,
        bool owned = true,
        bool favorite = false,
        int level = 50,
        int ivTotal = 100,
        int evTotal = 0,
        int ribbons = 0,
        DateOnly? date = null,
        int sourceBox = -1,
        int sourceSlot = -1)
    {
        if (sourceBox < 0)
            sourceBox = id / 30;
        if (sourceSlot < 0)
            sourceSlot = id % 30;
        return new LivingDexCandidate(
            new PokemonReference($"P{id:D3}", sourceBox, sourceSlot),
            species,
            form,
            shiny,
            egg,
            validData,
            legal,
            owned,
            favorite,
            level,
            ivTotal,
            evTotal,
            ribbons,
            date);
    }

    private static LivingDexOrganizerOptions Options(
        LivingDexMode mode = LivingDexMode.Species,
        LivingDexShinyScope shinyScope = LivingDexShinyScope.Species,
        LivingDexRepresentativePreference preference = LivingDexRepresentativePreference.DefaultSafest,
        LivingDexEggHandling eggHandling = LivingDexEggHandling.KeepInOverflow,
        LivingDexInvalidHandling invalidHandling = LivingDexInvalidHandling.KeepInOverflow,
        LivingDexOverflowOrder overflowOrder = LivingDexOverflowOrder.NationalDex,
        LivingDexOverflowStart overflowStart = LivingDexOverflowStart.NextBoxBoundary,
        bool rename = false,
        int maxNameLength = 16) =>
        new(
            mode,
            shinyScope,
            preference,
            eggHandling,
            invalidHandling,
            overflowOrder,
            overflowStart,
            rename,
            maxNameLength);
}
