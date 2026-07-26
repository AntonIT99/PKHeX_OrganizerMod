namespace OrganizerMod.Domain;

public enum CustomGroupCriterionType
{
    ShinyStatus,
    OriginGame,
    PrimaryType,
    LevelBand,
}

public enum CustomSortCriterionType
{
    NationalDex,
    Level,
    Experience,
    ShinyStatus,
    OriginGame,
    Gender,
}

public enum OrganizerSortDirection
{
    Ascending,
    Descending,
}

public sealed record CustomGroupRule(
    CustomGroupCriterionType Type,
    bool Enabled,
    int Priority,
    bool ShinyFirst = false);

public sealed record CustomSortRule(
    CustomSortCriterionType Type,
    bool Enabled,
    OrganizerSortDirection Direction,
    int Priority);

public sealed record CustomOrganizerOptions(
    IReadOnlyList<CustomGroupRule> GroupRules,
    IReadOnlyList<CustomSortRule> SortRules,
    int TrainingStart = 20,
    int HighLevelStart = 50,
    int EndgameStart = 80,
    bool StartEachGroupInNewBox = true,
    bool RenameBoxes = false,
    bool AssignMatchingBackgrounds = false,
    int MaximumBoxNameLength = 16);

public sealed class CustomOrganizationPlanner
{
    private static readonly PokemonElementType[] TypeOrder =
    [
        PokemonElementType.Normal, PokemonElementType.Fire, PokemonElementType.Water,
        PokemonElementType.Electric, PokemonElementType.Grass, PokemonElementType.Ice,
        PokemonElementType.Fighting, PokemonElementType.Poison, PokemonElementType.Ground,
        PokemonElementType.Flying, PokemonElementType.Psychic, PokemonElementType.Bug,
        PokemonElementType.Rock, PokemonElementType.Ghost, PokemonElementType.Dragon,
        PokemonElementType.Dark, PokemonElementType.Steel, PokemonElementType.Fairy,
    ];
    private static readonly IReadOnlyDictionary<PokemonElementType, int> TypeRanks =
        TypeOrder.Select((type, index) => (type, index)).ToDictionary(pair => pair.type, pair => pair.index);

    public GroupedOrganizationPlan CreatePlan(
        IReadOnlyList<GroupedPokemon> pokemon,
        IReadOnlyList<BoxState> boxes,
        CustomOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);
        var errors = Validate(options);
        if (errors.Count != 0)
        {
            return new GroupedOrganizationPlan(
                "Custom Rule-Based Organizer", "Custom rules", [], [], [], [], [], [], errors,
                new GroupedOrganizationSummary(pokemon.Count, 0, 0, boxes.Count, 0,
                    pokemon.Count(item => item.IsEgg), pokemon.Count(item => !item.IsValid)));
        }

        var groupRules = options.GroupRules.Where(rule => rule.Enabled).OrderBy(rule => rule.Priority).ToArray();
        var sortRules = options.SortRules.Where(rule => rule.Enabled).OrderBy(rule => rule.Priority).ToArray();
        var original = pokemon.OrderBy(item => item.Reference.SourceBoxIndex)
            .ThenBy(item => item.Reference.SourceSlotIndex)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToArray();
        var grouped = original
            .GroupBy(item => CreateKey(item, groupRules, options), GroupKeyComparer.Instance)
            .OrderBy(group => group.Key, GroupKeyComparer.Instance)
            .Select(group =>
            {
                var sorted = sortRules.Length == 0
                    ? group.ToArray()
                    : group.OrderBy(item => item, new CustomPokemonComparer(sortRules)).ToArray();
                return new OrganizationGroup(
                    group.Key.Id,
                    group.Key.DisplayName,
                    sorted,
                    options.AssignMatchingBackgrounds ? group.Key.BackgroundTheme : null);
            })
            .ToArray();

        var activeRules = groupRules.Select((rule, index) => $"Group {index + 1}: {Describe(rule)}")
            .Concat(sortRules.Select((rule, index) => $"Sort {index + 1}: {rule.Type} — {rule.Direction}"))
            .ToArray();
        return GroupedLayoutBuilder.Build(
            "Custom Rule-Based Organizer",
            $"Start each group in a new box: {(options.StartEachGroupInNewBox ? "Yes" : "No")}",
            grouped,
            boxes,
            options.StartEachGroupInNewBox,
            options.RenameBoxes,
            options.MaximumBoxNameLength,
            activeRules);
    }

    private static GroupKey CreateKey(
        GroupedPokemon pokemon,
        IReadOnlyList<CustomGroupRule> rules,
        CustomOrganizerOptions options)
    {
        if (rules.Count == 0)
            return new GroupKey("all", "Custom", [], null);
        var parts = rules.Select(rule => CreatePart(pokemon, rule, options)).ToArray();
        var theme = parts.Select(part => part.Theme).FirstOrDefault(value => value is not null);
        return new GroupKey(
            string.Join("|", parts.Select(part => part.Id)),
            string.Join(" ", parts.Select(part => part.DisplayName)),
            parts.Select(part => part.SortValue).ToArray(),
            theme);
    }

    private static GroupPart CreatePart(
        GroupedPokemon pokemon,
        CustomGroupRule rule,
        CustomOrganizerOptions options) => rule.Type switch
    {
        CustomGroupCriterionType.ShinyStatus => new GroupPart(
            $"shiny:{pokemon.IsShiny}",
            pokemon.IsShiny ? "Shiny" : "Non-Shiny",
            rule.ShinyFirst ? (pokemon.IsShiny ? 0 : 1) : (pokemon.IsShiny ? 1 : 0),
            pokemon.IsShiny ? BoxBackgroundTheme.White : BoxBackgroundTheme.Checkered),
        CustomGroupCriterionType.OriginGame => new GroupPart(
            $"origin:{pokemon.OriginGame}",
            pokemon.OriginGame > 0 && pokemon.OriginGameName.Length != 0 ? pokemon.OriginGameName : "Unknown Origin",
            pokemon.OriginGame > 0 ? pokemon.OriginGame : int.MaxValue,
            null),
        CustomGroupCriterionType.PrimaryType => new GroupPart(
            $"type:{(int?)pokemon.PrimaryType ?? -1}",
            pokemon.PrimaryType is null ? "Unknown Type" : pokemon.PrimaryTypeName,
            pokemon.PrimaryType is { } type ? TypeRanks[type] : int.MaxValue,
            pokemon.PrimaryType is { } mapped ? TypeBoxBackgroundMapping.TypeThemes[mapped][0] : null),
        CustomGroupCriterionType.LevelBand => CreateLevelBandPart(pokemon.Level, options),
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };

    private static GroupPart CreateLevelBandPart(int level, CustomOrganizerOptions options)
    {
        if (level < options.TrainingStart)
            return new GroupPart("level:0", $"Lv 1-{options.TrainingStart - 1}", 0, BoxBackgroundTheme.Forest);
        if (level < options.HighLevelStart)
            return new GroupPart("level:1", $"Lv {options.TrainingStart}-{options.HighLevelStart - 1}", 1, BoxBackgroundTheme.Steppe);
        if (level < options.EndgameStart)
            return new GroupPart("level:2", $"Lv {options.HighLevelStart}-{options.EndgameStart - 1}", 2, BoxBackgroundTheme.Volcano);
        return new GroupPart("level:3", $"Lv {options.EndgameStart}-100", 3, BoxBackgroundTheme.Metal);
    }

    private static List<string> Validate(CustomOrganizerOptions options)
    {
        var errors = new List<string>();
        var groups = options.GroupRules.Where(rule => rule.Enabled).ToArray();
        var sorts = options.SortRules.Where(rule => rule.Enabled).ToArray();
        if (groups.Length > 2)
            errors.Add("At most two grouping rules may be enabled.");
        if (sorts.Length > 4)
            errors.Add("At most four sorting rules may be enabled.");
        if (groups.Select(rule => rule.Type).Distinct().Count() != groups.Length)
            errors.Add("Duplicate active grouping criteria are not allowed.");
        if (sorts.Select(rule => rule.Type).Distinct().Count() != sorts.Length)
            errors.Add("Duplicate active sorting criteria are not allowed.");
        if (groups.Select(rule => rule.Priority).Distinct().Count() != groups.Length || groups.Any(rule => rule.Priority < 0))
            errors.Add("Active grouping priorities must be unique and non-negative.");
        if (sorts.Select(rule => rule.Priority).Distinct().Count() != sorts.Length || sorts.Any(rule => rule.Priority < 0))
            errors.Add("Active sorting priorities must be unique and non-negative.");
        if (options.TrainingStart is < 2 or > 98 ||
            options.HighLevelStart <= options.TrainingStart ||
            options.EndgameStart <= options.HighLevelStart ||
            options.EndgameStart > 100)
            errors.Add("Level boundaries must be strictly increasing within 2 through 100.");
        if (options.MaximumBoxNameLength <= 0)
            errors.Add("Maximum box-name length must be positive.");
        return errors;
    }

    private static string Describe(CustomGroupRule rule) => rule.Type == CustomGroupCriterionType.ShinyStatus
        ? $"Shiny status — {(rule.ShinyFirst ? "Shiny first" : "Non-Shiny first")}" : rule.Type.ToString();

    private sealed record GroupPart(string Id, string DisplayName, int SortValue, BoxBackgroundTheme? Theme);
    private sealed record GroupKey(
        string Id,
        string DisplayName,
        IReadOnlyList<int> SortValues,
        BoxBackgroundTheme? BackgroundTheme);

    private sealed class GroupKeyComparer : IEqualityComparer<GroupKey>, IComparer<GroupKey>
    {
        public static GroupKeyComparer Instance { get; } = new();
        public bool Equals(GroupKey? x, GroupKey? y) => x?.Id == y?.Id;
        public int GetHashCode(GroupKey obj) => StringComparer.Ordinal.GetHashCode(obj.Id);
        public int Compare(GroupKey? x, GroupKey? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            for (var index = 0; index < Math.Min(x.SortValues.Count, y.SortValues.Count); index++)
            {
                var result = x.SortValues[index].CompareTo(y.SortValues[index]);
                if (result != 0) return result;
            }
            return StringComparer.Ordinal.Compare(x.Id, y.Id);
        }
    }

    private sealed class CustomPokemonComparer(IReadOnlyList<CustomSortRule> rules) : IComparer<GroupedPokemon>
    {
        public int Compare(GroupedPokemon? x, GroupedPokemon? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            foreach (var rule in rules)
            {
                var result = CompareCriterion(x, y, rule.Type);
                if (rule.Direction == OrganizerSortDirection.Descending)
                    result = -result;
                if (result != 0) return result;
            }
            var fallback = x.Reference.SourceBoxIndex.CompareTo(y.Reference.SourceBoxIndex);
            if (fallback != 0) return fallback;
            fallback = x.Reference.SourceSlotIndex.CompareTo(y.Reference.SourceSlotIndex);
            return fallback != 0 ? fallback : StringComparer.Ordinal.Compare(x.Reference.StableId, y.Reference.StableId);
        }

        private static int CompareCriterion(GroupedPokemon x, GroupedPokemon y, CustomSortCriterionType type) => type switch
        {
            CustomSortCriterionType.NationalDex => ComparePair(x.Species, y.Species, x.Form, y.Form),
            CustomSortCriterionType.Level => ComparePair(x.Level, y.Level, x.Experience, y.Experience),
            CustomSortCriterionType.Experience => ComparePair(x.Experience, y.Experience, x.Level, y.Level),
            CustomSortCriterionType.ShinyStatus => x.IsShiny.CompareTo(y.IsShiny),
            CustomSortCriterionType.OriginGame => x.OriginGame.CompareTo(y.OriginGame),
            CustomSortCriterionType.Gender => GenderRank(x.Gender).CompareTo(GenderRank(y.Gender)),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        private static int ComparePair<TFirst, TSecond>(TFirst ax, TFirst ay, TSecond bx, TSecond by)
            where TFirst : IComparable<TFirst> where TSecond : IComparable<TSecond>
        {
            var result = ax.CompareTo(ay);
            return result != 0 ? result : bx.CompareTo(by);
        }

        private static int GenderRank(int gender) => gender switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
    }
}
