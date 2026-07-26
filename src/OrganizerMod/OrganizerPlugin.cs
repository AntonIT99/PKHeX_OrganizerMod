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
        menuItem.Click += OpenOrganizerWindow;
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
}
