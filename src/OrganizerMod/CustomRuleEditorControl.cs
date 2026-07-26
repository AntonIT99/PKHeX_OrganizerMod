using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class CustomRuleEditorControl : UserControl
{
    private readonly TableLayoutPanel groupRows;
    private readonly TableLayoutPanel sortRows;
    private readonly NumericUpDown training = Number(20);
    private readonly NumericUpDown high = Number(50);
    private readonly NumericUpDown endgame = Number(80);
    private readonly CheckBox startNewBox = new() { AutoSize = true, Checked = true, Text = "Start each group in a new box" };
    private List<GroupRow> groups =
    [
        new(CustomGroupCriterionType.PrimaryType, true),
        new(CustomGroupCriterionType.ShinyStatus, false),
    ];
    private List<SortRow> sorts =
    [
        new(CustomSortCriterionType.NationalDex, true, OrganizerSortDirection.Ascending),
        new(CustomSortCriterionType.Level, true, OrganizerSortDirection.Descending),
        new(CustomSortCriterionType.Experience, false, OrganizerSortDirection.Descending),
        new(CustomSortCriterionType.ShinyStatus, false, OrganizerSortDirection.Ascending),
    ];

    public CustomRuleEditorControl()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 8, Dock = DockStyle.Top, Margin = new Padding(0) };
        root.Controls.Add(Heading("Group by — enable at most two, highest priority first"), 0, 0);
        groupRows = RuleTable();
        root.Controls.Add(groupRows, 0, 1);
        root.Controls.Add(Heading("Sort by — enable at most four, highest priority first"), 0, 2);
        sortRows = RuleTable();
        root.Controls.Add(sortRows, 0, 3);
        var boundaries = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        boundaries.Controls.AddRange([
            new Label { AutoSize = true, Text = "Training starts:" }, training,
            new Label { AutoSize = true, Text = "High level starts:" }, high,
            new Label { AutoSize = true, Text = "Endgame starts:" }, endgame]);
        root.Controls.Add(boundaries, 0, 4);
        root.Controls.Add(startNewBox, 0, 5);
        root.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(800, 0), Text = "Grouping is hierarchical. With group boundaries enabled, unused trailing slots are reserved and may require more selected boxes." }, 0, 6);
        Controls.Add(root);
        RebuildGroups();
        RebuildSorts();
    }

    public CustomOrganizerOptions GetOptions(bool rename, bool backgrounds, int maximumNameLength) => new(
        groups.Select((row, index) => new CustomGroupRule(row.Type, row.Enabled, index, row.ShinyFirst)).ToArray(),
        sorts.Select((row, index) => new CustomSortRule(row.Type, row.Enabled, row.Direction, index)).ToArray(),
        (int)training.Value, (int)high.Value, (int)endgame.Value,
        startNewBox.Checked, rename, backgrounds, maximumNameLength);

    private void RebuildGroups()
    {
        groupRows.Controls.Clear();
        groupRows.RowCount = groups.Count;
        for (var index = 0; index < groups.Count; index++)
        {
            var position = index;
            var model = groups[index];
            var enabled = new CheckBox { AutoSize = true, Checked = model.Enabled };
            var criterion = EnumSelector(model.Type);
            var value = Direction(model.ShinyFirst ? 1 : 0, "Non-Shiny first", "Shiny first");
            value.Enabled = model.Type == CustomGroupCriterionType.ShinyStatus;
            enabled.CheckedChanged += (_, _) =>
            {
                if (enabled.Checked && groups.Count(item => item.Enabled) >= 2) { enabled.Checked = false; return; }
                if (enabled.Checked && groups.Any(item => !ReferenceEquals(item, model) && item.Enabled && item.Type == model.Type)) { enabled.Checked = false; return; }
                model.Enabled = enabled.Checked;
            };
            criterion.SelectedIndexChanged += (_, _) =>
            {
                var selected = (CustomGroupCriterionType)criterion.SelectedItem!;
                if (model.Enabled && groups.Any(item => !ReferenceEquals(item, model) && item.Enabled && item.Type == selected))
                {
                    criterion.SelectedItem = model.Type;
                    return;
                }
                model.Type = selected;
                value.Enabled = model.Type == CustomGroupCriterionType.ShinyStatus;
            };
            value.SelectedIndexChanged += (_, _) => model.ShinyFirst = value.SelectedIndex == 1;
            AddRuleRow(groupRows, position, enabled, criterion, value,
                () => MoveRule(groups, position, -1, RebuildGroups), () => MoveRule(groups, position, 1, RebuildGroups));
        }
    }

    private void RebuildSorts()
    {
        sortRows.Controls.Clear();
        sortRows.RowCount = sorts.Count;
        for (var index = 0; index < sorts.Count; index++)
        {
            var position = index;
            var model = sorts[index];
            var enabled = new CheckBox { AutoSize = true, Checked = model.Enabled };
            var criterion = EnumSelector(model.Type);
            var direction = Direction(model.Direction == OrganizerSortDirection.Ascending ? 0 : 1,
                model.Type == CustomSortCriterionType.ShinyStatus ? "Non-Shiny first" : "Ascending",
                model.Type == CustomSortCriterionType.ShinyStatus ? "Shiny first" : "Descending");
            enabled.CheckedChanged += (_, _) =>
            {
                if (enabled.Checked && sorts.Count(item => item.Enabled) >= 4) { enabled.Checked = false; return; }
                if (enabled.Checked && sorts.Any(item => !ReferenceEquals(item, model) && item.Enabled && item.Type == model.Type)) { enabled.Checked = false; return; }
                model.Enabled = enabled.Checked;
            };
            criterion.SelectedIndexChanged += (_, _) =>
            {
                var selected = (CustomSortCriterionType)criterion.SelectedItem!;
                if (model.Enabled && sorts.Any(item => !ReferenceEquals(item, model) && item.Enabled && item.Type == selected))
                {
                    criterion.SelectedItem = model.Type;
                    return;
                }
                model.Type = selected;
                direction.Items[0] = model.Type == CustomSortCriterionType.ShinyStatus ? "Non-Shiny first" : "Ascending";
                direction.Items[1] = model.Type == CustomSortCriterionType.ShinyStatus ? "Shiny first" : "Descending";
            };
            direction.SelectedIndexChanged += (_, _) => model.Direction = direction.SelectedIndex == 0 ? OrganizerSortDirection.Ascending : OrganizerSortDirection.Descending;
            AddRuleRow(sortRows, position, enabled, criterion, direction,
                () => MoveRule(sorts, position, -1, RebuildSorts), () => MoveRule(sorts, position, 1, RebuildSorts));
        }
    }

    private static void AddRuleRow(TableLayoutPanel table, int row, Control enabled, Control criterion, Control value, Action upAction, Action downAction)
    {
        var up = new Button { AutoSize = true, Enabled = row > 0, Text = "↑", AccessibleName = "Move rule up" };
        var down = new Button { AutoSize = true, Enabled = row < table.RowCount - 1, Text = "↓", AccessibleName = "Move rule down" };
        up.Click += (_, _) => upAction(); down.Click += (_, _) => downAction();
        table.Controls.Add(enabled, 0, row);
        table.Controls.Add(criterion, 1, row);
        table.Controls.Add(value, 2, row);
        table.Controls.Add(up, 3, row);
        table.Controls.Add(down, 4, row);
    }

    private static void MoveRule<T>(List<T> list, int index, int offset, Action rebuild)
    {
        var target = index + offset;
        if ((uint)target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]);
        rebuild();
    }
    private static TableLayoutPanel RuleTable()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 5, Dock = DockStyle.Top, Margin = new Padding(0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }
    private static ComboBox Direction(int selected, params string[] values)
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values); combo.SelectedIndex = selected; return combo;
    }
    private static ComboBox EnumSelector<T>(T selected) where T : struct, Enum
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(Enum.GetValues<T>().Cast<object>().ToArray());
        combo.Format += (_, args) => args.Value = args.ListItem?.ToString() switch
        {
            "ShinyStatus" => "Shiny status",
            "OriginGame" => "Origin game",
            "PrimaryType" => "Primary type",
            "LevelBand" => "Level band",
            "NationalDex" => "National Dex",
            _ => args.ListItem?.ToString() ?? string.Empty,
        };
        combo.SelectedItem = selected;
        return combo;
    }
    private static NumericUpDown Number(int value) => new() { Minimum = 1, Maximum = 100, Value = value, Width = 70 };
    private static Label Heading(string text) => new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 8, 0, 3), Text = text };
    private sealed class GroupRow(CustomGroupCriterionType type, bool enabled) { public CustomGroupCriterionType Type { get; set; } = type; public bool Enabled { get; set; } = enabled; public bool ShinyFirst { get; set; } }
    private sealed class SortRow(CustomSortCriterionType type, bool enabled, OrganizerSortDirection direction) { public CustomSortCriterionType Type { get; set; } = type; public bool Enabled { get; set; } = enabled; public OrganizerSortDirection Direction { get; set; } = direction; }
}
