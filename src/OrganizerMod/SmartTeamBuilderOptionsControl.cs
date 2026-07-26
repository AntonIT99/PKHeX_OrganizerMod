using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class SmartTeamBuilderOptionsControl : UserControl
{
    private readonly NumericUpDown teamSize = new() { Minimum = 1, Maximum = 6, Value = 6, Width = 65 };
    private readonly CheckBox allowSmaller = new() { AutoSize = true, Text = "Allow a smaller Team when too few Pokémon are eligible" };
    private readonly CheckBox allowEggs = new() { AutoSize = true, Text = "Allow eggs in the generated Team" };
    private readonly CheckBox requireTypes = new() { AutoSize = true, Text = "Require type(s)" };
    private readonly ComboBox typeMode = Combo("Has any selected type", "Has all selected types", "Exact type combination");
    private readonly ComboBox requiredType1 = TypeCombo(false);
    private readonly ComboBox requiredType2 = TypeCombo(true);
    private readonly CheckBox requireOrigin = new() { AutoSize = true, Text = "Require origin game" };
    private readonly ComboBox requiredOrigin;
    private readonly CheckBox requireGeneration = new() { AutoSize = true, Text = "Require Pokémon generation" };
    private readonly ComboBox requiredGeneration = GenerationCombo();
    private readonly CheckBox legendaryOnly = new() { AutoSize = true, Text = "Legendary or Mythical Pokémon only" };
    private readonly CheckBox shinyOnly = new() { AutoSize = true, Text = "Shiny Pokémon only" };
    private readonly CheckBox preferDifferentSpecies = new() { AutoSize = true, Checked = true, Text = "Prefer different species" };
    private readonly ComboBox teamOrder = Combo("Preference order", "Preserve current Team order where possible", "Level descending");
    private readonly TableLayoutPanel preferenceRows;
    private readonly IReadOnlyList<OriginGameChoice> originGames;
    private List<PreferenceRow> preferences;

    public SmartTeamBuilderOptionsControl(IReadOnlyList<OriginGameChoice> originGames)
    {
        this.originGames = originGames;
        requiredOrigin = OriginCombo(originGames);
        AutoSize = true;
        Dock = DockStyle.Top;
        preferences =
        [
            new(TeamPreferenceCriterionType.HighestLevelAndExperience, true),
            new(TeamPreferenceCriterionType.PreferredTypes, false),
            new(TeamPreferenceCriterionType.PreferredOriginGame, false),
            new(TeamPreferenceCriterionType.PreferredSpeciesGeneration, false),
            new(TeamPreferenceCriterionType.PreferLegendaryOrMythical, false),
            new(TeamPreferenceCriterionType.PreferShiny, false),
        ];
        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Margin = new Padding(0), RowCount = 12 };
        root.Controls.Add(Row(new Label { AutoSize = true, Text = "Team size:" }, teamSize, allowSmaller), 0, 0);
        root.Controls.Add(Heading("Eligibility rules — every enabled rule must match"), 0, 1);
        root.Controls.Add(Row(requireTypes, typeMode, requiredType1, requiredType2), 0, 2);
        root.Controls.Add(Row(requireOrigin, requiredOrigin), 0, 3);
        root.Controls.Add(Row(requireGeneration, requiredGeneration), 0, 4);
        root.Controls.Add(Row(legendaryOnly, shinyOnly, allowEggs), 0, 5);
        root.Controls.Add(Description("Exact type combination compares the complete normalized one- or two-type set; type order does not matter."), 0, 6);
        root.Controls.Add(Heading("Preferences — highest priority first"), 0, 7);
        preferenceRows = RuleTable();
        root.Controls.Add(preferenceRows, 0, 8);
        root.Controls.Add(preferDifferentSpecies, 0, 9);
        root.Controls.Add(Row(new Label { AutoSize = true, Text = "Team order:" }, teamOrder), 0, 10);
        root.Controls.Add(Description("If no preference is enabled, ties favor the current Team, then the earliest original location."), 0, 11);
        Controls.Add(root);
        BindEligibility();
        RebuildPreferences();
    }

    public void SetMaximumTeamSize(int maximum)
    {
        maximum = Math.Clamp(maximum, 1, 6);
        teamSize.Maximum = maximum;
        if (teamSize.Value > maximum) teamSize.Value = maximum;
    }

    public TeamBuilderOptions GetOptions(IReadOnlySet<int> selectedBoxes, int maximumTeamSize)
    {
        var eligibility = new TeamEligibilityRule[]
        {
            new(TeamEligibilityRuleType.RequiredTypes, requireTypes.Checked, SelectedTypes(requiredType1, requiredType2), (TeamTypeMatchingMode)typeMode.SelectedIndex),
            new(TeamEligibilityRuleType.RequiredOriginGame, requireOrigin.Checked, OriginGame: (requiredOrigin.SelectedItem as OriginGameChoice)?.Id),
            new(TeamEligibilityRuleType.RequiredSpeciesGeneration, requireGeneration.Checked, SpeciesGeneration: requiredGeneration.SelectedIndex + 1),
            new(TeamEligibilityRuleType.LegendaryOrMythicalOnly, legendaryOnly.Checked),
            new(TeamEligibilityRuleType.ShinyOnly, shinyOnly.Checked),
        };
        return new TeamBuilderOptions((int)teamSize.Value, maximumTeamSize, eligibility,
            preferences.Select(x => x.ToCriterion()).ToArray(), preferDifferentSpecies.Checked,
            (TeamPartyOrder)teamOrder.SelectedIndex, selectedBoxes, allowEggs.Checked, allowSmaller.Checked);
    }

    private void BindEligibility()
    {
        typeMode.SelectedIndex = 0; requiredType1.SelectedIndex = 0; requiredType2.SelectedIndex = 0;
        requiredGeneration.SelectedIndex = 0; teamOrder.SelectedIndex = 0;
        void updateTypes() { typeMode.Enabled = requiredType1.Enabled = requiredType2.Enabled = requireTypes.Checked; }
        void updateOrigin() => requiredOrigin.Enabled = requireOrigin.Checked;
        void updateGeneration() => requiredGeneration.Enabled = requireGeneration.Checked;
        requireTypes.CheckedChanged += (_, _) => updateTypes();
        requireOrigin.CheckedChanged += (_, _) => updateOrigin();
        requireGeneration.CheckedChanged += (_, _) => updateGeneration();
        updateTypes(); updateOrigin(); updateGeneration();
    }

    private void RebuildPreferences()
    {
        preferenceRows.SuspendLayout();
        preferenceRows.Controls.Clear();
        preferenceRows.RowCount = preferences.Count;
        for (var index = 0; index < preferences.Count; index++)
        {
            var position = index;
            var model = preferences[index];
            var enabled = new CheckBox { AutoSize = true, Checked = model.Enabled };
            var name = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = GetCriterionName(model.Type) };
            var value = CreatePreferenceValue(model);
            value.Enabled = model.Enabled && NeedsValue(model.Type);
            enabled.CheckedChanged += (_, _) => { model.Enabled = enabled.Checked; value.Enabled = model.Enabled && NeedsValue(model.Type); };
            var up = new Button { AutoSize = true, Enabled = index > 0, Text = "↑", AccessibleName = "Move preference up" };
            var down = new Button { AutoSize = true, Enabled = index < preferences.Count - 1, Text = "↓", AccessibleName = "Move preference down" };
            up.Click += (_, _) => MovePreference(position, -1); down.Click += (_, _) => MovePreference(position, 1);
            preferenceRows.Controls.Add(enabled, 0, index);
            preferenceRows.Controls.Add(name, 1, index);
            preferenceRows.Controls.Add(value, 2, index);
            preferenceRows.Controls.Add(up, 3, index);
            preferenceRows.Controls.Add(down, 4, index);
        }
        preferenceRows.ResumeLayout(true);
    }

    private Control CreatePreferenceValue(PreferenceRow model)
    {
        switch (model.Type)
        {
            case TeamPreferenceCriterionType.PreferredTypes:
                var types = Row(TypeCombo(false), TypeCombo(true));
                var first = (ComboBox)types.Controls[0]; var second = (ComboBox)types.Controls[1];
                first.SelectedIndex = model.Type1Index; second.SelectedIndex = model.Type2Index;
                first.SelectedIndexChanged += (_, _) => model.Type1Index = first.SelectedIndex;
                second.SelectedIndexChanged += (_, _) => model.Type2Index = second.SelectedIndex;
                return types;
            case TeamPreferenceCriterionType.PreferredOriginGame:
                var origin = OriginCombo(originGames);
                origin.SelectedItem = originGames.FirstOrDefault(x => x.Id == model.OriginGame) ?? originGames.FirstOrDefault();
                origin.SelectedIndexChanged += (_, _) => model.OriginGame = (origin.SelectedItem as OriginGameChoice)?.Id;
                model.OriginGame = (origin.SelectedItem as OriginGameChoice)?.Id;
                return origin;
            case TeamPreferenceCriterionType.PreferredSpeciesGeneration:
                var generation = GenerationCombo(); generation.SelectedIndex = model.Generation - 1;
                generation.SelectedIndexChanged += (_, _) => model.Generation = generation.SelectedIndex + 1;
                return generation;
            default:
                return new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = "No value required" };
        }
    }

    private void MovePreference(int index, int offset)
    {
        var target = index + offset;
        if ((uint)target >= preferences.Count) return;
        (preferences[index], preferences[target]) = (preferences[target], preferences[index]);
        RebuildPreferences();
    }

    private static IReadOnlyList<PokemonElementType> SelectedTypes(ComboBox first, ComboBox second)
    {
        var result = new List<PokemonElementType> { (PokemonElementType)first.SelectedIndex };
        if (second.SelectedIndex > 0) result.Add((PokemonElementType)(second.SelectedIndex - 1));
        return result;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(controls); return row;
    }
    private static TableLayoutPanel RuleTable()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 5, Dock = DockStyle.Top, Margin = new Padding(0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }
    private static ComboBox Combo(params string[] values)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        combo.Items.AddRange(values); return combo;
    }
    private static ComboBox TypeCombo(bool optional)
    {
        var combo = Combo();
        if (optional) combo.Items.Add("(no second type)");
        combo.Items.AddRange(Enum.GetNames<PokemonElementType>());
        combo.SelectedIndex = 0; return combo;
    }
    private static ComboBox GenerationCombo()
    {
        var combo = Combo(Enumerable.Range(1, 9).Select(x => $"Generation {x}").ToArray());
        combo.SelectedIndex = 0; return combo;
    }
    private static ComboBox OriginCombo(IReadOnlyList<OriginGameChoice> games)
    {
        var combo = Combo(); combo.Items.AddRange(games.Cast<object>().ToArray());
        if (combo.Items.Count != 0) combo.SelectedIndex = 0; return combo;
    }
    private static bool NeedsValue(TeamPreferenceCriterionType type) => type is TeamPreferenceCriterionType.PreferredTypes or TeamPreferenceCriterionType.PreferredOriginGame or TeamPreferenceCriterionType.PreferredSpeciesGeneration;
    private static string GetCriterionName(TeamPreferenceCriterionType type) => type switch
    {
        TeamPreferenceCriterionType.HighestLevelAndExperience => "Highest level and experience",
        TeamPreferenceCriterionType.PreferredTypes => "Prefer type(s)",
        TeamPreferenceCriterionType.PreferredOriginGame => "Prefer origin game",
        TeamPreferenceCriterionType.PreferredSpeciesGeneration => "Prefer species generation",
        TeamPreferenceCriterionType.PreferLegendaryOrMythical => "Prefer Legendary or Mythical",
        TeamPreferenceCriterionType.PreferShiny => "Prefer shiny Pokémon",
        _ => type.ToString(),
    };
    private static Label Heading(string text) => new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 8, 0, 3), Text = text };
    private static Label Description(string text) => new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(800, 0), Text = text };

    private sealed class PreferenceRow(TeamPreferenceCriterionType type, bool enabled)
    {
        public TeamPreferenceCriterionType Type { get; } = type;
        public bool Enabled { get; set; } = enabled;
        public int Type1Index { get; set; }
        public int Type2Index { get; set; }
        public int? OriginGame { get; set; }
        public int Generation { get; set; } = 1;
        public TeamPreferenceCriterion ToCriterion()
        {
            IReadOnlyList<PokemonElementType>? types = null;
            if (Type == TeamPreferenceCriterionType.PreferredTypes)
            {
                var values = new List<PokemonElementType> { (PokemonElementType)Type1Index };
                if (Type2Index > 0) values.Add((PokemonElementType)(Type2Index - 1));
                types = values;
            }
            return new(Type, Enabled, types, OriginGame, Generation);
        }
    }
}
