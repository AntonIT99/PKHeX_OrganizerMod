namespace OrganizerMod.Domain;

public sealed record SlotMove
{
    public SlotMove(SlotPosition source, SlotPosition destination)
    {
        if (source == destination)
            throw new ArgumentException("Source and destination must be different.", nameof(destination));

        Source = source;
        Destination = destination;
    }

    public SlotPosition Source { get; }

    public SlotPosition Destination { get; }
}
