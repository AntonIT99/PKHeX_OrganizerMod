namespace OrganizerMod.Domain;

public sealed class PkmDatabaseCleanupAnalyzer
{
    public PkmDatabaseCleanupAnalysis Analyze(
        IReadOnlyList<PkmDatabaseCleanupCandidate> candidates,
        int scannedFiles = 0,
        int unreadableFiles = 0,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Select(x => x.StableId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            throw new ArgumentException("Database candidate identities must be unique.", nameof(candidates));

        var groups = candidates
            .GroupBy(candidate => candidate.Identity)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var ordered = group.OrderBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal).ToArray();
                var keeper = ordered.OrderByDescending(candidate => candidate.Level)
                    .ThenByDescending(candidate => candidate.Experience)
                    .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal).First();
                return new PkmDatabaseDuplicateGroup(
                    $"{group.Key.PersonalityId:X8}:{group.Key.Species}:{ordered[0].StableId}",
                    Array.AsReadOnly(ordered),
                    keeper.StableId);
            })
            .OrderBy(group => group.Candidates[0].Identity.Species)
            .ThenBy(group => group.Candidates[0].Identity.PersonalityId)
            .ThenBy(group => group.GroupId, StringComparer.Ordinal)
            .ToArray();
        return new PkmDatabaseCleanupAnalysis(groups, scannedFiles == 0 ? candidates.Count : scannedFiles,
            candidates.Count, unreadableFiles, warnings ?? []);
    }
}
