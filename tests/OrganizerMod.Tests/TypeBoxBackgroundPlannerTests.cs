using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class TypeBoxBackgroundPlannerTests
{
    private static readonly IReadOnlySet<BoxBackgroundTheme> AllThemes =
        Enum.GetValues<BoxBackgroundTheme>().ToHashSet();

    [Fact]
    public void DisabledOptionProducesNoAssignments()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [Box(4, PokemonElementType.Water)],
            Options(assign: false, rotate: true));

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(PokemonElementType.Normal, BoxBackgroundTheme.Checkered)]
    [InlineData(PokemonElementType.Fire, BoxBackgroundTheme.Volcano)]
    [InlineData(PokemonElementType.Water, BoxBackgroundTheme.DeepSea)]
    [InlineData(PokemonElementType.Electric, BoxBackgroundTheme.City)]
    [InlineData(PokemonElementType.Grass, BoxBackgroundTheme.Forest)]
    [InlineData(PokemonElementType.Ice, BoxBackgroundTheme.Snow)]
    [InlineData(PokemonElementType.Fighting, BoxBackgroundTheme.Steppe)]
    [InlineData(PokemonElementType.Poison, BoxBackgroundTheme.Cave)]
    [InlineData(PokemonElementType.Ground, BoxBackgroundTheme.Desert)]
    [InlineData(PokemonElementType.Flying, BoxBackgroundTheme.Sky)]
    [InlineData(PokemonElementType.Psychic, BoxBackgroundTheme.PokemonCenter)]
    [InlineData(PokemonElementType.Bug, BoxBackgroundTheme.Forest)]
    [InlineData(PokemonElementType.Rock, BoxBackgroundTheme.Rocky)]
    [InlineData(PokemonElementType.Ghost, BoxBackgroundTheme.Cave)]
    [InlineData(PokemonElementType.Dragon, BoxBackgroundTheme.Volcano)]
    [InlineData(PokemonElementType.Dark, BoxBackgroundTheme.Cave)]
    [InlineData(PokemonElementType.Steel, BoxBackgroundTheme.Metal)]
    [InlineData(PokemonElementType.Fairy, BoxBackgroundTheme.White)]
    public void EveryTypeUsesConfiguredPrimaryTheme(
        PokemonElementType type,
        BoxBackgroundTheme expected)
    {
        var result = TypeBoxBackgroundPlanner.Create([Box(2, type)], Options());

        var assignment = Assert.Single(result);
        Assert.Equal(2, assignment.BoxIndex);
        Assert.Equal(expected, assignment.Theme);
        Assert.Equal(BackgroundThemeChoice.Primary, assignment.Choice);
    }

    [Fact]
    public void SameTypeBoxesRotateDeterministicallyThroughAlternatives()
    {
        var boxes = Enumerable.Range(0, 4)
            .Select(index => Box(index, PokemonElementType.Water))
            .ToArray();

        var first = TypeBoxBackgroundPlanner.Create(boxes, Options(rotate: true));
        var second = TypeBoxBackgroundPlanner.Create(boxes.Reverse().Reverse().ToArray(), Options(rotate: true));

        Assert.Equal(
            [BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.River, BoxBackgroundTheme.Beach, BoxBackgroundTheme.DeepSea],
            first.Select(item => item.Theme));
        Assert.Equal(first, second);
    }

    [Fact]
    public void RotationDisabledUsesPrimaryForEveryBox()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [Box(1, PokemonElementType.Fire), Box(2, PokemonElementType.Fire)],
            Options(rotate: false));

        Assert.All(result, item => Assert.Equal(BoxBackgroundTheme.Volcano, item.Theme));
    }

    [Fact]
    public void UnsupportedPrimaryFallsBackToFirstSupportedAlternative()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [Box(1, PokemonElementType.Water)],
            Options(supported: new HashSet<BoxBackgroundTheme> { BoxBackgroundTheme.Beach }));

        var assignment = Assert.Single(result);
        Assert.Equal(BoxBackgroundTheme.Beach, assignment.Theme);
        Assert.Equal(BackgroundThemeChoice.Fallback, assignment.Choice);
    }

    [Fact]
    public void RotationFiltersUnsupportedThemesBeforeCycling()
    {
        var boxes = Enumerable.Range(0, 3)
            .Select(index => Box(index, PokemonElementType.Water))
            .ToArray();
        var supported = new HashSet<BoxBackgroundTheme>
        {
            BoxBackgroundTheme.DeepSea,
            BoxBackgroundTheme.River,
        };

        var result = TypeBoxBackgroundPlanner.Create(boxes, Options(rotate: true, supported: supported));

        Assert.Equal(
            [BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.River, BoxBackgroundTheme.DeepSea],
            result.Select(item => item.Theme));
    }

    [Fact]
    public void RotationStartsWithFirstActuallySupportedTheme()
    {
        var boxes = Enumerable.Range(0, 3)
            .Select(index => Box(index + 12, PokemonElementType.Water))
            .ToArray();
        var supported = new HashSet<BoxBackgroundTheme>
        {
            BoxBackgroundTheme.River,
            BoxBackgroundTheme.Beach,
        };

        var result = TypeBoxBackgroundPlanner.Create(boxes, Options(rotate: true, supported: supported));

        Assert.Equal(
            [BoxBackgroundTheme.River, BoxBackgroundTheme.Beach, BoxBackgroundTheme.River],
            result.Select(item => item.Theme));
    }

    [Fact]
    public void NoSupportedThemePreservesExistingAndWarns()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [Box(7, PokemonElementType.Water)],
            Options(supported: new HashSet<BoxBackgroundTheme> { BoxBackgroundTheme.City }));

        var assignment = Assert.Single(result);
        Assert.True(assignment.PreservesExisting);
        Assert.Equal(BackgroundThemeChoice.Preserved, assignment.Choice);
        Assert.Contains("Box 8", assignment.Warning);
    }

    [Fact]
    public void MixedBoxesUseStableNeutralThemeWithoutRotation()
    {
        var boxes = new[]
        {
            new TypeBoxAssignment(3, null, [], true),
            new TypeBoxAssignment(5, null, [], true),
        };

        var result = TypeBoxBackgroundPlanner.Create(boxes, Options(rotate: true));

        Assert.All(result, item => Assert.Equal(BoxBackgroundTheme.Checkered, item.Theme));
        Assert.Equal([3, 5], result.Select(item => item.BoxIndex));
    }

    [Fact]
    public void MixedBoxFallsBackToWhite()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [new TypeBoxAssignment(3, null, [], true)],
            Options(supported: new HashSet<BoxBackgroundTheme> { BoxBackgroundTheme.White }));

        var assignment = Assert.Single(result);
        Assert.Equal(BoxBackgroundTheme.White, assignment.Theme);
        Assert.Equal(BackgroundThemeChoice.Fallback, assignment.Choice);
    }

    [Fact]
    public void MixedBoxPreservesExistingWhenNeutralThemesAreUnsupported()
    {
        var result = TypeBoxBackgroundPlanner.Create(
            [new TypeBoxAssignment(3, null, [], true)],
            Options(supported: new HashSet<BoxBackgroundTheme> { BoxBackgroundTheme.Forest }));

        var assignment = Assert.Single(result);
        Assert.True(assignment.PreservesExisting);
        Assert.NotNull(assignment.Warning);
    }

    [Fact]
    public void EachGeneratedBoxGetsAtMostOneAssignment()
    {
        var boxes = new[]
        {
            Box(9, PokemonElementType.Water),
            Box(11, PokemonElementType.Water),
            Box(14, PokemonElementType.Fire),
        };

        var result = TypeBoxBackgroundPlanner.Create(boxes, Options(rotate: true));

        Assert.Equal(boxes.Length, result.Count);
        Assert.Equal(result.Count, result.Select(item => item.BoxIndex).Distinct().Count());
        Assert.Equal([9, 11, 14], result.Select(item => item.BoxIndex));
    }

    [Fact]
    public void MappingContainsEveryPokemonTypeAndNoDuplicateThemePerType()
    {
        Assert.Equal(Enum.GetValues<PokemonElementType>().Length, TypeBoxBackgroundMapping.TypeThemes.Count);
        foreach (var type in Enum.GetValues<PokemonElementType>())
        {
            var themes = TypeBoxBackgroundMapping.TypeThemes[type];
            Assert.NotEmpty(themes);
            Assert.Equal(themes.Count, themes.Distinct().Count());
        }
    }

    [Fact]
    public void OptionsNormalizeRotationWhenAssignmentIsDisabled()
    {
        var options = Options(assign: false, rotate: true);

        Assert.False(options.AssignMatchingBackgrounds);
        Assert.False(options.RotateAlternativeBackgrounds);
    }

    private static TypeBoxAssignment Box(int index, PokemonElementType type) =>
        new(index, type, [], false);

    private static TypeBoxOrganizerOptions Options(
        bool assign = true,
        bool rotate = false,
        IReadOnlySet<BoxBackgroundTheme>? supported = null) =>
        new(
            TypeBoxLayoutMode.Compact,
            renameBoxes: false,
            assignMatchingBackgrounds: assign,
            rotateAlternativeBackgrounds: rotate,
            supportedBackgroundThemes: supported ?? AllThemes);
}
