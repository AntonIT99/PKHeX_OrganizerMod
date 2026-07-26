using PKHeX.Core;

namespace OrganizerMod;

public sealed class OrganizerPlugin : IPlugin
{
    private OrganizerWindow? window;
    private MenuStrip? menuStrip;

    public string Name => "Organizer Mod";

    public int Priority => 50;

    public ISaveFileProvider SaveFileEditor { get; private set; } = null!;

    public void Initialize(params object[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        SaveFileEditor = args.OfType<ISaveFileProvider>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Organizer Mod requires PKHeX to provide an ISaveFileProvider during initialization.");

        menuStrip = args.OfType<MenuStrip>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Organizer Mod requires PKHeX to provide its main MenuStrip during initialization.");

        var toolsMenu = menuStrip.Items
            .OfType<ToolStripMenuItem>()
            .FirstOrDefault(item =>
                string.Equals(item.Name, "Menu_Tools", StringComparison.Ordinal) ||
                (item.Text ?? string.Empty).Replace("&", string.Empty, StringComparison.Ordinal)
                    .Equals("Tools", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Organizer Mod could not locate PKHeX's Tools menu.");

        if (toolsMenu.DropDownItems.Find("Menu_OrganizerMod", false).Length != 0)
            return;

        var menuItem = new ToolStripMenuItem(Name)
        {
            Name = "Menu_OrganizerMod",
        };

        var openItem = new ToolStripMenuItem("Open Organizer")
        {
            Name = "Menu_OrganizerMod_Open",
        };
        openItem.Click += OpenOrganizerWindow;

        var removeDuplicateSpeciesItem = new ToolStripMenuItem("Remove Duplicate Species…")
        {
            Name = "Menu_OrganizerMod_RemoveDuplicateSpecies",
        };
        removeDuplicateSpeciesItem.Click += OpenDuplicateSpeciesWindow;
        var importDatabaseItem = new ToolStripMenuItem("Import from PKM Database…")
        {
            Name = "Menu_OrganizerMod_ImportDatabase",
        };
        importDatabaseItem.Click += OpenDatabaseImportWindow;

        var removeDuplicatesItem = new ToolStripMenuItem("Remove duplicates by PID…")
        {
            Name = "Menu_OrganizerMod_RemoveDuplicates",
        };
        removeDuplicatesItem.Click += RemoveDuplicates;

        menuItem.DropDownItems.Add(openItem);
        var typeAllocationItem = new ToolStripMenuItem("Type-Optimized Box Allocation…")
        {
            Name = "Menu_OrganizerMod_TypeAllocation",
        };
        typeAllocationItem.Click += OpenOrganizerWindow;
        menuItem.DropDownItems.Add(typeAllocationItem);
        menuItem.DropDownItems.Add(removeDuplicateSpeciesItem);
        menuItem.DropDownItems.Add(importDatabaseItem);
        menuItem.DropDownItems.Add(new ToolStripSeparator());
        menuItem.DropDownItems.Add(removeDuplicatesItem);
        toolsMenu.DropDownItems.Add(menuItem);
    }

    public void NotifySaveLoaded() => window?.RefreshSaveInfo();

    public bool TryLoadFile(string filePath) => false;

    private void OpenOrganizerWindow(object? sender, EventArgs e)
    {
        if (window is null || window.IsDisposed)
        {
            window = new OrganizerWindow(SaveFileEditor);
            window.FormClosed += (_, _) => window = null;
        }

        if (!window.Visible)
            window.Show(menuStrip?.FindForm());
        else
            window.Activate();
    }

    private void RemoveDuplicates(object? sender, EventArgs e)
    {
        var owner = menuStrip?.FindForm();
        try
        {
            var service = new DuplicateRemovalService(SaveFileEditor);
            var plan = service.CreatePlan();
            if (plan.Removals.Count == 0)
            {
                MessageBox.Show(
                    owner,
                    "No duplicate Pokémon with matching personality IDs were found in the party or boxes.",
                    Name,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var preview = new DuplicateRemovalPreviewWindow(
                plan,
                SaveFileEditor.SAV.PartyCount);
            if (preview.ShowDialog(owner) != DialogResult.OK)
                return;

            service.Apply(plan);
            window?.RefreshSaveInfo();
            MessageBox.Show(
                owner,
                $"Removed {plan.Removals.Count} duplicate Pokémon. Save the file in PKHeX to persist the changes.",
                Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                ex.Message,
                $"{Name} — Duplicate removal failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenDuplicateSpeciesWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectDuplicateSpeciesFunction();
    }

    private void OpenDatabaseImportWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectDatabaseImportFunction();
    }
}
