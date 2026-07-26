using OrganizerMod.Domain;

namespace OrganizerMod;

internal sealed class PkmDatabaseCleanupPreviewWindow : Form
{
    private readonly ListView list;
    private bool updating;

    public PkmDatabaseCleanupPreviewWindow(PkmDatabaseCleanupSession session)
    {
        Text = "Preview — Clean PKM Database";
        ClientSize = new Size(1100, 760);
        MinimumSize = new Size(850, 600);
        StartPosition = FormStartPosition.CenterParent;
        list = new ListView { CheckBoxes = true, Dock = DockStyle.Fill, FullRowSelect = true, GridLines = true, View = View.Details };
        list.Columns.Add("Group", 70); list.Columns.Add("Keep", 50); list.Columns.Add("Pokémon", 130);
        list.Columns.Add("PID", 90); list.Columns.Add("Level", 60); list.Columns.Add("EXP", 100);
        list.Columns.Add("Origin / met data", 250); list.Columns.Add("Database file", 330);
        foreach (var (group, groupIndex) in session.Analysis.Groups.Select((value, index) => (value, index + 1)))
        foreach (var candidate in group.Candidates)
        {
            var item = new ListViewItem([
                groupIndex.ToString(), candidate.StableId == group.SuggestedKeeperId ? "Suggested" : "",
                candidate.SpeciesName, candidate.Identity.PersonalityId.ToString("X8"), candidate.Level.ToString(),
                candidate.Experience.ToString("N0"),
                $"{candidate.Identity.OriginGame} · {candidate.Identity.MetDate?.ToString() ?? "no date"} · place {candidate.Identity.MetLocation} · met Lv {candidate.Identity.MetLevel}",
                candidate.RelativePath]);
            item.Checked = candidate.StableId == group.SuggestedKeeperId;
            item.Tag = new Row(group.GroupId, candidate.StableId);
            list.Items.Add(item);
        }
        list.ItemCheck += KeepExactlyOne;

        var summary = new Label
        {
            AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10),
            Text = $"Database: {session.DatabasePath}{Environment.NewLine}" +
                   $"Files scanned: {session.Analysis.ScannedFiles} · Pokémon loaded: {session.Analysis.LoadedPokemon} · Duplicate groups: {session.Analysis.Groups.Count} · Files to remove: {session.Analysis.DuplicateFiles}{Environment.NewLine}" +
                   "Checked entries are kept. Unchecked duplicates will be moved to a recovery folder beside the database.",
        };
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = "Cancel" };
        var apply = new Button { AutoSize = true, DialogResult = DialogResult.OK, Text = $"Clean {session.Analysis.DuplicateFiles} duplicate files" };
        var best = new Button { AutoSize = true, Text = "Keep highest level and experience" };
        best.Click += (_, _) => SelectSuggested(session);
        footer.Controls.Add(cancel); footer.Controls.Add(apply); footer.Controls.Add(best);
        Controls.Add(list); Controls.Add(summary); Controls.Add(footer);
        CancelButton = cancel;
    }

    public IReadOnlyDictionary<string, string> Keepers =>
        list.Items.Cast<ListViewItem>().Where(item => item.Checked)
            .Select(item => (Row)item.Tag!).ToDictionary(row => row.GroupId, row => row.CandidateId, StringComparer.Ordinal);

    private void KeepExactlyOne(object? sender, ItemCheckEventArgs e)
    {
        if (updating) return;
        var row = (Row)list.Items[e.Index].Tag!;
        if (e.NewValue == CheckState.Checked)
        {
            updating = true;
            try
            {
                for (var index = 0; index < list.Items.Count; index++)
                    if (index != e.Index && ((Row)list.Items[index].Tag!).GroupId == row.GroupId)
                        list.Items[index].Checked = false;
            }
            finally { updating = false; }
        }
        else
        {
            var another = list.Items.Cast<ListViewItem>().Any(item =>
                item.Index != e.Index && item.Checked && ((Row)item.Tag!).GroupId == row.GroupId);
            if (!another) e.NewValue = CheckState.Checked;
        }
    }

    private void SelectSuggested(PkmDatabaseCleanupSession session)
    {
        updating = true;
        try
        {
            var suggested = session.Analysis.Groups.ToDictionary(x => x.GroupId, x => x.SuggestedKeeperId, StringComparer.Ordinal);
            foreach (ListViewItem item in list.Items)
            {
                var row = (Row)item.Tag!;
                item.Checked = suggested[row.GroupId] == row.CandidateId;
            }
        }
        finally { updating = false; }
    }

    private sealed record Row(string GroupId, string CandidateId);
}
