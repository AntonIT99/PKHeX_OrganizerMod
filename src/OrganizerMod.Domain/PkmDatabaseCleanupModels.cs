using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public sealed record PkmInstanceIdentity(
    uint PersonalityId,
    int Species,
    int OriginGame,
    DateOnly? MetDate,
    int MetLocation,
    int MetLevel,
    DateOnly? EggDate,
    int EggLocation,
    int TrainerId,
    int SecretId,
    string OriginalTrainerName,
    int OriginalTrainerGender,
    int Language,
    int Ball,
    int Gender,
    int Nature,
    int IvHp,
    int IvAttack,
    int IvDefense,
    int IvSpeed,
    int IvSpecialAttack,
    int IvSpecialDefense,
    bool IsEgg);

public sealed record PkmDatabaseCleanupCandidate(
    string StableId,
    string RelativePath,
    string ContentFingerprint,
    PkmInstanceIdentity Identity,
    string SpeciesName,
    int Form,
    int Level,
    ulong Experience,
    string Nickname,
    IReadOnlyList<int> Moves);

public sealed record PkmDatabaseDuplicateGroup(
    string GroupId,
    IReadOnlyList<PkmDatabaseCleanupCandidate> Candidates,
    string SuggestedKeeperId);

public sealed class PkmDatabaseCleanupAnalysis
{
    public PkmDatabaseCleanupAnalysis(IEnumerable<PkmDatabaseDuplicateGroup> groups, int scannedFiles, int loadedPokemon,
        int unreadableFiles, IEnumerable<string> warnings)
    {
        Groups = Array.AsReadOnly(groups.ToArray());
        ScannedFiles = scannedFiles;
        LoadedPokemon = loadedPokemon;
        UnreadableFiles = unreadableFiles;
        Warnings = Array.AsReadOnly(warnings.ToArray());
    }
    public ReadOnlyCollection<PkmDatabaseDuplicateGroup> Groups { get; }
    public int ScannedFiles { get; }
    public int LoadedPokemon { get; }
    public int UnreadableFiles { get; }
    public ReadOnlyCollection<string> Warnings { get; }
    public int DuplicateFiles => Groups.Sum(group => group.Candidates.Count - 1);
}
