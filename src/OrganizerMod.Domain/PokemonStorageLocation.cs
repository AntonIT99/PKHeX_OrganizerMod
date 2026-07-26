namespace OrganizerMod.Domain;

public enum PokemonStorageArea
{
    Party,
    Box,
    Pension,
}

public readonly record struct PokemonStorageLocation
{
    private PokemonStorageLocation(PokemonStorageArea area, int box, int facility, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        if (area == PokemonStorageArea.Box)
            ArgumentOutOfRangeException.ThrowIfNegative(box);
        if (area == PokemonStorageArea.Pension)
            ArgumentOutOfRangeException.ThrowIfNegative(facility);

        Area = area;
        Box = box;
        Facility = facility;
        Slot = slot;
    }

    public PokemonStorageArea Area { get; }

    public int Box { get; }

    public int Facility { get; }

    public int Slot { get; }

    public bool IsParty => Area == PokemonStorageArea.Party;

    public bool IsPension => Area == PokemonStorageArea.Pension;

    public static PokemonStorageLocation Party(int slot) =>
        new(PokemonStorageArea.Party, -1, -1, slot);

    public static PokemonStorageLocation BoxSlot(int box, int slot) =>
        new(PokemonStorageArea.Box, box, -1, slot);

    public static PokemonStorageLocation Pension(int facility, int slot) =>
        new(PokemonStorageArea.Pension, -1, facility, slot);
}
