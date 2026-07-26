namespace OrganizerMod.Domain;

public sealed record DuplicatePokemon(
    uint PersonalityId,
    ushort Species,
    byte Level,
    uint Experience,
    PokemonStorageLocation Location);
