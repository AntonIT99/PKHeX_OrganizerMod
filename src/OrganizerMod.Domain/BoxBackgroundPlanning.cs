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
                [PokemonElementType.Normal] = Themes(BoxBackgroundTheme.Checkered, BoxBackgroundTheme.White, BoxBackgroundTheme.City),
                [PokemonElementType.Fire] = Themes(BoxBackgroundTheme.Volcano, BoxBackgroundTheme.Steppe, BoxBackgroundTheme.Desert),
                [PokemonElementType.Water] = Themes(BoxBackgroundTheme.DeepSea, BoxBackgroundTheme.River, BoxBackgroundTheme.Beach),
                [PokemonElementType.Electric] = Themes(BoxBackgroundTheme.City, BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.Metal),
                [PokemonElementType.Grass] = Themes(BoxBackgroundTheme.Forest, BoxBackgroundTheme.River, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Ice] = Themes(BoxBackgroundTheme.Snow, BoxBackgroundTheme.White, BoxBackgroundTheme.Cave),
                [PokemonElementType.Fighting] = Themes(BoxBackgroundTheme.Steppe, BoxBackgroundTheme.Rocky, BoxBackgroundTheme.City),
                [PokemonElementType.Poison] = Themes(BoxBackgroundTheme.Cave, BoxBackgroundTheme.City, BoxBackgroundTheme.DeepSea),
                [PokemonElementType.Ground] = Themes(BoxBackgroundTheme.Desert, BoxBackgroundTheme.Rocky, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Flying] = Themes(BoxBackgroundTheme.Sky, BoxBackgroundTheme.Beach, BoxBackgroundTheme.Steppe),
                [PokemonElementType.Psychic] = Themes(BoxBackgroundTheme.PokemonCenter, BoxBackgroundTheme.Sky, BoxBackgroundTheme.White),
                [PokemonElementType.Bug] = Themes(BoxBackgroundTheme.Forest, BoxBackgroundTheme.Steppe, BoxBackgroundTheme.River),
                [PokemonElementType.Rock] = Themes(BoxBackgroundTheme.Rocky, BoxBackgroundTheme.Cave, BoxBackgroundTheme.Desert),
                [PokemonElementType.Ghost] = Themes(BoxBackgroundTheme.Cave, BoxBackgroundTheme.White, BoxBackgroundTheme.PokemonCenter),
                [PokemonElementType.Dragon] = Themes(BoxBackgroundTheme.Volcano, BoxBackgroundTheme.Sky, BoxBackgroundTheme.Cave),
                [PokemonElementType.Dark] = Themes(BoxBackgroundTheme.Cave, BoxBackgroundTheme.City, BoxBackgroundTheme.Metal),
                [PokemonElementType.Steel] = Themes(BoxBackgroundTheme.Metal, BoxBackgroundTheme.City, BoxBackgroundTheme.PokemonCenter),
                [PokemonElementType.Fairy] = Themes(BoxBackgroundTheme.White, BoxBackgroundTheme.Forest, BoxBackgroundTheme.Sky),
            });

    public static IReadOnlyList<BoxBackgroundTheme> MixedThemes { get; } =
        Themes(BoxBackgroundTheme.Checkered, BoxBackgroundTheme.White);

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
        var result = new List<PlannedBoxBackgroundTheme>(boxes.Count);
        foreach (var box in boxes)
        {
            var configured = box.IsMixed
                ? TypeBoxBackgroundMapping.MixedThemes
                : TypeBoxBackgroundMapping.TypeThemes[box.SharedType!.Value];
            var supported = configured
                .Select((theme, configuredIndex) => (Theme: theme, ConfiguredIndex: configuredIndex))
                .Where(item => options.SupportedBackgroundThemes.Contains(item.Theme))
                .ToArray();

            if (supported.Length == 0)
            {
                var group = box.IsMixed ? "Mixed" : box.SharedType!.Value.ToString();
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
            if (!box.IsMixed)
            {
                var type = box.SharedType!.Value;
                ordinal = typeOrdinals.GetValueOrDefault(type);
                typeOrdinals[type] = ordinal + 1;
            }

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
