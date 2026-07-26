using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum BoxBackgroundTheme
{
    Forest,
    City,
    Desert,
    Steppe,
    Rocky,
    Volcano,
    Snow,
    Cave,
    Beach,
    DeepSea,
    River,
    Sky,
    PokemonCenter,
    Metal,
    Checkered,
    White,
}

public enum BackgroundThemeChoice
{
    Primary,
    Alternative,
    Fallback,
    Preserved,
}

public sealed record PlannedBoxBackgroundTheme(
    int BoxIndex,
    PokemonElementType? AssignedType,
    bool IsMixed,
    BoxBackgroundTheme? Theme,
    BackgroundThemeChoice Choice,
    string? Warning)
{
    public bool PreservesExisting => Theme is null;
}

public static class TypeBoxBackgroundMapping
{
    private static readonly IReadOnlyDictionary<PokemonElementType, IReadOnlyList<BoxBackgroundTheme>> Mapping =
        new ReadOnlyDictionary<PokemonElementType, IReadOnlyList<BoxBackgroundTheme>>(
            new Dictionary<PokemonElementType, IReadOnlyList<BoxBackgroundTheme>>
            {
                // Checkered and White are deliberately reserved for Mixed boxes. The
                // remaining primary themes cover all non-neutral backgrounds before
                // repeating a type-appropriate one.
                [PokemonElementType.Normal] = Themes(BoxBackgroundTheme.City, BoxBackgroundTheme.Steppe, BoxBackgroundTheme.Forest),
                [PokemonElementType.Fire] = Themes(BoxBackgroundTheme.Volcano, BoxBackgroundTheme.Desert, BoxBackgroundTheme.Rocky),
                [PokemonElementType.Water] = Themes(BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.River, BoxBackgroundTheme.Beach),
                [PokemonElementType.Electric] = Themes(BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.Metal, BoxBackgroundTheme.City),
                [PokemonElementType.Grass] = Themes(BoxBackgroundTheme.Forest, BoxBackgroundTheme.River, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Ice] = Themes(BoxBackgroundTheme.Snow, BoxBackgroundTheme.Cave, BoxBackgroundTheme.Sky),
                [PokemonElementType.Fighting] = Themes(BoxBackgroundTheme.Steppe, BoxBackgroundTheme.Rocky, BoxBackgroundTheme.City),
                [PokemonElementType.Poison] = Themes(BoxBackgroundTheme.Cave, BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.PokemonCenter),
                [PokemonElementType.Ground] = Themes(BoxBackgroundTheme.Desert, BoxBackgroundTheme.Rocky, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Flying] = Themes(BoxBackgroundTheme.Sky, BoxBackgroundTheme.Beach, BoxBackgroundTheme.River),
                [PokemonElementType.Psychic] = Themes(BoxBackgroundTheme.River, BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.Sky),
                [PokemonElementType.Bug] = Themes(BoxBackgroundTheme.Beach, BoxBackgroundTheme.Forest, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Rock] = Themes(BoxBackgroundTheme.Rocky, BoxBackgroundTheme.Cave, BoxBackgroundTheme.Desert),
                [PokemonElementType.Ghost] = Themes(BoxBackgroundTheme.Cave, BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.Sky),
                [PokemonElementType.Dragon] = Themes(BoxBackgroundTheme.Volcano, BoxBackgroundTheme.Sky, BoxBackgroundTheme.Rocky),
                [PokemonElementType.Dark] = Themes(BoxBackgroundTheme.Metal, BoxBackgroundTheme.City, BoxBackgroundTheme.Cave),
                [PokemonElementType.Steel] = Themes(BoxBackgroundTheme.Metal, BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.City),
                [PokemonElementType.Fairy] = Themes(BoxBackgroundTheme.Forest, BoxBackgroundTheme.Sky, BoxBackgroundTheme.PokemonCenter),
            });

    public static IReadOnlyList<BoxBackgroundTheme> MixedThemes { get; } =
        Themes(BoxBackgroundTheme.Checkered, BoxBackgroundTheme.White);

    public static IReadOnlyList<BoxBackgroundTheme> LegendaryThemes { get; } =
        Themes(BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.Sky, BoxBackgroundTheme.City);

    public static IReadOnlyDictionary<PokemonElementType, IReadOnlyList<BoxBackgroundTheme>> TypeThemes => Mapping;

    private static IReadOnlyList<BoxBackgroundTheme> Themes(params BoxBackgroundTheme[] themes) =>
        Array.AsReadOnly(themes);
}

public static class TypeBoxBackgroundPlanner
{
    public static IReadOnlyList<PlannedBoxBackgroundTheme> Create(
        IReadOnlyList<TypeBoxAssignment> boxes,
        TypeBoxOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.AssignMatchingBackgrounds)
            return [];

        var typeOrdinals = new Dictionary<PokemonElementType, int>();
        var legendaryOrdinal = 0;
        var result = new List<PlannedBoxBackgroundTheme>(boxes.Count);
        foreach (var box in boxes)
        {
            var configured = box.IsLegendary
                ? TypeBoxBackgroundMapping.LegendaryThemes
                : box.IsMixed
                    ? TypeBoxBackgroundMapping.MixedThemes
                    : TypeBoxBackgroundMapping.TypeThemes[box.SharedType!.Value];
            var supported = configured
                .Select((theme, configuredIndex) => (Theme: theme, ConfiguredIndex: configuredIndex))
                .Where(item => options.SupportedBackgroundThemes.Contains(item.Theme))
                .ToArray();

            if (supported.Length == 0)
            {
                var group = box.IsLegendary
                    ? "Legendary"
                    : box.IsMixed ? "Mixed" : box.SharedType!.Value.ToString();
                var warning =
                    $"Box {box.TargetBoxIndex + 1}: No supported matching background was available for {group}. Existing background will be preserved.";
                result.Add(new PlannedBoxBackgroundTheme(
                    box.TargetBoxIndex,
                    box.SharedType,
                    box.IsMixed,
                    null,
                    BackgroundThemeChoice.Preserved,
                    warning));
                continue;
            }

            var ordinal = 0;
            if (!box.IsMixed && !box.IsLegendary)
            {
                var type = box.SharedType!.Value;
                ordinal = typeOrdinals.GetValueOrDefault(type);
                typeOrdinals[type] = ordinal + 1;
            }

            if (box.IsLegendary)
                ordinal = legendaryOrdinal++;
            var supportedIndex = !box.IsMixed && options.RotateAlternativeBackgrounds
                ? ordinal % supported.Length
                : 0;
            var selected = supported[supportedIndex];
            var choice = GetChoice(selected.ConfiguredIndex, supportedIndex, options.RotateAlternativeBackgrounds);
            result.Add(new PlannedBoxBackgroundTheme(
                box.TargetBoxIndex,
                box.SharedType,
                box.IsMixed,
                selected.Theme,
                choice,
                null));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private static BackgroundThemeChoice GetChoice(
        int configuredIndex,
        int supportedIndex,
        bool rotating)
    {
        if (configuredIndex == 0)
            return BackgroundThemeChoice.Primary;
        if (rotating && supportedIndex != 0)
            return BackgroundThemeChoice.Alternative;
        return BackgroundThemeChoice.Fallback;
    }
}
