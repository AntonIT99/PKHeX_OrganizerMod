using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public sealed class OrganizationPlan
{
    private static readonly OrganizationPlan EmptyPlan = new([]);

    public OrganizationPlan(IEnumerable<SlotMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        Moves = new ReadOnlyCollection<SlotMove>(moves.ToArray());
    }

    public static OrganizationPlan Empty => EmptyPlan;

    public IReadOnlyList<SlotMove> Moves { get; }
}
