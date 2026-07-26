using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class DuplicateRemovalPreviewWindow : Form
{
    public DuplicateRemovalPreviewWindow(DuplicateRemovalPlan plan, int currentPartyCount)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Text = "Organizer Mod — Review duplicate removal";
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1120, 650);
        MinimumSize = new Size(850, 450);
        StartPosition = FormStartPosition.CenterParent;

        var summary = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 4),
            Text =
                $"{plan.Removals.Count} Pokémon from {plan.DuplicateGroupCount} matching PID/species group(s) will be deleted." +
                Environment.NewLine +
                "Pension Pokémon are read-only and always have highest keep priority.",
        };

        var grid = CreateGrid();
        foreach (var removal in plan.Removals)
        {
            grid.Rows.Add(
                DescribeLocation(removal.Removed.Location),
                DescribePokemon(removal.Removed),
                DescribeLocation(removal.Kept.Location),
                DescribePokemon(removal.Kept),
                DescribeDifferences(removal));
        }

        var partyRemovalCount = plan.Removals.Count(removal => removal.Removed.Location.IsParty);
        var warningText = currentPartyCount - partyRemovalCount == 0 && partyRemovalCount != 0
            ? "Warning: this removal will leave the party empty. The operation cannot be undone inside Organizer Mod."
            : "Review every row carefully. The operation cannot be undone inside Organizer Mod.";
        var warning = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 42,
            Padding = new Padding(12, 8, 12, 4),
            ForeColor = Color.DarkRed,
            Text = warningText,
        };

        var removeButton = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Margin = new Padding(8),
            Padding = new Padding(12, 4, 12, 4),
            Text = $"Remove {plan.Removals.Count} Pokémon",
        };
        var cancelButton = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8),
            Padding = new Padding(12, 4, 12, 4),
            Text = "Cancel",
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(removeButton);

        Controls.Add(grid);
        Controls.Add(warning);
        Controls.Add(buttons);
        Controls.Add(summary);
        AcceptButton = removeButton;
        CancelButton = cancelButton;
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.Fixed3D,
            Dock = DockStyle.Fill,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Both,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.Columns.Add(CreateColumn("DeleteFrom", "Delete from", 15));
        grid.Columns.Add(CreateColumn("DeletedPokemon", "Pokémon to delete", 22));
        grid.Columns.Add(CreateColumn("KeepAt", "Keep at", 15));
        grid.Columns.Add(CreateColumn("KeptPokemon", "Pokémon kept", 22));
        grid.Columns.Add(CreateColumn("Differences", "Differences / keep reason", 26));
        return grid;
    }

    private static DataGridViewTextBoxColumn CreateColumn(
        string name,
        string header,
        float fillWeight) =>
        new()
        {
            Name = name,
            HeaderText = header,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Automatic,
        };

    private static string DescribePokemon(DuplicatePokemon pokemon)
    {
        var names = GameInfo.Strings.Species;
        var speciesName = pokemon.Species < names.Count
            ? names[pokemon.Species]
            : $"Species {pokemon.Species}";
        return
            $"{speciesName} (#{pokemon.Species}){Environment.NewLine}" +
            $"PID {pokemon.PersonalityId:X8} · Level {pokemon.Level} · EXP {pokemon.Experience}";
    }

    private static string DescribeLocation(PokemonStorageLocation location) =>
        location.Area switch
        {
            PokemonStorageArea.Party => $"Team slot {location.Slot + 1}",
            PokemonStorageArea.Box => $"Box {location.Box + 1}, slot {location.Slot + 1}",
            PokemonStorageArea.Pension => $"Pension {location.Facility + 1}, slot {location.Slot + 1} (read-only)",
            _ => throw new ArgumentOutOfRangeException(nameof(location)),
        };

    private static string DescribeDifferences(DuplicateRemoval removal)
    {
        var kept = removal.Kept;
        var deleted = removal.Removed;
        var differences = new List<string>
        {
            kept.Level == deleted.Level
                ? $"Same level ({kept.Level})"
                : $"Level: delete {deleted.Level}, keep {kept.Level}",
            kept.Experience == deleted.Experience
                ? $"Same EXP ({kept.Experience})"
                : $"EXP: delete {deleted.Experience}, keep {kept.Experience}",
        };

        if (kept.Location.IsPension)
            differences.Add("Pension read-only priority");
        else if (kept.Level != deleted.Level)
            differences.Add("Higher-level priority");
        else if (kept.Experience != deleted.Experience)
            differences.Add("Higher-EXP priority");
        else if (kept.Location.IsParty && !deleted.Location.IsParty)
            differences.Add("Team priority");
        else
            differences.Add("Final tie resolved randomly");

        return string.Join(Environment.NewLine, differences);
    }
}
