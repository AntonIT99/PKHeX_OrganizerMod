using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class LivingDexPreviewWindow : Form
{
    public LivingDexPreviewWindow(LivingDexOrganizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var plan = session.Plan;

        Text = "Preview — Living Dex Sorting";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(920, 720);
        MinimumSize = new Size(760, 580);
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summary = new Label
        {
            AutoSize = true,
            Text = CreateSummary(plan, session.DefinitionScope),
        };
        var warning = new Label
        {
            AutoSize = true,
            ForeColor = plan.Warnings.Count == 0 ? SystemColors.GrayText : Color.DarkOrange,
            Margin = new Padding(0, 8, 0, 8),
            Text = plan.Warnings.Count == 0
                ? "Review the complete target layout. No save data changes until you click Apply."
                : string.Join(Environment.NewLine, plan.Warnings),
        };

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateLayoutTab(session));
        tabs.TabPages.Add(CreateMissingTab(plan));
        tabs.TabPages.Add(CreateRenameTab(plan));

        var footer = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0),
        };
        var cancel = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Text = "Cancel",
        };
        var apply = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Enabled = plan.IsValid,
            Text = "Apply",
        };
        footer.Controls.Add(cancel);
        footer.Controls.Add(apply);

        layout.Controls.Add(summary, 0, 0);
        layout.Controls.Add(warning, 0, 1);
        layout.Controls.Add(tabs, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static string CreateSummary(
        LivingDexOrganizationPlan plan,
        string definitionScope)
    {
        var summary = plan.Summary;
        var mode = plan.Options.Mode switch
        {
            LivingDexMode.Species => "Species Living Dex",
            LivingDexMode.Form => "Form Living Dex",
            LivingDexMode.Shiny => "Shiny Living Dex",
            _ => throw new ArgumentOutOfRangeException(),
        };
        var preference = plan.Options.RepresentativePreference switch
        {
            LivingDexRepresentativePreference.DefaultSafest => "Default / Safest",
            LivingDexRepresentativePreference.OldestObtained => "Oldest obtained",
            LivingDexRepresentativePreference.Strongest => "Strongest",
            _ => throw new ArgumentOutOfRangeException(),
        };
        return
            $"Strategy: Living Dex Sorting    Mode: {mode}    Representative: {preference}{Environment.NewLine}" +
            $"Scope: {definitionScope}{Environment.NewLine}" +
            $"Selected boxes: {summary.SelectedBoxes}    Included Pokémon: {summary.IncludedPokemon}    Preserved in place: {summary.PreservedPokemon}{Environment.NewLine}" +
            $"Expected entries: {summary.ExpectedEntries}    Filled: {summary.FilledEntries}    Missing: {summary.MissingEntries}    Completion: {summary.CompletionPercentage:F1}%{Environment.NewLine}" +
            $"Duplicates: {summary.DuplicatePokemon}    Overflow Pokémon: {summary.OverflowPokemon}    Main boxes: {summary.MainBoxes}    Overflow boxes: {summary.OverflowBoxes}{Environment.NewLine}" +
            $"Required boxes: {summary.RequiredBoxes}    Available slots: {summary.AvailableSlots}    Unused selected slots: {summary.UnusedSelectedSlots}    Box renames: {plan.RenameOperations.Count}";
    }

    private static TabPage CreateLayoutTab(LivingDexOrganizationSession session)
    {
        var page = new TabPage("Proposed layout");
        var tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
        };
        var plan = session.Plan;
        var renames = plan.RenameOperations.ToDictionary(item => item.BoxIndex);
        var assignments = plan.Assignments.ToDictionary(item => item.Pokemon);
        foreach (var box in plan.Boxes)
        {
            var label = renames.TryGetValue(box.TargetBoxIndex, out var rename)
                ? rename.NewName
                : box.IsOverflowOnly
                    ? "Overflow"
                    : GetMainLabel(plan.Options.Mode);
            var node = new TreeNode(
                $"Box {box.TargetBoxIndex + 1}: {label} — {box.PokemonCount}/30");
            foreach (var reference in box.MainPokemon.Concat(box.OverflowPokemon))
            {
                var entity = session.PokemonSnapshots[reference.StableId];
                var species = entity.Species < GameInfo.Strings.Species.Count
                    ? GameInfo.Strings.Species[entity.Species]
                    : $"Species {entity.Species}";
                var assignment = assignments[reference];
                var role = assignment.IsOverflow ? "Overflow" : "Living Dex";
                node.Nodes.Add(
                    $"{entity.Species:D3} {species} — {role} — Box {reference.SourceBoxIndex + 1}, slot {reference.SourceSlotIndex + 1} → slot {assignment.TargetSlotIndex + 1}");
            }
            tree.Nodes.Add(node);
        }
        tree.ExpandAll();
        page.Controls.Add(tree);
        return page;
    }

    private static TabPage CreateMissingTab(LivingDexOrganizationPlan plan)
    {
        var page = new TabPage($"Missing entries ({plan.MissingEntries.Count})");
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            View = View.Details,
        };
        list.Columns.Add("National Dex", 100);
        list.Columns.Add("Species", 220);
        list.Columns.Add("Form", 220);
        list.Columns.Add("Requirement", 120);
        foreach (var missing in plan.MissingEntries)
        {
            var definition = missing.Definition;
            list.Items.Add(new ListViewItem(
            [
                definition.Key.Species.ToString("D3"),
                definition.SpeciesName,
                definition.FormName ?? "Base / species",
                definition.Key.RequiresShiny ? "Shiny" : "Normal",
            ]));
        }

        var copy = new Button
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            Text = "Copy missing list",
        };
        copy.Click += (_, _) =>
        {
            var lines = plan.MissingEntries.Select(item =>
            {
                var definition = item.Definition;
                var form = definition.FormName is null ? string.Empty : $"\t{definition.FormName}";
                var shiny = definition.Key.RequiresShiny ? "\tShiny" : string.Empty;
                return $"{definition.Key.Species:D3}\t{definition.SpeciesName}{form}{shiny}";
            });
            var text = string.Join(Environment.NewLine, lines);
            if (text.Length != 0)
                Clipboard.SetText(text);
        };
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(copy, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage CreateRenameTab(LivingDexOrganizationPlan plan)
    {
        var page = new TabPage($"Box renames ({plan.RenameOperations.Count})");
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
        };
        if (plan.RenameOperations.Count == 0)
            list.Items.Add("No box names will be changed.");
        else
        {
            foreach (var rename in plan.RenameOperations)
            {
                list.Items.Add(
                    $"Box {rename.BoxIndex + 1}: \"{rename.OriginalName}\" → \"{rename.NewName}\"");
            }
        }
        page.Controls.Add(list);
        return page;
    }

    private static string GetMainLabel(LivingDexMode mode) =>
        mode switch
        {
            LivingDexMode.Species => "Living Dex",
            LivingDexMode.Form => "Form Dex",
            LivingDexMode.Shiny => "Shiny Dex",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
}
