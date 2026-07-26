using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class DuplicateCriteriaEditor : UserControl
{
    private readonly TableLayoutPanel rows;
    private readonly IReadOnlyList<OriginGameChoice> originGames;
    private List<CriterionRow> criteria;

    public DuplicateCriteriaEditor(IReadOnlyList<OriginGameChoice> originGames)
    {
        this.originGames = originGames;
        AutoSize = true;
        Dock = DockStyle.Top;
        rows = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 5,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(rows);
        criteria =
        [
            new(DuplicateSelectionCriterionType.HighestLevel, true),
            new(DuplicateSelectionCriterionType.PreferredOriginGame, false),
            new(DuplicateSelectionCriterionType.PreferredGender, false),
        ];
        Rebuild();
    }

    public IReadOnlyList<DuplicateSelectionCriterion> GetCriteria() =>
        criteria.Select(item => item.ToDefinition()).ToArray();

    private void Rebuild()
    {
        rows.SuspendLayout();
        rows.Controls.Clear();
        rows.RowCount = criteria.Count;
        for (var index = 0; index < criteria.Count; index++)
        {
            var rowIndex = index;
            var model = criteria[index];
            var enabled = new CheckBox
            {
                AutoSize = true,
                Checked = model.Enabled,
                Anchor = AnchorStyles.Left,
            };
            var name = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Text = GetName(model.Type),
            };
            var value = CreateValueControl(model);
            enabled.CheckedChanged += (_, _) =>
            {
                model.Enabled = enabled.Checked;
                value.Enabled = enabled.Checked && model.Type != DuplicateSelectionCriterionType.HighestLevel;
            };
            value.Enabled = model.Enabled && model.Type != DuplicateSelectionCriterionType.HighestLevel;
            var up = new Button { AutoSize = true, Enabled = index > 0, Text = "↑" };
            var down = new Button { AutoSize = true, Enabled = index < criteria.Count - 1, Text = "↓" };
            up.Click += (_, _) => MoveRow(rowIndex, -1);
            down.Click += (_, _) => MoveRow(rowIndex, 1);
            rows.Controls.Add(enabled, 0, index);
            rows.Controls.Add(name, 1, index);
            rows.Controls.Add(value, 2, index);
            rows.Controls.Add(up, 3, index);
            rows.Controls.Add(down, 4, index);
        }
        rows.ResumeLayout(true);
    }

    private Control CreateValueControl(CriterionRow model)
    {
        if (model.Type == DuplicateSelectionCriterionType.HighestLevel)
            return new Label { AutoSize = true, Text = "Highest current level", Anchor = AnchorStyles.Left };
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        if (model.Type == DuplicateSelectionCriterionType.PreferredOriginGame)
        {
            combo.Items.AddRange(originGames.Cast<object>().ToArray());
            model.OriginGameId ??= originGames.FirstOrDefault()?.Id;
            combo.SelectedItem = originGames.FirstOrDefault(item => item.Id == model.OriginGameId);
            if (combo.SelectedIndex < 0 && combo.Items.Count != 0)
                combo.SelectedIndex = 0;
            combo.SelectedIndexChanged += (_, _) =>
                model.OriginGameId = (combo.SelectedItem as OriginGameChoice)?.Id;
        }
        else
        {
            combo.Items.AddRange(["Male", "Female", "Genderless"]);
            combo.SelectedIndex = (int)(model.Gender ?? PokemonGenderPreference.Female);
            combo.SelectedIndexChanged += (_, _) =>
                model.Gender = (PokemonGenderPreference)combo.SelectedIndex;
        }
        return combo;
    }

    private void MoveRow(int index, int offset)
    {
        criteria = DuplicateCriterionList.Move(criteria, index, offset).ToList();
        Rebuild();
    }

    private static string GetName(DuplicateSelectionCriterionType type) =>
        type switch
        {
            DuplicateSelectionCriterionType.HighestLevel => "Highest level",
            DuplicateSelectionCriterionType.PreferredOriginGame => "Preferred origin game",
            DuplicateSelectionCriterionType.PreferredGender => "Preferred gender",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private sealed class CriterionRow(DuplicateSelectionCriterionType type, bool enabled)
    {
        public DuplicateSelectionCriterionType Type { get; } = type;
        public bool Enabled { get; set; } = enabled;
        public int? OriginGameId { get; set; }
        public PokemonGenderPreference? Gender { get; set; } = PokemonGenderPreference.Female;

        public DuplicateSelectionCriterion ToDefinition() =>
            new(Type, Enabled, OriginGameId, Gender);
    }
}
