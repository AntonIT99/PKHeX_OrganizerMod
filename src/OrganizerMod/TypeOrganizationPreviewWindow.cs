using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class TypeOrganizationPreviewWindow : Form
{
    public TypeOrganizationPreviewWindow(TypeOrganizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var plan = session.Plan;

        Text = "Preview — Type-Optimized Box Allocation";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(850, 680);
        MinimumSize = new Size(700, 540);
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 6,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summary = new Label
        {
            AutoSize = true,
            Text = CreateSummary(plan),
        };
        var warning = new Label
        {
            AutoSize = true,
            ForeColor = plan.Warnings.Count == 0 ? SystemColors.GrayText : Color.DarkOrange,
            Margin = new Padding(0, 8, 0, 8),
            Text = plan.Warnings.Count == 0
                ? "Review the complete target layout below. No save data changes until you click Apply."
                : string.Join(Environment.NewLine, plan.Warnings),
        };

        var tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
        };
        PopulateLayout(tree, session);
        tree.ExpandAll();

        var renameHeading = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 4),
            Text = $"Boxes to be renamed: {plan.RenameOperations.Count}",
        };
        var renames = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
        };
        if (plan.RenameOperations.Count == 0)
            renames.Items.Add("No box names will be changed.");
        else
        {
            foreach (var rename in plan.RenameOperations)
            {
                renames.Items.Add(
                    $"Box {rename.BoxIndex + 1}: \"{rename.OriginalName}\" → \"{rename.NewName}\"");
            }
        }

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
        layout.Controls.Add(tree, 0, 2);
        layout.Controls.Add(renameHeading, 0, 3);
        layout.Controls.Add(renames, 0, 4);
        layout.Controls.Add(footer, 0, 5);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private static string CreateSummary(TypeOrganizationPlan plan)
    {
        var mode = plan.LayoutMode == TypeBoxLayoutMode.Compact
            ? "Compact"
            : "Expanded by Type";
        var summary = plan.Summary;
        return
            $"Mode: {mode}    Usable boxes: {plan.UsableBoxCount}    Pokémon organized: {plan.PokemonCount}{Environment.NewLine}" +
            $"Full type boxes: {summary.FullTypeBoxes}    Partial type boxes: {summary.PartialTypeBoxes}    Mixed boxes: {summary.MixedBoxes}{Environment.NewLine}" +
            $"Pokémon in type-coherent boxes: {summary.PokemonInTypeBoxes}    Pokémon in mixed boxes: {summary.PokemonInMixedBoxes}    Unused slots: {summary.UnusedSlots}";
    }

    private static void PopulateLayout(TreeView tree, TypeOrganizationSession session)
    {
        var plan = session.Plan;
        var renames = plan.RenameOperations.ToDictionary(item => item.BoxIndex);
        var assignmentByReference = plan.Assignments.ToDictionary(item => item.Pokemon);
        foreach (var box in plan.Boxes)
        {
            var label = box.IsMixed
                ? "Mixed"
                : GetLocalizedTypeName(box.SharedType!.Value);
            if (renames.TryGetValue(box.TargetBoxIndex, out var rename))
                label = rename.NewName;

            var boxNode = new TreeNode(
                $"Box {box.TargetBoxIndex + 1}: {label} — {box.Pokemon.Count}/30");
            foreach (var reference in box.Pokemon)
            {
                var entity = session.PokemonSnapshots[reference.StableId];
                var species = entity.Species < GameInfo.Strings.Species.Count
                    ? GameInfo.Strings.Species[entity.Species]
                    : $"Species {entity.Species}";
                var target = assignmentByReference[reference];
                boxNode.Nodes.Add(
                    $"{species} — Box {reference.SourceBoxIndex + 1}, slot {reference.SourceSlotIndex + 1} → slot {target.TargetSlotIndex + 1}");
            }

            tree.Nodes.Add(boxNode);
        }
    }

    private static string GetLocalizedTypeName(PokemonElementType type)
    {
        var names = GameInfo.Strings.Types;
        return (int)type < names.Count ? names[(int)type] : type.ToString();
    }
}
