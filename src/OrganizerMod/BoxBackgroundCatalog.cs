using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed record AvailableBoxBackground(
    int WallpaperId,
    BoxBackgroundTheme Theme,
    string DisplayName,
    bool IsWritable);

internal sealed record BoxBackgroundPreview(
    int BoxIndex,
    PokemonElementType? AssignedType,
    bool IsMixed,
    BoxBackgroundTheme? Theme,
    BackgroundThemeChoice Choice,
    int? OriginalWallpaperId,
    int? NewWallpaperId,
    string OriginalDisplayName,
    string? NewDisplayName,
    bool Changed,
    bool Preserved,
    string? Warning);

internal sealed record BoxBackgroundChangeOperation(
    int BoxIndex,
    BoxBackgroundTheme Theme,
    BackgroundThemeChoice Choice,
    int OriginalWallpaperId,
    int NewWallpaperId,
    string OriginalDisplayName,
    string NewDisplayName);

internal sealed class BoxBackgroundCatalog
{
    private static readonly BoxBackgroundTheme[] StandardThemes =
    [
        BoxBackgroundTheme.Forest,
        BoxBackgroundTheme.City,
        BoxBackgroundTheme.Desert,
        BoxBackgroundTheme.Steppe,
        BoxBackgroundTheme.Rocky,
        BoxBackgroundTheme.Volcano,
        BoxBackgroundTheme.Snow,
        BoxBackgroundTheme.Cave,
        BoxBackgroundTheme.Beach,
        BoxBackgroundTheme.DeepSea,
        BoxBackgroundTheme.River,
        BoxBackgroundTheme.Sky,
        BoxBackgroundTheme.Checkered,
        BoxBackgroundTheme.PokemonCenter,
        BoxBackgroundTheme.Metal,
        BoxBackgroundTheme.White,
    ];

    private static readonly IReadOnlyDictionary<BoxBackgroundTheme, string> EnglishNames =
        new Dictionary<BoxBackgroundTheme, string>
        {
            [BoxBackgroundTheme.Forest] = "Forest",
            [BoxBackgroundTheme.City] = "City",
            [BoxBackgroundTheme.Desert] = "Desert",
            [BoxBackgroundTheme.Steppe] = "Savanna",
            [BoxBackgroundTheme.Rocky] = "Crag",
            [BoxBackgroundTheme.Volcano] = "Volcano",
            [BoxBackgroundTheme.Snow] = "Snow",
            [BoxBackgroundTheme.Cave] = "Cave",
            [BoxBackgroundTheme.Beach] = "Beach",
            [BoxBackgroundTheme.DeepSea] = "Seafloor",
            [BoxBackgroundTheme.River] = "River",
            [BoxBackgroundTheme.Sky] = "Sky",
            [BoxBackgroundTheme.Checkered] = "Checks",
            [BoxBackgroundTheme.PokemonCenter] = "Pokémon Center",
            [BoxBackgroundTheme.Metal] = "Machine",
            [BoxBackgroundTheme.White] = "Simple",
        };

    private readonly IBoxDetailWallpaper? wallpapers;
    private readonly int wallpaperCount;
    private readonly AvailableBoxBackground[] available;
    private readonly IReadOnlyDictionary<BoxBackgroundTheme, AvailableBoxBackground> byTheme;

    public BoxBackgroundCatalog(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        wallpapers = save as IBoxDetailWallpaper;
        wallpaperCount = GetWallpaperCount(save);
        var semanticCount = GetSemanticWallpaperCount(save);
        available = Enumerable.Range(0, semanticCount)
            .Select(id =>
            {
                var theme = StandardThemes[id];
                return new AvailableBoxBackground(id, theme, GetKnownDisplayName(id, theme), wallpapers is not null);
            })
            .ToArray();
        byTheme = available.ToDictionary(item => item.Theme);
    }

    public bool CanAssign => wallpapers is not null && available.Length != 0;
    public IReadOnlyList<AvailableBoxBackground> Available => available;
    public IReadOnlySet<BoxBackgroundTheme> SupportedThemes => byTheme.Keys.ToHashSet();

    public bool TryResolveTheme(BoxBackgroundTheme theme, out AvailableBoxBackground background) =>
        byTheme.TryGetValue(theme, out background!);

    public int GetCurrentWallpaper(int box)
    {
        if (wallpapers is null)
            throw new NotSupportedException("The loaded save does not expose writable box backgrounds.");
        return wallpapers.GetBoxWallpaper(box);
    }

    public string GetDisplayName(int wallpaperId)
    {
        if ((uint)wallpaperId < available.Length)
            return available[wallpaperId].DisplayName;
        var localized = GameInfo.Strings.wallpapernames;
        if ((uint)wallpaperId < wallpaperCount &&
            wallpaperId < localized.Length &&
            !string.IsNullOrWhiteSpace(localized[wallpaperId]))
        {
            return localized[wallpaperId];
        }
        return $"Wallpaper {wallpaperId + 1}";
    }

    public void SetWallpaper(int box, int wallpaperId)
    {
        if (wallpapers is null)
            throw new NotSupportedException("The loaded save no longer exposes writable box backgrounds.");
        if ((uint)wallpaperId >= available.Length)
            throw new ArgumentOutOfRangeException(nameof(wallpaperId), wallpaperId, "The wallpaper is outside the supported semantic catalog.");
        wallpapers.SetBoxWallpaper(box, wallpaperId);
    }

    private string GetKnownDisplayName(int id, BoxBackgroundTheme theme)
    {
        var localized = GameInfo.Strings.wallpapernames;
        return id < localized.Length && !string.IsNullOrWhiteSpace(localized[id])
            ? localized[id]
            : EnglishNames[theme];
    }

    private static int GetSemanticWallpaperCount(SaveFile save)
    {
        if (save is not IBoxDetailWallpaper)
            return 0;
        return save.Generation switch
        {
            3 when save is SAV3 or SAV3RSBox => StandardThemes.Length,
            4 or 5 or 6 or 7 => StandardThemes.Length,
            8 when save is SAV8BS => StandardThemes.Length,
            _ => 0,
        };
    }

    private static int GetWallpaperCount(SaveFile save) =>
        save.Generation switch
        {
            3 when save is SAV3 or SAV3RSBox => 16,
            4 or 5 or 6 => 24,
            7 => 16,
            8 when save is SAV8BS => 32,
            8 => 19,
            9 => 20,
            _ => 0,
        };
}
