using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class CompetitiveOptionsControl : UserControl
{
    private readonly ComboBox mode = DropDown("Progress Groups", "Level Bands", "Experience Order");
    private readonly NumericUpDown battleLevel = Number(1, 100, 50);
    private readonly NumericUpDown evTotal = Number(0, 1530, 508);
    private readonly NumericUpDown highLevel = Number(1, 100, 50);
    private readonly NumericUpDown training = Number(1, 100, 20);
    private readonly NumericUpDown endgame = Number(1, 100, 80);
    private readonly CheckBox requireLegal = new() { AutoSize = true, Text = "Require legal Pokémon for Battle Ready" };
    private readonly CheckBox requireMoves = new() { AutoSize = true, Text = "Require all four moves to be non-empty" };
    private readonly ComboBox withinSort = DropDown("Level descending", "Experience descending", "National Dex", "Species name");
    private readonly Label explanation = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(800, 0) };

    public CompetitiveOptionsControl()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 11, Dock = DockStyle.Top, Margin = new Padding(0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(table, 0, "Organization mode:", mode);
        table.Controls.Add(explanation, 1, 1);
        Add(table, 2, "Battle-ready level:", battleLevel);
        Add(table, 3, "Minimum EV total:", evTotal);
        Add(table, 4, "High-level threshold:", highLevel);
        Add(table, 5, "Training threshold:", training);
        Add(table, 6, "Endgame threshold:", endgame);
        Add(table, 7, "Sort within groups:", withinSort);
        table.Controls.Add(requireLegal, 1, 8);
        table.Controls.Add(requireMoves, 1, 9);
        table.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Levels must be ordered training < high level < endgame. EV totals support 0–1530 across PKHeX formats." }, 1, 10);
        Controls.Add(table);
        mode.SelectedIndexChanged += (_, _) => UpdateMode();
        UpdateMode();
    }

    public CompetitiveOrganizerOptions GetOptions(bool rename, bool backgrounds, int maximumNameLength) => new(
        (CompetitiveOrganizationMode)mode.SelectedIndex,
        (int)battleLevel.Value,
        (int)evTotal.Value,
        (int)highLevel.Value,
        (int)training.Value,
        (int)endgame.Value,
        requireLegal.Checked,
        requireMoves.Checked,
        (CompetitiveWithinGroupSort)withinSort.SelectedIndex,
        rename,
        backgrounds,
        maximumNameLength);

    private void UpdateMode()
    {
        var progress = mode.SelectedIndex == 0;
        var bands = mode.SelectedIndex == 1;
        battleLevel.Enabled = evTotal.Enabled = requireLegal.Enabled = requireMoves.Enabled = progress;
        withinSort.Enabled = progress;
        highLevel.Enabled = training.Enabled = progress || bands;
        endgame.Enabled = bands;
        explanation.Text = mode.SelectedIndex switch
        {
            0 => "Creates Battle Ready, High Level, In Training, Low Level, Eggs, and Invalid groups.",
            1 => "Creates configurable level bands, followed by Eggs and Invalid entries.",
            _ => "Sorts by total experience and level, placing eggs after non-eggs without reserving semantic group boundaries.",
        };
    }

    private static void Add(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = label }, 0, row);
        table.Controls.Add(control, 1, row);
    }
    private static NumericUpDown Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 100 };
    private static ComboBox DropDown(params string[] values)
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(values);
        combo.SelectedIndex = 0;
        return combo;
    }
}
