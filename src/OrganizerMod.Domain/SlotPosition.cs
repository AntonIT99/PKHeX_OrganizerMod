namespace OrganizerMod.Domain;

public readonly record struct SlotPosition
{
    public SlotPosition(int box, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(box);
        ArgumentOutOfRangeException.ThrowIfNegative(slot);

        Box = box;
        Slot = slot;
    }

    public int Box { get; }

    public int Slot { get; }
}
