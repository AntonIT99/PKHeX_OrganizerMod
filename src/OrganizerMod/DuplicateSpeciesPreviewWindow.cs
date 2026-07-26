using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class DuplicateSpeciesPreviewWindow : Form
{
    public DuplicateSpeciesPreviewWindow(DuplicateSpeciesRemovalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var plan = session.Plan;
        Text = "Preview — Remove Duplicate Species";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(940, 720);
        MinimumSize = new Size(760, 560);
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
        layout.Controls.Add(new Label { AutoSize = true, Text = CreateSummary(plan) }, 0, 0);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.Firebrick,
            Margin = new Padding(0, 8, 0, 8),
            Text = "Review every decision below. Kept Pokémon stay in their current slots; only listed REMOVE slots will be cleared after a second confirmation.",
        }, 0, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 470,
        };
        split.Panel1.Controls.Add(CreateDecisionTree(session));
        split.Panel2.Controls.Add(CreateCriteriaList(session));
        layout.Controls.Add(split, 0, 2);

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
        var remove = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Enabled = plan.IsValid && plan.RemovalCandidates.Count != 0,
            Text = $"Remove {plan.RemovalCandidates.Count} duplicate Pokémon",
        };
        footer.Controls.Add(cancel);
        footer.Controls.Add(remove);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        CancelButton = cancel;
    }

    private static string CreateSummary(SpeciesDuplicateRemovalPlan plan)
    {
        var summary = plan.Summary;
        return
            $"Function: Remove Duplicate Species    Selected boxes: {summary.SelectedBoxes}{Environment.NewLine}" +
            $"Pokémon scanned: {summary.PokemonScanned}    Analyzed: {summary.PokemonAnalyzed}    Unique species represented: {summary.UniqueSpeciesRepresented}{Environment.NewLine}" +
            $"Duplicate groups: {summary.DuplicateGroups}    Kept representatives: {summary.KeptRepresentatives}    Pokémon to remove: {summary.RemovalCandidates}{Environment.NewLine}" +
            $"Shiny handling: {DescribeShinyMode(plan.Options.ShinyMode)}{Environment.NewLine}" +
            $"Eggs ignored: {summary.EggsIgnored}    Invalid entries ignored: {summary.InvalidEntriesIgnored}    Shiny Pokémon ignored: {summary.ShinyPokemonIgnored}";
    }

    private static Control CreateDecisionTree(DuplicateSpeciesRemovalSession session)
    {
        var tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
        };
        foreach (var decision in session.Plan.Decisions)
        {
            var entity = session.PokemonSnapshots[decision.Kept.Reference.StableId];
            var species = entity.Species < GameInfo.Strings.Species.Count
                ? GameInfo.Strings.Species[entity.Species]
                : $"Species {entity.Species}";
            var shinyGroup = decision.Key.IsShiny switch
            {
                true => " — shiny group",
                false => " — non-shiny group",
                null => string.Empty,
            };
            var group = new TreeNode(
                $"{entity.Species:D3} {species}{shinyGroup} — {decision.CandidateCount} candidates");
            group.Nodes.Add(new TreeNode($"KEEP — {Describe(decision.Kept, session)}"));
            var reasons = new TreeNode("Reason");
            foreach (var reason in decision.Reasons)
                reasons.Nodes.Add(ResolveReason(reason, session));
            group.Nodes.Add(reasons);
            var remove = new TreeNode($"REMOVE — {decision.Removed.Count}");
            foreach (var candidate in decision.Removed)
                remove.Nodes.Add(Describe(candidate, session));
            group.Nodes.Add(remove);
            tree.Nodes.Add(group);
        }
        tree.ExpandAll();
        return tree;
    }

    private static Control CreateCriteriaList(DuplicateSpeciesRemovalSession session)
    {
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
        };
        var priority = 1;
        foreach (var criterion in session.Plan.Options.Criteria.Where(item => item.Enabled))
            list.Items.Add($"{priority++}. {DescribeCriterion(criterion, session)}");
        list.Items.Add($"{priority}. Earliest original box and slot fallback");
        if (session.Plan.Options.Criteria.All(item => !item.Enabled))
            list.Items.Insert(0, "No criteria enabled; deterministic location fallback selects the representative.");
        if (session.Plan.Options.ShinyMode == ShinyDuplicateMode.IgnoreShiny)
            list.Items.Add("Shiny Pokémon are not analyzed and remain untouched.");
        return list;
    }

    private static string Describe(DuplicateCandidate candidate, DuplicateSpeciesRemovalSession session)
    {
        var origin = session.OriginGameNames.GetValueOrDefault(
            candidate.OriginGameId,
            $"Game ID {candidate.OriginGameId}");
        var shiny = candidate.IsShiny ? "Shiny" : "Non-shiny";
        return
            $"Box {candidate.Reference.SourceBoxIndex + 1}, slot {candidate.Reference.SourceSlotIndex + 1} · " +
            $"Level {candidate.Level} · {origin} · {candidate.Gender} · {shiny}";
    }

    private static string DescribeCriterion(
        DuplicateSelectionCriterion criterion,
        DuplicateSpeciesRemovalSession session) =>
        criterion.Type switch
        {
            DuplicateSelectionCriterionType.HighestLevel => "Highest level",
            DuplicateSelectionCriterionType.PreferredOriginGame =>
                $"Preferred origin game: {session.OriginGameNames.GetValueOrDefault(criterion.PreferredOriginGame ?? -1, $"Game ID {criterion.PreferredOriginGame}")}",
            DuplicateSelectionCriterionType.PreferredGender =>
                $"Preferred gender: {criterion.PreferredGender}",
            _ => throw new ArgumentOutOfRangeException(nameof(criterion)),
        };

    private static string ResolveReason(string reason, DuplicateSpeciesRemovalSession session)
    {
        const string prefix = "Matched preferred origin game ID ";
        if (!reason.StartsWith(prefix, StringComparison.Ordinal))
            return reason;
        var value = reason[prefix.Length..].TrimEnd('.');
        return int.TryParse(value, out var id) && session.OriginGameNames.TryGetValue(id, out var name)
            ? $"Matched preferred origin game: {name}."
            : reason;
    }

    private static string DescribeShinyMode(ShinyDuplicateMode mode) =>
        mode switch
        {
            ShinyDuplicateMode.CombinedWithNonShiny => "Consider shiny and non-shiny the same species",
            ShinyDuplicateMode.SeparateShinyGroup => "Treat shiny and non-shiny separately",
            ShinyDuplicateMode.IgnoreShiny => "Ignore shiny Pokémon",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
}
