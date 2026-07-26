using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class SmartTeamBuilderPreviewWindow : Form
{
    public SmartTeamBuilderPreviewWindow(SmartTeamBuilderSession session)
    {
        Text = "Preview — Smart Team Builder";
        ClientSize = new Size(980, 740);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("Proposed Team", TeamTree(session)));
        tabs.TabPages.Add(Page("Exchanges", ExchangeTree(session)));
        tabs.TabPages.Add(Page("Rules and exclusions", Rules(session.Plan)));
        var summary = new TextBox
        {
            Dock = DockStyle.Top, Height = 145, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Text = Summary(session.Plan),
        };
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = "Cancel" };
        var apply = new Button { AutoSize = true, DialogResult = DialogResult.OK, Text = "Build this team", Enabled = session.Plan.IsValid };
        footer.Controls.Add(cancel); footer.Controls.Add(apply);
        Controls.Add(tabs); Controls.Add(summary); Controls.Add(footer);
        CancelButton = cancel;
    }

    private static TreeView TeamTree(SmartTeamBuilderSession session)
    {
        var tree = Tree();
        foreach (var decision in session.Plan.SelectedTeam.OrderBy(x => x.FinalTeamSlot))
        {
            var candidate = session.Candidates[decision.StableId];
            var root = new TreeNode($"Team slot {decision.FinalTeamSlot + 1}: {candidate.DisplayName}");
            root.Nodes.Add($"Level {candidate.Level} · EXP {candidate.Experience:N0} · {Types(candidate)} · {(candidate.IsShiny ? "Shiny" : "Non-shiny")}");
            root.Nodes.Add($"Origin: {candidate.OriginGameName} · Species generation {candidate.SpeciesGeneration} · Source: {DescribeLocation(candidate.OriginalLocation)}");
            var reasons = new TreeNode("Selection reason");
            foreach (var reason in decision.Reasons) reasons.Nodes.Add(reason);
            root.Nodes.Add(reasons); tree.Nodes.Add(root);
        }
        tree.ExpandAll(); return tree;
    }

    private static TreeView ExchangeTree(SmartTeamBuilderSession session)
    {
        var tree = Tree();
        foreach (var group in session.Plan.LocationChanges.GroupBy(change =>
                     change.Source.IsParty ? "TEAM → BOX" : change.Destination.IsParty ? "BOX → TEAM" : "TEAM ORDER"))
        {
            var root = new TreeNode(group.Key);
            foreach (var change in group)
            {
                var candidate = session.Candidates[change.StableId];
                root.Nodes.Add($"{candidate.DisplayName}: {DescribeLocation(change.Source)} → {DescribeLocation(change.Destination)}");
            }
            tree.Nodes.Add(root);
        }
        tree.ExpandAll(); return tree;
    }

    private static Control Rules(TeamExchangePlan plan)
    {
        var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
        var lines = new List<string> { "Eligibility:" };
        lines.AddRange(plan.Options.EligibilityRules.Where(x => x.Enabled).Select(x => $"- {Describe(x)}"));
        if (!plan.Options.EligibilityRules.Any(x => x.Enabled)) lines.Add("- No additional eligibility filters");
        lines.Add(""); lines.Add("Preferences:");
        var index = 1;
        lines.AddRange(plan.Options.PreferenceCriteria.Where(x => x.Enabled).Select(x => $"{index++}. {Describe(x)}"));
        lines.Add($"{index}. Current Team and original-location fallback");
        lines.Add(""); lines.Add("Sequential exclusions:");
        lines.AddRange(plan.SequentialExclusionCounts.Select(x => $"- {x.Key}: {x.Value}"));
        box.Text = string.Join(Environment.NewLine, lines); return box;
    }

    private static string Summary(TeamExchangePlan plan)
    {
        var x = plan.Summary;
        return $"Function: Smart Team Builder{Environment.NewLine}" +
               $"Candidate boxes: {x.CandidateBoxes}    Candidate Pokémon: {x.CandidatePokemon}    Eligible Pokémon: {x.EligiblePokemon}{Environment.NewLine}" +
               $"Requested Team size: {x.RequestedTeamSize}    Selected Team size: {x.SelectedTeamSize}    Current Team retained: {x.RetainedTeamPokemon}{Environment.NewLine}" +
               $"Box → Team: {x.MovedFromBoxesToTeam}    Team → boxes: {x.MovedFromTeamToBoxes}    Unchanged box Pokémon: {x.UnchangedBoxPokemon}{Environment.NewLine}" +
               $"Warnings: {plan.Warnings.Count}" +
               (plan.Warnings.Count == 0 ? string.Empty : $"{Environment.NewLine}{string.Join(Environment.NewLine, plan.Warnings)}");
    }

    private static string Describe(TeamEligibilityRule rule) => rule.Type switch
    {
        TeamEligibilityRuleType.RequiredTypes => $"{rule.TypeMatching}: {string.Join(", ", rule.Types ?? [])}",
        TeamEligibilityRuleType.RequiredOriginGame => $"Origin game ID {rule.OriginGame}",
        TeamEligibilityRuleType.RequiredSpeciesGeneration => $"Species generation {rule.SpeciesGeneration}",
        TeamEligibilityRuleType.LegendaryOrMythicalOnly => "Legendary, Sub-Legendary, or Mythical only",
        TeamEligibilityRuleType.ShinyOnly => "Shiny only",
        _ => rule.Type.ToString(),
    };
    private static string Describe(TeamPreferenceCriterion criterion) => criterion.Type switch
    {
        TeamPreferenceCriterionType.PreferredTypes => $"Prefer types: {string.Join(", ", criterion.Types ?? [])}",
        TeamPreferenceCriterionType.PreferredOriginGame => $"Prefer origin game ID {criterion.OriginGame}",
        TeamPreferenceCriterionType.PreferredSpeciesGeneration => $"Prefer species generation {criterion.SpeciesGeneration}",
        TeamPreferenceCriterionType.HighestLevelAndExperience => "Highest level and experience",
        TeamPreferenceCriterionType.PreferLegendaryOrMythical => "Prefer Legendary or Mythical",
        TeamPreferenceCriterionType.PreferShiny => "Prefer shiny Pokémon",
        _ => criterion.Type.ToString(),
    };
    private static string Types(TeamBuilderCandidate candidate) => candidate.SecondaryType is { } secondary ? $"{candidate.PrimaryType}/{secondary}" : candidate.PrimaryType.ToString();
    private static string DescribeLocation(PokemonStorageLocation location) => location.Area switch
    {
        PokemonStorageArea.Party => $"Team slot {location.Slot + 1}",
        PokemonStorageArea.Box => $"Box {location.Box + 1}, slot {location.Slot + 1}",
        _ => location.Area.ToString(),
    };
    private static TreeView Tree() => new() { Dock = DockStyle.Fill, HideSelection = false };
    private static TabPage Page(string title, Control control) { var page = new TabPage(title); page.Controls.Add(control); return page; }
}
