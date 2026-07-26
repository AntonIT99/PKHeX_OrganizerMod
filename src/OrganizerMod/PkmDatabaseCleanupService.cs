using System.Collections.ObjectModel;
using System.Security.Cryptography;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class PkmDatabaseCleanupSession(
    string databasePath,
    PkmDatabaseCleanupAnalysis analysis,
    IReadOnlyDictionary<string, string> absolutePaths,
    IReadOnlyDictionary<string, string> fingerprints)
{
    public string DatabasePath { get; } = databasePath;
    public PkmDatabaseCleanupAnalysis Analysis { get; } = analysis;
    public IReadOnlyDictionary<string, string> AbsolutePaths { get; } = absolutePaths;
    public IReadOnlyDictionary<string, string> Fingerprints { get; } = fingerprints;
}

internal sealed class PkmDatabaseCleanupService(ISaveFileProvider saveFileProvider)
{
    private readonly PkmDatabaseCleanupAnalyzer analyzer = new();

    public PkmDatabaseCleanupSession Scan(string databasePath)
    {
        if (!Directory.Exists(databasePath))
            throw new DirectoryNotFoundException($"PKM database directory does not exist: {databasePath}");
        var save = saveFileProvider.SAV;
        var files = Directory.EnumerateFiles(databasePath, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(databasePath, path), StringComparer.OrdinalIgnoreCase).ToArray();
        var candidates = new List<PkmDatabaseCleanupCandidate>();
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var unreadable = 0;
        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                if (!EntityDetection.IsSizePlausible(info.Length)) continue;
                var data = File.ReadAllBytes(file);
                if (!FileUtil.TryGetPKM(data, out var pk, info.Extension, save) || pk.Species == 0)
                {
                    unreadable++; warnings.Add($"{Path.GetRelativePath(databasePath, file)} could not be parsed."); continue;
                }
                var relative = Path.GetRelativePath(databasePath, file).Replace(Path.DirectorySeparatorChar, '/');
                var hash = Convert.ToHexString(SHA256.HashData(data));
                var id = $"{relative}:{hash}";
                var identity = new PkmInstanceIdentity(pk.PID, pk.Species, (int)pk.Version, pk.MetDate,
                    pk.MetLocation, pk.MetLevel, pk.EggMetDate, pk.EggLocation, pk.TID16, pk.SID16,
                    pk.OriginalTrainerName, pk.OriginalTrainerGender, pk.Language, pk.Ball, pk.Gender,
                    (int)pk.Nature, pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPE, pk.IV_SPA, pk.IV_SPD, pk.IsEgg);
                var species = pk.Species < GameInfo.Strings.Species.Count
                    ? GameInfo.Strings.Species[pk.Species] : $"Species {pk.Species}";
                candidates.Add(new(id, relative, hash, identity, species, pk.Form, pk.CurrentLevel, pk.EXP,
                    pk.Nickname, [pk.Move1, pk.Move2, pk.Move3, pk.Move4]));
                paths[id] = file; fingerprints[id] = hash;
            }
            catch (Exception ex)
            {
                unreadable++; warnings.Add($"{Path.GetRelativePath(databasePath, file)}: {ex.Message}");
            }
        }
        return new(databasePath, analyzer.Analyze(candidates, files.Length, unreadable, warnings),
            new ReadOnlyDictionary<string, string>(paths), new ReadOnlyDictionary<string, string>(fingerprints));
    }

    public string Apply(PkmDatabaseCleanupSession session, IReadOnlyDictionary<string, string> keepers)
    {
        foreach (var group in session.Analysis.Groups)
            if (!keepers.TryGetValue(group.GroupId, out var keeper) || group.Candidates.All(x => x.StableId != keeper))
                throw new InvalidOperationException("Every duplicate group must have exactly one selected keeper.");
        var removals = session.Analysis.Groups.SelectMany(group =>
            group.Candidates.Where(candidate => candidate.StableId != keepers[group.GroupId])).ToArray();
        foreach (var candidate in session.Analysis.Groups.SelectMany(group => group.Candidates))
        {
            var path = session.AbsolutePaths[candidate.StableId];
            if (!File.Exists(path) || Fingerprint(path) != session.Fingerprints[candidate.StableId])
                throw new InvalidOperationException($"Database file changed after preview: {candidate.RelativePath}");
        }
        var parent = Directory.GetParent(Path.GetFullPath(session.DatabasePath))?.FullName
            ?? throw new InvalidOperationException("The database parent directory is unavailable.");
        var recovery = Path.Combine(parent, $"{new DirectoryInfo(session.DatabasePath).Name}.OrganizerMod Backups",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var moved = new List<(string Source, string Destination)>();
        try
        {
            foreach (var candidate in removals)
            {
                var source = session.AbsolutePaths[candidate.StableId];
                var destination = Path.Combine(recovery, candidate.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination)) destination += $".{candidate.ContentFingerprint[..8]}";
                File.Move(source, destination);
                moved.Add((source, destination));
            }
            return recovery;
        }
        catch
        {
            foreach (var item in moved.AsEnumerable().Reverse())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Source)!);
                if (File.Exists(item.Destination) && !File.Exists(item.Source))
                    File.Move(item.Destination, item.Source);
            }
            throw;
        }
    }

    private static string Fingerprint(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
