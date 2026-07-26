namespace OrganizerMod.Domain;

public enum CompetitiveOrganizationMode
{
    ProgressGroups,
    LevelBands,
    ExperienceOrder,
}

public enum CompetitiveWithinGroupSort
{
    LevelDescending,
    ExperienceDescending,
    NationalDex,
    SpeciesName,
}

public sealed record CompetitiveOrganizerOptions
{
    public CompetitiveOrganizerOptions(
        CompetitiveOrganizationMode mode,
        int battleReadyLevel = 50,
        int minimumEvTotal = 508,
        int highLevelThreshold = 50,
        int trainingThreshold = 20,
        int endgameThreshold = 80,
        bool requireLegal = false,
        bool requireAllMoves = false,
        CompetitiveWithinGroupSort withinGroupSort = CompetitiveWithinGroupSort.LevelDescending,
        bool renameBoxes = false,
        bool assignMatchingBackgrounds = false,
        int maximumBoxNameLength = 16)
    {
        Mode = mode;
        BattleReadyLevel = battleReadyLevel;
        MinimumEvTotal = minimumEvTotal;
        HighLevelThreshold = highLevelThreshold;
        TrainingThreshold = trainingThreshold;
        EndgameThreshold = endgameThreshold;
        RequireLegal = requireLegal;
        RequireAllMoves = requireAllMoves;
        WithinGroupSort = withinGroupSort;
        RenameBoxes = renameBoxes;
        AssignMatchingBackgrounds = assignMatchingBackgrounds;
        MaximumBoxNameLength = maximumBoxNameLength;
    }

    public CompetitiveOrganizationMode Mode { get; }
    public int BattleReadyLevel { get; }
    public int MinimumEvTotal { get; }
    public int HighLevelThreshold { get; }
    public int TrainingThreshold { get; }
    public int EndgameThreshold { get; }
    public bool RequireLegal { get; }
    public bool RequireAllMoves { get; }
    public CompetitiveWithinGroupSort WithinGroupSort { get; }
    public bool RenameBoxes { get; }
    public bool AssignMatchingBackgrounds { get; }
    public int MaximumBoxNameLength { get; }
}

public sealed class CompetitiveOrganizationPlanner
{
    public GroupedOrganizationPlan CreatePlan(
        IReadOnlyList<GroupedPokemon> pokemon,
        IReadOnlyList<BoxState> boxes,
        CompetitiveOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);
        var validation = Validate(options);
        if (validation.Count != 0)
        {
            return new GroupedOrganizationPlan(
                "Competitive / Progress Organizer", options.Mode.ToString(), [], [], [], [], [], [], validation,
                new GroupedOrganizationSummary(pokemon.Count, 0, 0, boxes.Count, 0,
                    pokemon.Count(item => item.IsEgg), pokemon.Count(item => !item.IsValid)));
        }

        var groups = options.Mode switch
        {
            CompetitiveOrganizationMode.ProgressGroups => BuildProgressGroups(pokemon, options),
            CompetitiveOrganizationMode.LevelBands => BuildLevelBands(pokemon, options),
            CompetitiveOrganizationMode.ExperienceOrder => BuildExperienceGroups(pokemon, options.AssignMatchingBackgrounds),
            _ => throw new ArgumentOutOfRangeException(nameof(options)),
        };
        var rules = DescribeRules(options);
        return GroupedLayoutBuilder.Build(
            "Competitive / Progress Organizer",
            GetModeName(options.Mode),
            groups,
            boxes,
            startEachGroupInNewBox: options.Mode != CompetitiveOrganizationMode.ExperienceOrder,
            options.RenameBoxes,
            options.MaximumBoxNameLength,
            rules);
    }

    private static IReadOnlyList<OrganizationGroup> BuildProgressGroups(
        IReadOnlyList<GroupedPokemon> pokemon,
        CompetitiveOrganizerOptions options)
    {
        string GetGroup(GroupedPokemon item)
        {
            if (item.IsEgg)
                return "eggs";
            if (!item.IsValid || item.Level is < 1 or > 100)
                return "invalid";
            if (item.Level >= options.BattleReadyLevel &&
                item.EVTotal >= options.MinimumEvTotal &&
                (!options.RequireLegal || item.IsLegal) &&
                (!options.RequireAllMoves || item.HasAllMoves))
                return "battle";
            if (item.Level >= options.HighLevelThreshold)
                return "high";
            return item.Level >= options.TrainingThreshold ? "training" : "low";
        }

        var definitions = new[]
        {
            (Id: "battle", Name: "Battle Ready", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.Metal),
            (Id: "high", Name: "High Level", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.Volcano),
            (Id: "training", Name: "In Training", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.Steppe),
            (Id: "low", Name: "Low Level", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.Forest),
            (Id: "eggs", Name: "Eggs", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.PokemonCenter),
            (Id: "invalid", Name: "Invalid", Theme: (BoxBackgroundTheme?)BoxBackgroundTheme.Checkered),
        };
        var lookup = pokemon.GroupBy(GetGroup).ToDictionary(group => group.Key, group => group.ToArray());
        return definitions
            .Where(definition => lookup.ContainsKey(definition.Id))
            .Select(definition => new OrganizationGroup(
                definition.Id,
                definition.Name,
                Sort(lookup[definition.Id], options.WithinGroupSort),
                options.AssignMatchingBackgrounds ? definition.Theme : null))
            .ToArray();
    }

    private static IReadOnlyList<OrganizationGroup> BuildLevelBands(
        IReadOnlyList<GroupedPokemon> pokemon,
        CompetitiveOrganizerOptions options)
    {
        var bands = new[]
        {
            (Id: "band1", Min: 1, Max: options.TrainingThreshold - 1, Theme: BoxBackgroundTheme.Forest),
            (Id: "band2", Min: options.TrainingThreshold, Max: options.HighLevelThreshold - 1, Theme: BoxBackgroundTheme.Steppe),
            (Id: "band3", Min: options.HighLevelThreshold, Max: options.EndgameThreshold - 1, Theme: BoxBackgroundTheme.Volcano),
            (Id: "band4", Min: options.EndgameThreshold, Max: 100, Theme: BoxBackgroundTheme.Metal),
        };
        var result = new List<OrganizationGroup>();
        foreach (var band in bands)
        {
            var items = pokemon.Where(item => item.IsValid && !item.IsEgg && item.Level >= band.Min && item.Level <= band.Max).ToArray();
            if (items.Length != 0)
            {
                result.Add(new OrganizationGroup(
                    band.Id,
                    $"Lv {band.Min}-{band.Max}",
                    Sort(items, CompetitiveWithinGroupSort.LevelDescending),
                    options.AssignMatchingBackgrounds ? band.Theme : null));
            }
        }
        AddSpecialGroups(result, pokemon, options.AssignMatchingBackgrounds);
        return result;
    }

    private static IReadOnlyList<OrganizationGroup> BuildExperienceGroups(
        IReadOnlyList<GroupedPokemon> pokemon,
        bool assignBackgrounds)
    {
        var valid = pokemon.Where(item => item.IsValid && !item.IsEgg && item.Level is >= 1 and <= 100)
            .OrderByDescending(item => item.Experience)
            .ThenByDescending(item => item.Level)
            .ThenBy(item => item.Species)
            .ThenBy(item => item.Form)
            .ThenBy(item => item.Reference.SourceBoxIndex)
            .ThenBy(item => item.Reference.SourceSlotIndex)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToArray();
        var result = new List<OrganizationGroup>();
        if (valid.Length != 0)
            result.Add(new OrganizationGroup("experience", "Experience", valid, assignBackgrounds ? BoxBackgroundTheme.City : null));
        AddSpecialGroups(result, pokemon, assignBackgrounds);
        return result;
    }

    private static void AddSpecialGroups(
        ICollection<OrganizationGroup> result,
        IReadOnlyList<GroupedPokemon> pokemon,
        bool assignBackgrounds)
    {
        var eggs = pokemon.Where(item => item.IsEgg).OrderBy(item => item, OriginalComparer.Instance).ToArray();
        if (eggs.Length != 0)
            result.Add(new OrganizationGroup("eggs", "Eggs", eggs, assignBackgrounds ? BoxBackgroundTheme.PokemonCenter : null));
        var invalid = pokemon.Where(item => !item.IsEgg && (!item.IsValid || item.Level is < 1 or > 100)).OrderBy(item => item, OriginalComparer.Instance).ToArray();
        if (invalid.Length != 0)
            result.Add(new OrganizationGroup("invalid", "Invalid", invalid, assignBackgrounds ? BoxBackgroundTheme.Checkered : null));
    }

    private static IReadOnlyList<GroupedPokemon> Sort(
        IEnumerable<GroupedPokemon> pokemon,
        CompetitiveWithinGroupSort sort)
    {
        IOrderedEnumerable<GroupedPokemon> ordered = sort switch
        {
            CompetitiveWithinGroupSort.LevelDescending => pokemon.OrderByDescending(item => item.Level)
                .ThenByDescending(item => item.Experience).ThenBy(item => item.Species),
            CompetitiveWithinGroupSort.ExperienceDescending => pokemon.OrderByDescending(item => item.Experience)
                .ThenByDescending(item => item.Level).ThenBy(item => item.Species),
            CompetitiveWithinGroupSort.NationalDex => pokemon.OrderBy(item => item.Species)
                .ThenBy(item => item.Form).ThenByDescending(item => item.Level),
            CompetitiveWithinGroupSort.SpeciesName => pokemon.OrderBy(item => item.SpeciesName, StringComparer.Ordinal)
                .ThenBy(item => item.Species).ThenBy(item => item.Form),
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };
        return ordered.ThenBy(item => item.Form)
            .ThenBy(item => item.IsShiny)
            .ThenBy(item => item.Reference.SourceBoxIndex)
            .ThenBy(item => item.Reference.SourceSlotIndex)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> Validate(CompetitiveOrganizerOptions options)
    {
        var errors = new List<string>();
        if (options.BattleReadyLevel is < 1 or > 100)
            errors.Add("Battle-ready level must be from 1 through 100.");
        if (options.MinimumEvTotal is < 0 or > 1530)
            errors.Add("Minimum EV total must be from 0 through 1530.");
        if (options.TrainingThreshold is < 1 or > 100 || options.HighLevelThreshold is < 1 or > 100 ||
            (options.Mode == CompetitiveOrganizationMode.LevelBands && options.EndgameThreshold is < 1 or > 100))
            errors.Add("Level boundaries must be from 1 through 100.");
        if (options.TrainingThreshold >= options.HighLevelThreshold ||
            (options.Mode == CompetitiveOrganizationMode.LevelBands && options.HighLevelThreshold >= options.EndgameThreshold))
            errors.Add(options.Mode == CompetitiveOrganizationMode.LevelBands
                ? "Level boundaries must be strictly increasing: training < high level < endgame."
                : "Training threshold must be lower than the high-level threshold.");
        if (options.MaximumBoxNameLength <= 0)
            errors.Add("Maximum box-name length must be positive.");
        return errors;
    }

    private static IReadOnlyList<string> DescribeRules(CompetitiveOrganizerOptions options) =>
        options.Mode switch
        {
            CompetitiveOrganizationMode.ProgressGroups =>
            [
                $"Battle Ready: level ≥ {options.BattleReadyLevel}, EV total ≥ {options.MinimumEvTotal}" +
                (options.RequireLegal ? ", legal required" : string.Empty) +
                (options.RequireAllMoves ? ", four non-empty moves required" : string.Empty),
                $"High Level: level ≥ {options.HighLevelThreshold}",
                $"In Training: level ≥ {options.TrainingThreshold}",
                $"Within groups: {options.WithinGroupSort}",
            ],
            CompetitiveOrganizationMode.LevelBands =>
            [$"Boundaries: {options.TrainingThreshold}, {options.HighLevelThreshold}, {options.EndgameThreshold}"],
            CompetitiveOrganizationMode.ExperienceOrder =>
            ["Experience descending, then level, National Dex, form, and original position; eggs last."],
            _ => [],
        };

    private static string GetModeName(CompetitiveOrganizationMode mode) => mode switch
    {
        CompetitiveOrganizationMode.ProgressGroups => "Progress Groups",
        CompetitiveOrganizationMode.LevelBands => "Level Bands",
        CompetitiveOrganizationMode.ExperienceOrder => "Experience Order",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private sealed class OriginalComparer : IComparer<GroupedPokemon>
    {
        public static OriginalComparer Instance { get; } = new();
        public int Compare(GroupedPokemon? x, GroupedPokemon? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var result = x.Reference.SourceBoxIndex.CompareTo(y.Reference.SourceBoxIndex);
            if (result != 0) return result;
            result = x.Reference.SourceSlotIndex.CompareTo(y.Reference.SourceSlotIndex);
            return result != 0 ? result : StringComparer.Ordinal.Compare(x.Reference.StableId, y.Reference.StableId);
        }
    }
}
