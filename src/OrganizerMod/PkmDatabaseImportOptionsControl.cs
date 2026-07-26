using System.Diagnostics;
using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class PkmDatabaseImportOptionsControl : UserControl
{
    private readonly TextBox path;
    private readonly ComboBox pidMode;
    private readonly ComboBox speciesMode;
    private readonly CheckBox legal;
    private readonly CheckBox originEnabled;
    private readonly ComboBox origin;
    private readonly CheckBox levelEnabled;
    private readonly NumericUpDown level;
    private readonly CheckBox genderEnabled;
    private readonly ComboBox gender;

    public PkmDatabaseImportOptionsControl()
    {
        AutoSize = true; Dock = DockStyle.Top;
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top, RowCount = 10 };
        table.ColumnStyles.Add(new(SizeType.AutoSize)); table.ColumnStyles.Add(new(SizeType.Percent, 100)); table.ColumnStyles.Add(new(SizeType.AutoSize));
        path = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        try { path.Text = PkmDatabaseImportService.ResolveConfiguredDatabasePath(); } catch (Exception ex) { path.Text = ex.Message; }
        var open = new Button { AutoSize = true, Text = "Open folder" };
        open.Click += (_, _) => { if (Directory.Exists(path.Text)) Process.Start(new ProcessStartInfo("explorer.exe", path.Text) { UseShellExecute = true }); };
        table.Controls.Add(new Label { AutoSize = true, Text = "PKM database:", Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(path, 1, 0); table.Controls.Add(open, 2, 0);

        pidMode = DropDown("Import as an additional Pokémon", "Replace when the database Pokémon is more advanced", "Do not import database Pokémon with an existing PID");
        pidMode.SelectedIndex = 1;
        AddRow(table, 1, "Same PID:", pidMode);
        table.Controls.Add(Description("Same species: replace only for higher level, or equal level and higher experience. Different species: import additionally."), 1, 2);
        speciesMode = DropDown("Import as an additional Pokémon", "Import only the best database representative, replacing a weaker save representative", "Do not import when matching species and shiny status exists");
        speciesMode.SelectedIndex = 0;
        AddRow(table, 3, "Same species + shiny:", speciesMode);
        table.Controls.Add(Description("Alternate forms share this key. PID handling always takes precedence."), 1, 4);

        legal = new CheckBox { AutoSize = true, Text = "Only legal Pokémon" };
        table.Controls.Add(new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Text = "Filters", Margin = new Padding(0, 8, 0, 2) }, 0, 5);
        table.Controls.Add(legal, 1, 5);
        originEnabled = new CheckBox { AutoSize = true, Text = "Origin game", Anchor = AnchorStyles.Left };
        origin = DropDown();
        origin.Items.AddRange(DuplicateSpeciesRemovalService.GetOriginGames().Cast<object>().ToArray());
        if (origin.Items.Count != 0) origin.SelectedIndex = 0;
        Bind(originEnabled, origin); table.Controls.Add(originEnabled, 0, 6); table.Controls.Add(origin, 1, 6);
        levelEnabled = new CheckBox { AutoSize = true, Text = "Minimum level", Anchor = AnchorStyles.Left };
        level = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Dock = DockStyle.Left };
        Bind(levelEnabled, level); table.Controls.Add(levelEnabled, 0, 7); table.Controls.Add(level, 1, 7);
        genderEnabled = new CheckBox { AutoSize = true, Text = "Gender", Anchor = AnchorStyles.Left };
        gender = DropDown("Male", "Female", "Genderless"); gender.SelectedIndex = 0;
        Bind(genderEnabled, gender); table.Controls.Add(genderEnabled, 0, 8); table.Controls.Add(gender, 1, 8);
        table.Controls.Add(Description("All enabled filters must match. Scanning runs in the background and never changes the save."), 1, 9);
        Controls.Add(table);
    }

    public string DatabasePath => path.Text;
    public SamePidImportMode PidMode => (SamePidImportMode)pidMode.SelectedIndex;
    public SameSpeciesShinyImportMode SpeciesMode => (SameSpeciesShinyImportMode)speciesMode.SelectedIndex;
    public PkmDatabaseFilterOptions Filters => new(
        legal.Checked ? LegalityFilterMode.OnlyLegal : LegalityFilterMode.Regardless,
        originEnabled.Checked ? (origin.SelectedItem as OriginGameChoice)?.Id : null,
        levelEnabled.Checked ? (int)level.Value : null,
        genderEnabled.Checked ? (PokemonGenderPreference)gender.SelectedIndex : null);
    public bool IsDatabaseAvailable => Directory.Exists(DatabasePath);

    private static void Bind(CheckBox check, Control value) { value.Enabled = check.Checked; check.CheckedChanged += (_, _) => value.Enabled = check.Checked; }
    private static void AddRow(TableLayoutPanel table, int row, string label, Control value) { table.Controls.Add(new Label { AutoSize = true, Text = label, Anchor = AnchorStyles.Left }, 0, row); table.Controls.Add(value, 1, row); table.SetColumnSpan(value, 2); }
    private static ComboBox DropDown(params string[] items) { var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); return c; }
    private static Label Description(string text) => new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(680, 0), Text = text };
}
