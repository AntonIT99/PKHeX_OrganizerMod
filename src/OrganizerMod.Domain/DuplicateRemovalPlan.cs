using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public sealed class DuplicateRemovalPlan
{
    public DuplicateRemovalPlan(IEnumerable<DuplicateRemoval> removals)
    {
        ArgumentNullException.ThrowIfNull(removals);
        Removals = new ReadOnlyCollection<DuplicateRemoval>(removals.ToArray());
    }

    public IReadOnlyList<DuplicateRemoval> Removals { get; }

    public int DuplicateGroupCount =>
        Removals
            .Select(removal => (removal.Kept.PersonalityId, removal.Kept.Species))
            .Distinct()
            .Count();
}
