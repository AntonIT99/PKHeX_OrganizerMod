using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class GroupedOrganizationPreviewWindow : Form
{
    public GroupedOrganizationPreviewWindow(GroupedOrganizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var plan = session.Plan;
        Text = $"Preview — {plan.StrategyName}";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 720);
        MinimumSize = new Size(760, 580);
        StartPosition = FormStartPosition.CenterParent;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateLayoutPage(session));
        tabs.TabPages.Add(CreateListPage("Group counts", plan.GroupCounts.Select(item =>
            $"{item.DisplayName}: {item.PokemonCount}"), "No Pokémon groups were generated."));
        tabs.TabPages.Add(CreateListPage("Active rules", plan.ActiveRules, "No optional rules are active."));
        tabs.TabPages.Add(CreateListPage("Box renames", plan.RenameOperations.Select(item =>
            $"Box {item.BoxIndex + 1}: \"{item.OriginalName}\" → \"{item.NewName}\""), "No box names will change."));
        tabs.TabPages.Add(CreateListPage("Background changes", session.BackgroundPreviews.Select(item =>
            $"Box {item.BoxIndex + 1}: {item.OriginalDisplayName} → {item.NewDisplayName}"), "No box backgrounds will change."));

        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Dock = DockStyle.Fill, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var summary = plan.Summary;
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = $"Strategy: {plan.StrategyName}    Mode: {plan.ModeDescription}{Environment.NewLine}" +
                   $"Selected boxes: {summary.AvailableBoxes}    Pokémon included: {summary.IncludedPokemon}    Required boxes: {summary.RequiredBoxes}{Environment.NewLine}" +
                   $"Final groups: {summary.FinalGroups}    Eggs: {summary.Eggs}    Invalid entries: {summary.InvalidEntries}    Unused slots: {summary.UnusedSlots}{Environment.NewLine}" +
                   $"Box renames: {plan.RenameOperations.Count}    Background changes: {session.BackgroundChanges.Count}",
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = plan.Warnings.Count == 0 ? SystemColors.GrayText : Color.DarkOrange,
            Margin = new Padding(0, 8, 0, 8),
            Text = plan.Warnings.Count == 0
                ? "Review the complete target layout. Cancel leaves the save unchanged."
                : string.Join(Environment.NewLine, plan.Warnings),
        }, 0, 1);
        layout.Controls.Add(tabs, 0, 2);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        var cancel = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = "Cancel" };
        var apply = new Button { AutoSize = true, DialogResult = DialogResult.OK, Enabled = plan.IsValid, Text = "Apply organization" };
        footer.Controls.Add(cancel);
        footer.Controls.Add(apply);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static TabPage CreateLayoutPage(GroupedOrganizationSession session)
    {
        var page = new TabPage("Proposed layout");
        var tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        var snapshots = session.PokemonSnapshots;
        foreach (var box in session.Plan.Boxes)
        {
            var node = new TreeNode($"Box {box.TargetBoxIndex + 1}: {box.DisplayName} — {box.Pokemon.Count}/30");
            foreach (var reference in box.Pokemon)
            {
                var entity = snapshots[reference.StableId];
                var name = entity.Species < GameInfo.Strings.Species.Count
                    ? GameInfo.Strings.Species[entity.Species]
                    : $"Species {entity.Species}";
                node.Nodes.Add($"{name} — from Box {reference.SourceBoxIndex + 1}, slot {reference.SourceSlotIndex + 1}");
            }
            tree.Nodes.Add(node);
        }
        tree.ExpandAll();
        page.Controls.Add(tree);
        return page;
    }

    private static TabPage CreateListPage(string title, IEnumerable<string> values, string empty)
    {
        var page = new TabPage(title);
        var list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        var items = values.ToArray();
        list.Items.AddRange(items.Length == 0 ? [empty] : items.Cast<object>().ToArray());
        page.Controls.Add(list);
        return page;
    }
}
