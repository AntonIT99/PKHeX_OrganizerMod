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
            Image = MenuIcons.Organizer,
        };

        var openItem = new ToolStripMenuItem("Open Organizer")
        {
            Name = "Menu_OrganizerMod_Open",
            Image = MenuIcons.Organizer,
        };
        openItem.Click += OpenOrganizerWindow;

        var removeDuplicateSpeciesItem = new ToolStripMenuItem("Remove Duplicate Species…")
        {
            Name = "Menu_OrganizerMod_RemoveDuplicateSpecies",
            Image = MenuIcons.DuplicateSpecies,
        };
        removeDuplicateSpeciesItem.Click += OpenDuplicateSpeciesWindow;
        var importDatabaseItem = new ToolStripMenuItem("Import from PKM Database…")
        {
            Name = "Menu_OrganizerMod_ImportDatabase",
            Image = MenuIcons.ImportDatabase,
        };
        importDatabaseItem.Click += OpenDatabaseImportWindow;
        var smartTeamItem = new ToolStripMenuItem("Smart Team Builder…")
        {
            Name = "Menu_OrganizerMod_SmartTeamBuilder",
            Image = MenuIcons.Organizer,
        };
        smartTeamItem.Click += OpenSmartTeamBuilderWindow;

        var removeDuplicatesItem = new ToolStripMenuItem("Remove duplicates by PID…")
        {
            Name = "Menu_OrganizerMod_RemoveDuplicates",
            Image = MenuIcons.DuplicatePid,
        };
        removeDuplicatesItem.Click += OpenPidDuplicateWindow;

        menuItem.DropDownItems.Add(openItem);
        var typeAllocationItem = new ToolStripMenuItem("Type-Optimized Box Allocation…")
        {
            Name = "Menu_OrganizerMod_TypeAllocation",
            Image = MenuIcons.TypeAllocation,
        };
        typeAllocationItem.Click += OpenTypeAllocationWindow;
        menuItem.DropDownItems.Add(typeAllocationItem);
        var livingDexItem = new ToolStripMenuItem("Living Dex Sorting…")
        {
            Name = "Menu_OrganizerMod_LivingDexSorting",
            Image = MenuIcons.Organizer,
        };
        livingDexItem.Click += OpenLivingDexSortingWindow;
        menuItem.DropDownItems.Add(livingDexItem);
        var competitiveItem = new ToolStripMenuItem("Competitive / Progress Organizer…")
        {
            Name = "Menu_OrganizerMod_Competitive",
            Image = MenuIcons.Organizer,
        };
        competitiveItem.Click += OpenCompetitiveWindow;
        menuItem.DropDownItems.Add(competitiveItem);
        var customItem = new ToolStripMenuItem("Custom Rule-Based Organizer…")
        {
            Name = "Menu_OrganizerMod_CustomRules",
            Image = MenuIcons.Organizer,
        };
        customItem.Click += OpenCustomRuleWindow;
        menuItem.DropDownItems.Add(customItem);
        menuItem.DropDownItems.Add(importDatabaseItem);
        menuItem.DropDownItems.Add(smartTeamItem);
        menuItem.DropDownItems.Add(removeDuplicateSpeciesItem);
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

    private void OpenDuplicateSpeciesWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectDuplicateSpeciesFunction();
    }

    private void OpenTypeAllocationWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectTypeAllocationStrategy();
    }

    private void OpenLivingDexSortingWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectLivingDexSortingStrategy();
    }

    private void OpenCompetitiveWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectCompetitiveStrategy();
    }

    private void OpenCustomRuleWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectCustomRuleStrategy();
    }

    private void OpenDatabaseImportWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectDatabaseImportFunction();
    }

    private void OpenPidDuplicateWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectPidDuplicateFunction();
    }

    private void OpenSmartTeamBuilderWindow(object? sender, EventArgs e)
    {
        OpenOrganizerWindow(sender, e);
        window?.SelectSmartTeamBuilderFunction();
    }
}
