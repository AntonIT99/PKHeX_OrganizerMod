namespace OrganizerMod.Domain;

public static class DuplicateRemovalPlanner
{
    public static DuplicateRemovalPlan CreatePlan(
        IEnumerable<DuplicatePokemon> pokemon,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(random);

        var removals = new List<DuplicateRemoval>();
        foreach (var group in pokemon.GroupBy(
                     candidate => (candidate.PersonalityId, candidate.Species)))
        {
            var candidates = group.ToArray();
            if (candidates.Length < 2)
                continue;

            var finalists = candidates
                .Where(candidate => candidate.Location.IsPension)
                .ToArray();
            if (finalists.Length == 0)
                finalists = candidates;

            var highestLevel = finalists.Max(candidate => candidate.Level);
            finalists = finalists
                .Where(candidate => candidate.Level == highestLevel)
                .ToArray();

            var highestExperience = finalists.Max(candidate => candidate.Experience);
            finalists = finalists
                .Where(candidate => candidate.Experience == highestExperience)
                .ToArray();

            if (!finalists.Any(candidate => candidate.Location.IsPension) &&
                finalists.Any(candidate => candidate.Location.IsParty))
            {
                finalists = finalists
                    .Where(candidate => candidate.Location.IsParty)
                    .ToArray();
            }

            var kept = finalists.Length == 1
                ? finalists[0]
                : finalists[random.Next(finalists.Length)];

            removals.AddRange(
                candidates
                    .Where(candidate => !candidate.Location.IsPension && candidate != kept)
                    .Select(candidate => new DuplicateRemoval(kept, candidate)));
        }

        return new DuplicateRemovalPlan(removals);
    }
}
