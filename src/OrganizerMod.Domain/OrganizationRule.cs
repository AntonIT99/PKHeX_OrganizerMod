namespace OrganizerMod.Domain;

public sealed class OrganizationRule
{
    public OrganizationRule(int sourceBox, int destinationBox)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceBox);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationBox);
        if (sourceBox == destinationBox)
            throw new ArgumentException("Source and destination boxes must be different.", nameof(destinationBox));

        SourceBox = sourceBox;
        DestinationBox = destinationBox;
    }

    public int SourceBox { get; }

    public int DestinationBox { get; }

    public OrganizationPlan CreateSamplePlan() =>
        new(
        [
            new SlotMove(
                new SlotPosition(SourceBox, 0),
                new SlotPosition(DestinationBox, 0)),
        ]);
}
