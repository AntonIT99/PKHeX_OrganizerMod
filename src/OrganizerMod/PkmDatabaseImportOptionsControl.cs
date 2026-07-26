using System.Diagnostics;
using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class PkmDatabaseImportOptionsControl : UserControl
{
    private readonly TextBox path;
    private readonly ComboBox pidMode;
    private readonly CheckBox includeTeam;
    private readonly CheckBox allowTeamReplacements;
    private readonly CheckBox useTeamSlots;
    private readonly CheckBox includePension;
    private readonly ComboBox speciesMode;
    private readonly ComboBox shinyGrouping;
    private readonly CheckBox legal;
    private readonly CheckBox originEnabled;
    private readonly ComboBox origin;
    private readonly CheckBox levelEnabled;
    private readonly NumericUpDown level;
    private readonly CheckBox genderEnabled;
    private readonly ComboBox gender;
    private readonly CheckBox shinyEnabled;
    private readonly ComboBox shiny;

    public PkmDatabaseImportOptionsControl()
    {
        AutoSize = true; Dock = DockStyle.Top;
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top, RowCount = 18 };
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
        includeTeam = new CheckBox { AutoSize = true, Text = "Include Team in same-PID comparison" };
        allowTeamReplacements = new CheckBox { AutoSize = true, Text = "Allow matching Team member to be replaced" };
        useTeamSlots = new CheckBox { AutoSize = true, Text = "Use free Team slots for new imports" };
        includePension = new CheckBox { AutoSize = true, Text = "Include Pension in same-PID comparison" };
        table.Controls.Add(includeTeam, 1, 3);
        table.Controls.Add(allowTeamReplacements, 1, 4);
        table.Controls.Add(useTeamSlots, 1, 5);
        table.Controls.Add(includePension, 1, 6);
        table.Controls.Add(Description("Team replacement requires Team comparison. When enabled, new imports use free Team slots first (up to six), then selected empty boxes. Pension remains comparison-only."), 1, 7);
        void UpdateTeamReplacementAvailability()
        {
            allowTeamReplacements.Enabled = includeTeam.Checked;
            if (!includeTeam.Checked) allowTeamReplacements.Checked = false;
        }
        includeTeam.CheckedChanged += (_, _) => UpdateTeamReplacementAvailability();
        UpdateTeamReplacementAvailability();
        speciesMode = DropDown("Always import another copy", "Keep the most advanced representative", "Skip when the save already has a match");
        speciesMode.SelectedIndex = 0;
        AddRow(table, 8, "Species match action:", speciesMode);
        shinyGrouping = DropDown("Keep shiny and non-shiny separate", "Treat shiny and non-shiny as the same species");
        shinyGrouping.SelectedIndex = 0;
        AddRow(table, 9, "Shiny matching:", shinyGrouping);
        var shinyGroupingDescription = Description("");
        table.Controls.Add(shinyGroupingDescription, 1, 10);
        table.SetColumnSpan(shinyGroupingDescription, 2);
        void UpdateShinyDescription() => shinyGroupingDescription.Text = shinyGrouping.SelectedIndex == 0
            ? "A shiny and a non-shiny Pokémon are different matches. Alternate forms still share their species group. This rule uses selected boxes only; PID handling takes precedence."
            : "Shiny status is ignored for species matching. A shiny may match or replace a non-shiny Pokémon, and vice versa. This rule uses selected boxes only; PID handling takes precedence.";
        shinyGrouping.SelectedIndexChanged += (_, _) => UpdateShinyDescription();
        UpdateShinyDescription();

        legal = new CheckBox { AutoSize = true, Text = "Only legal Pokémon", Anchor = AnchorStyles.Left };
        table.Controls.Add(new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Text = "Filters", Margin = new Padding(0, 8, 0, 2) }, 0, 11);
        table.Controls.Add(legal, 0, 12);
        originEnabled = new CheckBox { AutoSize = true, Text = "Origin game", Anchor = AnchorStyles.Left };
        origin = DropDown();
        origin.Items.AddRange(DuplicateSpeciesRemovalService.GetOriginGames().Cast<object>().ToArray());
        if (origin.Items.Count != 0) origin.SelectedIndex = 0;
        Bind(originEnabled, origin); table.Controls.Add(originEnabled, 0, 13); table.Controls.Add(origin, 1, 13);
        levelEnabled = new CheckBox { AutoSize = true, Text = "Minimum level", Anchor = AnchorStyles.Left };
        level = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Dock = DockStyle.Left };
        Bind(levelEnabled, level); table.Controls.Add(levelEnabled, 0, 14); table.Controls.Add(level, 1, 14);
        genderEnabled = new CheckBox { AutoSize = true, Text = "Gender", Anchor = AnchorStyles.Left };
        gender = DropDown("Male", "Female", "Genderless"); gender.SelectedIndex = 0;
        Bind(genderEnabled, gender); table.Controls.Add(genderEnabled, 0, 15); table.Controls.Add(gender, 1, 15);
        shinyEnabled = new CheckBox { AutoSize = true, Text = "Shiny status", Anchor = AnchorStyles.Left };
        shiny = DropDown("Shiny only", "Non-shiny only"); shiny.SelectedIndex = 0;
        Bind(shinyEnabled, shiny); table.Controls.Add(shinyEnabled, 0, 16); table.Controls.Add(shiny, 1, 16);
        table.Controls.Add(Description("All enabled filters must match. Shiny filtering happens before PID and species matching. Scanning never changes the save."), 1, 17);
        Controls.Add(table);
    }

    public string DatabasePath => path.Text;
    public SamePidImportMode PidMode => (SamePidImportMode)pidMode.SelectedIndex;
    public bool IncludeTeamInPidComparison => includeTeam.Checked;
    public bool AllowTeamReplacements => allowTeamReplacements.Checked;
    public bool UseTeamSlotsForNewImports => useTeamSlots.Checked;
    public bool IncludePensionInPidComparison => includePension.Checked;
    public SameSpeciesShinyImportMode SpeciesMode => (SameSpeciesShinyImportMode)speciesMode.SelectedIndex;
    public SpeciesShinyGroupingMode SpeciesShinyGrouping => (SpeciesShinyGroupingMode)shinyGrouping.SelectedIndex;
    public PkmDatabaseFilterOptions Filters => new(
        legal.Checked ? LegalityFilterMode.OnlyLegal : LegalityFilterMode.Regardless,
        originEnabled.Checked ? (origin.SelectedItem as OriginGameChoice)?.Id : null,
        levelEnabled.Checked ? (int)level.Value : null,
        genderEnabled.Checked ? (PokemonGenderPreference)gender.SelectedIndex : null,
        shinyEnabled.Checked ? shiny.SelectedIndex == 0 : null);
    public bool IsDatabaseAvailable => Directory.Exists(DatabasePath);

    private static void Bind(CheckBox check, Control value) { value.Enabled = check.Checked; check.CheckedChanged += (_, _) => value.Enabled = check.Checked; }
    private static void AddRow(TableLayoutPanel table, int row, string label, Control value) { table.Controls.Add(new Label { AutoSize = true, Text = label, Anchor = AnchorStyles.Left }, 0, row); table.Controls.Add(value, 1, row); table.SetColumnSpan(value, 2); }
    private static ComboBox DropDown(params string[] items) { var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); return c; }
    private static Label Description(string text) => new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(680, 0), Text = text };
}
