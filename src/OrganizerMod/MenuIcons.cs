using System.Reflection;

namespace OrganizerMod;

internal static class MenuIcons
{
    public static Image Organizer { get; } = Load("organizer");
    public static Image TypeAllocation { get; } = Load("type-allocation");
    public static Image DuplicateSpecies { get; } = Load("duplicate-species");
    public static Image ImportDatabase { get; } = Load("import-database");
    public static Image DuplicatePid { get; } = Load("duplicate-pid");

    private static Image Load(string name)
    {
        var resourceName = $"OrganizerMod.Assets.Menu.{name}.png";
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Organizer Mod menu icon resource is missing: {resourceName}");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}
