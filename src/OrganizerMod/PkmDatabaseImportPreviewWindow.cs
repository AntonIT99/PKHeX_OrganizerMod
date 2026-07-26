using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class PkmDatabaseImportPreviewWindow : Form
{
    public PkmDatabaseImportPreviewWindow(PkmDatabaseImportSession session)
    {
        var plan = session.Plan;
        Text = "Preview — Import from PKM Database"; ClientSize = new(980, 740); MinimumSize = new(800, 600);
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Font;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(new Label { AutoSize = true, Text = Summary(session) }, 0, 0);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = plan.Warnings.Count == 0 ? SystemColors.GrayText : Color.DarkOrange, Margin = new(0, 8, 0, 8),
            Text = plan.Warnings.Count == 0 ? "Review imports and replacements. No save data changes until the final confirmation." : $"{plan.Warnings.Count} warning(s); review the Warnings tab." }, 0, 1);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("New imports", plan.Decisions.Where(x => x.Kind == DatabaseDecisionKind.NewImport), session));
        tabs.TabPages.Add(Page("Replacements", plan.Decisions.Where(x => x.Kind == DatabaseDecisionKind.Replacement), session));
        tabs.TabPages.Add(Page("Skipped", plan.Decisions.Where(x => x.Kind == DatabaseDecisionKind.Skipped), session));
        var warningPage = new TabPage($"Warnings ({plan.Warnings.Count})"); var warningList = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true }; warningList.Items.AddRange(plan.Warnings.Cast<object>().ToArray()); warningPage.Controls.Add(warningList); tabs.TabPages.Add(warningPage);
        layout.Controls.Add(tabs, 0, 2);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new(0, 8, 0, 0) };
        var cancel = new Button { AutoSize = true, Text = "Cancel", DialogResult = DialogResult.Cancel };
        var apply = new Button { AutoSize = true, DialogResult = DialogResult.OK, Enabled = plan.IsValid && plan.Imports.Count + plan.Replacements.Count > 0, Text = ApplyText(plan) };
        footer.Controls.Add(cancel); footer.Controls.Add(apply); layout.Controls.Add(footer, 0, 3); Controls.Add(layout); CancelButton = cancel;
    }

    private static TabPage Page(string title, IEnumerable<DatabaseImportDecision> source, PkmDatabaseImportSession session)
    {
        var items = source.ToArray(); var page = new TabPage($"{title} ({items.Length})");
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
        foreach (var (name, width) in new[] { ("Pokémon", 150), ("Details", 270), ("Source", 250), ("Target", 150), ("Reason", 420) }) list.Columns.Add(name, width);
        foreach (var d in items)
        {
            var c = d.Candidate; var species = c.Species < GameInfo.Strings.Species.Count ? GameInfo.Strings.Species[c.Species] : $"Species {c.Species}";
            var target = d.ImportDestination is { } slot ? DescribeDestination(slot) :
                d.ReplacementTarget is { } old ? $"REPLACE {DescribeExistingLocation(old)} (Lv {old.Level}, EXP {old.Experience:N0}, PID {old.Pid:X8})" : "—";
            var origin = session.OriginGameNames.GetValueOrDefault(c.OriginGameId, $"Game {c.OriginGameId}");
            list.Items.Add(new ListViewItem([species, $"{(c.IsShiny ? "Shiny · " : "")}Lv {c.Level} · EXP {c.Experience:N0} · {origin} · {c.Gender} · PID {c.Pid:X8}", c.RelativeSourcePath, target, d.Reason]));
        }
        page.Controls.Add(list); return page;
    }
    private static string Summary(PkmDatabaseImportSession s) { var p = s.Plan; var x = p.Summary; var f = x.Filters; var filters = p.Options.Filters; return
        $"Function: Import from PKM Database{Environment.NewLine}Database: {s.DatabasePath}{Environment.NewLine}" +
        $"Same PID: {p.Options.SamePidMode}    Comparison scope: {PidScope(p.Options)}{Environment.NewLine}" +
        $"Team writes: {TeamWriteMode(p.Options)}{Environment.NewLine}" +
        $"Species match action: {SpeciesAction(p.Options.SameSpeciesShinyMode)}    Shiny matching: {ShinyGrouping(p.Options.SpeciesShinyGrouping)}{Environment.NewLine}" +
        $"Filters: legality {filters.Legality}, origin {filters.OriginGame?.ToString() ?? "any"}, minimum level {filters.MinimumLevel?.ToString() ?? "none"}, gender {filters.Gender?.ToString() ?? "any"}, shiny {ShinyFilter(filters.IsShiny)}{Environment.NewLine}" +
        $"Files scanned: {x.FilesScanned}    Pokémon loaded: {x.LoadedPokemon}    Eligible: {x.EligibleAfterFilters}    Existing compared: {x.ExistingPokemonCompared}{Environment.NewLine}" +
        $"New imports: {x.NewImports}    Replacements: {x.Replacements}    Skipped: {x.Skipped}    Enabled destinations: {x.EmptyDestinationSlots}    Remaining: {x.RemainingFreeSlots}{Environment.NewLine}" +
        $"Unreadable: {x.UnreadableFiles}    Incompatible: {x.IncompatiblePokemon}    Sequential filter exclusions — legality {f.ExcludedByLegality}, origin {f.ExcludedByOrigin}, level {f.ExcludedByMinimumLevel}, gender {f.ExcludedByGender}, shiny {f.ExcludedByShiny}"; }
    private static string PidScope(PkmDatabaseImportOptions options)
    {
        var scopes = new List<string> { "selected boxes" };
        if (options.IncludeTeamInPidComparison) scopes.Add("Team");
        if (options.IncludePensionInPidComparison) scopes.Add("Pension");
        return string.Join(" + ", scopes);
    }
    private static string TeamWriteMode(PkmDatabaseImportOptions options)
    {
        var values = new List<string>();
        if (options.AllowTeamReplacements) values.Add("matching PID replacements enabled");
        if (options.UseTeamSlotsForNewImports) values.Add("free Team slots used before boxes");
        return values.Count == 0 ? "disabled" : string.Join("; ", values);
    }
    private static string DescribeDestination(EmptySaveSlot slot) => slot.Area == ExistingPokemonArea.Team
        ? $"Team slot {slot.SlotIndex + 1}"
        : $"Box {slot.BoxIndex + 1}, slot {slot.SlotIndex + 1}";
    private static string DescribeExistingLocation(ExistingSavePokemon pokemon) => pokemon.Area switch
    {
        ExistingPokemonArea.Team => $"Team slot {pokemon.SlotIndex + 1}",
        ExistingPokemonArea.Pension => $"Pension {pokemon.FacilityIndex + 1}, slot {pokemon.SlotIndex + 1}",
        _ => $"Box {pokemon.BoxIndex + 1}, slot {pokemon.SlotIndex + 1}",
    };
    private static string SpeciesAction(SameSpeciesShinyImportMode mode) => mode switch
    {
        SameSpeciesShinyImportMode.BestDatabaseRepresentativeReplaceWhenBetter => "keep most advanced",
        SameSpeciesShinyImportMode.DoNotImportWhenExisting => "skip when a match exists",
        _ => "always import another copy",
    };
    private static string ShinyGrouping(SpeciesShinyGroupingMode mode) =>
        mode == SpeciesShinyGroupingMode.Separate ? "separate shiny/non-shiny groups" : "shiny status ignored";
    private static string ShinyFilter(bool? shiny) => shiny switch { true => "shiny only", false => "non-shiny only", null => "any" };
    private static string ApplyText(DatabaseImportPlan p) => p.Imports.Count == 0 ? $"Replace {p.Replacements.Count} Pokémon" : p.Replacements.Count == 0 ? $"Import {p.Imports.Count} Pokémon" : $"Import {p.Imports.Count} and replace {p.Replacements.Count} Pokémon";
}
