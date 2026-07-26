using System.Collections.ObjectModel;
using System.Security.Cryptography;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class PkmDatabaseImportSession(
    SaveFile save,
    string databasePath,
    DatabaseImportPlan plan,
    IReadOnlyDictionary<string, PKM> convertedPokemon,
    IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
    IReadOnlyDictionary<(ExistingPokemonArea Area, int Facility, int Slot), string> supplementalFingerprints,
    int partyCountAtPreview,
    IReadOnlyDictionary<int, string> boxNames,
    IReadOnlyDictionary<string, string> sourceFingerprints,
    IReadOnlyDictionary<int, string> originGameNames)
{
    public SaveFile Save { get; } = save;
    public string DatabasePath { get; } = databasePath;
    public DatabaseImportPlan Plan { get; } = plan;
    public IReadOnlyDictionary<string, PKM> ConvertedPokemon { get; } = convertedPokemon;
    public IReadOnlyDictionary<(int Box, int Slot), string> SlotFingerprints { get; } = slotFingerprints;
    public IReadOnlyDictionary<(ExistingPokemonArea Area, int Facility, int Slot), string> SupplementalFingerprints { get; } = supplementalFingerprints;
    public int PartyCountAtPreview { get; } = partyCountAtPreview;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
    public IReadOnlyDictionary<string, string> SourceFingerprints { get; } = sourceFingerprints;
    public IReadOnlyDictionary<int, string> OriginGameNames { get; } = originGameNames;
}

internal sealed class PkmDatabaseImportService(ISaveFileProvider saveFileProvider)
{
    private readonly PkmDatabaseImportPlanner planner = new();

    public static string ResolveConfiguredDatabasePath()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("PKHeX.WinForms.Main", false))
            .FirstOrDefault(item => item is not null)
            ?? throw new InvalidOperationException("The running PKHeX WinForms application was not found.");
        return type.GetProperty("DatabasePath")?.GetValue(null) as string
            ?? throw new InvalidOperationException("PKHeX did not expose its configured PKM database path.");
    }

    public IReadOnlyList<BoxSelectionItem> GetBoxSelection() =>
        new LivingDexOrganizationService(saveFileProvider).GetBoxSelection();

    public Task<PkmDatabaseImportSession> CreatePlanAsync(
        string databasePath,
        IReadOnlyCollection<int> selectedBoxIndices,
        SamePidImportMode pidMode,
        SameSpeciesShinyImportMode speciesMode,
        SpeciesShinyGroupingMode speciesShinyGrouping,
        PkmDatabaseFilterOptions filters,
        bool includeTeamInPidComparison,
        bool includePensionInPidComparison,
        bool allowTeamReplacements,
        bool useTeamSlotsForNewImports,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => CreatePlan(databasePath, selectedBoxIndices, pidMode, speciesMode, speciesShinyGrouping, filters,
            includeTeamInPidComparison, includePensionInPidComparison, allowTeamReplacements, useTeamSlotsForNewImports,
            cancellationToken), cancellationToken);

    public void Apply(PkmDatabaseImportSession session)
    {
        if (!session.Plan.IsValid)
            throw new InvalidOperationException("An invalid database-import plan cannot be applied.");
        var save = saveFileProvider.SAV;
        if (!ReferenceEquals(save, session.Save))
            throw new InvalidOperationException("A different save was loaded after the preview. Rescan before applying.");
        SafeOrganizationApplier.ValidateStillMatches(save, session.SlotFingerprints, session.BoxNames);
        ValidateSupplementalStillMatches(save, session);
        foreach (var pair in session.SourceFingerprints)
        {
            if (!File.Exists(pair.Key) || Fingerprint(File.ReadAllBytes(pair.Key)) != pair.Value)
                throw new InvalidOperationException($"Database source changed after scanning: {pair.Key}. Rescan before applying.");
        }
        var backup = save.Clone();
        var wasEdited = save.State.Edited;
        try
        {
            foreach (var replacement in session.Plan.Replacements)
                Write(save, session, replacement.Candidate.StableId, replacement.Existing.Area, replacement.Existing.BoxIndex, replacement.Existing.SlotIndex);
            foreach (var import in session.Plan.Imports)
                Write(save, session, import.Candidate.StableId, import.Destination.Area, import.Destination.BoxIndex, import.Destination.SlotIndex);
            save.State.Edited = true;
            saveFileProvider.ReloadSlots();
        }
        catch
        {
            save.CopyChangesFrom(backup);
            save.State.Edited = wasEdited;
            saveFileProvider.ReloadSlots();
            throw;
        }
    }

    private PkmDatabaseImportSession CreatePlan(
        string databasePath,
        IReadOnlyCollection<int> selectedBoxIndices,
        SamePidImportMode pidMode,
        SameSpeciesShinyImportMode speciesMode,
        SpeciesShinyGroupingMode speciesShinyGrouping,
        PkmDatabaseFilterOptions filters,
        bool includeTeamInPidComparison,
        bool includePensionInPidComparison,
        bool allowTeamReplacements,
        bool useTeamSlotsForNewImports,
        CancellationToken token)
    {
        var save = saveFileProvider.SAV;
        if (!save.HasBox) throw new NotSupportedException("The loaded save does not provide storage boxes.");
        if (!Directory.Exists(databasePath)) throw new DirectoryNotFoundException($"PKM database directory does not exist: {databasePath}");
        var choices = GetBoxSelection().ToDictionary(x => x.BoxIndex);
        var selected = selectedBoxIndices.Distinct().Order().ToArray();
        foreach (var box in selected)
            if (!choices.TryGetValue(box, out var item) || !item.IsAvailable)
                throw new InvalidOperationException($"Box {box + 1} is unavailable.");

        string[] files;
        try { files = Directory.EnumerateFiles(databasePath, "*", SearchOption.AllDirectories).OrderBy(x => Path.GetRelativePath(databasePath, x), StringComparer.OrdinalIgnoreCase).ToArray(); }
        catch (Exception ex) { throw new IOException($"PKM database could not be read: {ex.Message}", ex); }
        var database = new List<DatabasePokemonCandidate>();
        var converted = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var sourceFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var unreadable = 0;
        var entityHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(databasePath, file).Replace(Path.DirectorySeparatorChar, '/');
            try
            {
                var info = new FileInfo(file);
                if (!EntityDetection.IsSizePlausible(info.Length)) continue;
                var data = File.ReadAllBytes(file);
                if (!FileUtil.TryGetPKM(data, out var pk, info.Extension, save) || pk.Species == 0)
                {
                    unreadable++; warnings.Add($"{relative}: supported-size file could not be parsed as a Pokémon."); continue;
                }
                var fileHash = Fingerprint(data);
                sourceFingerprints[file] = fileHash;
                if (entityHashes.TryGetValue(fileHash, out var prior))
                    warnings.Add($"{relative}: serialized entity is identical to {prior}.");
                else entityHashes[fileHash] = relative;
                bool? legal = null;
                if (filters.Legality == LegalityFilterMode.OnlyLegal)
                {
                    try { legal = new LegalityAnalysis(pk, save.Personal).Valid; } catch { legal = false; }
                }
                var output = EntityConverter.ConvertToType(pk.Clone(), save.PKMType, out var conversion);
                var compatible = output is not null && conversion is EntityConverterResult.None or EntityConverterResult.Success;
                var stableId = $"{relative}:{fileHash}";
                database.Add(new(stableId, relative, pk.PID, pk.Species, pk.Form, pk.IsShiny, pk.CurrentLevel, pk.EXP,
                    (int)pk.Version, ToGender(pk.Gender), legal, compatible));
                if (compatible) converted[stableId] = output!;
            }
            catch (Exception ex) { unreadable++; warnings.Add($"{relative}: {ex.Message}"); }
        }
        if (database.Count == 0) throw new InvalidOperationException("The database contains no supported Pokémon files.");

        var existing = new List<ExistingSavePokemon>();
        var empty = new List<EmptySaveSlot>();
        var fingerprints = new Dictionary<(int, int), string>();
        var names = selected.ToDictionary(box => box, box => OrganizationStorageUtilities.GetBoxName(save, box));
        foreach (var box in selected)
        for (var slot = 0; slot < save.BoxSlotCount; slot++)
        {
            var pk = save.GetBoxSlotAtIndex(box, slot);
            var fingerprint = OrganizationStorageUtilities.Fingerprint(pk);
            fingerprints[(box, slot)] = fingerprint;
            if (pk.Species == 0) empty.Add(new(box, slot));
            else existing.Add(new($"{box:D3}:{slot:D2}:{fingerprint}", pk.PID, pk.Species, pk.Form, pk.IsShiny, pk.CurrentLevel, pk.EXP,
                (int)pk.Version, ToGender(pk.Gender), box, slot));
        }
        if (useTeamSlotsForNewImports && save.HasParty)
        {
            for (var slot = save.PartyCount; slot < 6; slot++)
                empty.Add(new(-1, slot, ExistingPokemonArea.Team));
        }
        var supplementalFingerprints = new Dictionary<(ExistingPokemonArea, int, int), string>();
        var needsTeamSnapshot = includeTeamInPidComparison || useTeamSlotsForNewImports;
        ReadSupplementalPokemon(save, needsTeamSnapshot, includePensionInPidComparison, existing, supplementalFingerprints);
        var options = new PkmDatabaseImportOptions(pidMode, speciesMode, filters, new HashSet<int>(selected),
            includeTeamInPidComparison, includePensionInPidComparison, speciesShinyGrouping,
            allowTeamReplacements, useTeamSlotsForNewImports);
        var plan = planner.CreatePlan(database, existing, empty, options, files.Length, unreadable, warnings);
        var games = DuplicateSpeciesRemovalService.GetOriginGames().ToDictionary(x => x.Id, x => x.Name);
        return new(save, databasePath, plan, new ReadOnlyDictionary<string, PKM>(converted),
            new ReadOnlyDictionary<(int, int), string>(fingerprints),
            new ReadOnlyDictionary<(ExistingPokemonArea, int, int), string>(supplementalFingerprints),
            save.PartyCount,
            new ReadOnlyDictionary<int, string>(names),
            new ReadOnlyDictionary<string, string>(sourceFingerprints), new ReadOnlyDictionary<int, string>(games));
    }

    private static void ValidateSupplementalStillMatches(SaveFile save, PkmDatabaseImportSession session)
    {
        var includesTeam = session.Plan.Options.IncludeTeamInPidComparison || session.Plan.Options.UseTeamSlotsForNewImports;
        if (!includesTeam && !session.Plan.Options.IncludePensionInPidComparison)
            return;
        if (includesTeam && save.PartyCount != session.PartyCountAtPreview)
            throw new InvalidOperationException("The Team changed after the preview. Rescan before applying.");
        var current = new Dictionary<(ExistingPokemonArea, int, int), string>();
        ReadSupplementalPokemon(save, includesTeam,
            session.Plan.Options.IncludePensionInPidComparison, [], current);
        if (current.Count != session.SupplementalFingerprints.Count ||
            current.Any(pair => !session.SupplementalFingerprints.TryGetValue(pair.Key, out var expected) || expected != pair.Value))
            throw new InvalidOperationException("The Team or Pension changed after the preview. Rescan before applying.");
    }

    private static void ReadSupplementalPokemon(
        SaveFile save,
        bool includeTeam,
        bool includePension,
        ICollection<ExistingSavePokemon> existing,
        IDictionary<(ExistingPokemonArea, int, int), string> fingerprints)
    {
        if (includeTeam && save.HasParty)
        {
            for (var slot = 0; slot < save.PartyCount; slot++)
                AddSupplemental(save.GetPartySlotAtIndex(slot), ExistingPokemonArea.Team, 0, slot, existing, fingerprints);
        }

        if (!includePension)
            return;

        var facility = 0;
        if (save is IDaycareMulti multiple)
        {
            for (var index = 0; index < multiple.DaycareCount; index++)
                ReadPension(save, multiple[index], facility++, existing, fingerprints);
        }
        else if (save is IDaycareStorage pension)
        {
            ReadPension(save, pension, facility++, existing, fingerprints);
        }

        // Some formats expose pension storage only through PKHeX's extra-slot API.
        if (facility != 0)
            return;
        var extras = save.GetExtraSlots().Where(x => x.Type == StorageSlotType.Daycare).ToArray();
        for (var slot = 0; slot < extras.Length; slot++)
            AddSupplemental(extras[slot].Read(save), ExistingPokemonArea.Pension, 0, slot, existing, fingerprints);
    }

    private static void ReadPension(
        SaveFile save,
        IDaycareStorage pension,
        int facility,
        ICollection<ExistingSavePokemon> existing,
        IDictionary<(ExistingPokemonArea, int, int), string> fingerprints)
    {
        for (var slot = 0; slot < pension.DaycareSlotCount; slot++)
        {
            if (!pension.IsDaycareOccupied(slot))
                continue;
            AddSupplemental(save.GetStoredSlot(pension.GetDaycareSlot(slot).Span), ExistingPokemonArea.Pension,
                facility, slot, existing, fingerprints);
        }
    }

    private static void AddSupplemental(
        PKM pk,
        ExistingPokemonArea area,
        int facility,
        int slot,
        ICollection<ExistingSavePokemon> existing,
        IDictionary<(ExistingPokemonArea, int, int), string> fingerprints)
    {
        if (pk.Species == 0)
            return;
        var fingerprint = OrganizationStorageUtilities.Fingerprint(pk);
        fingerprints[(area, facility, slot)] = fingerprint;
        existing.Add(new($"{area}:{facility:D2}:{slot:D2}:{fingerprint}", pk.PID, pk.Species, pk.Form, pk.IsShiny,
            pk.CurrentLevel, pk.EXP, (int)pk.Version, ToGender(pk.Gender), -1, slot, area, facility));
    }

    private static void Write(SaveFile save, PkmDatabaseImportSession session, string id, ExistingPokemonArea area, int box, int slot)
    {
        if (!session.ConvertedPokemon.TryGetValue(id, out var pk))
            throw new InvalidOperationException($"Converted database Pokémon is missing: {id}");
        if (area == ExistingPokemonArea.Team)
            save.SetPartySlotAtIndex(pk.Clone(), slot, EntityImportSettings.None);
        else if (area == ExistingPokemonArea.Box)
            save.SetBoxSlotAtIndex(pk.Clone(), box, slot, EntityImportSettings.None);
        else
            throw new InvalidOperationException("Pension storage cannot be used as an import destination.");
    }
    private static PokemonGenderPreference ToGender(byte value) => value switch { 0 => PokemonGenderPreference.Male, 1 => PokemonGenderPreference.Female, _ => PokemonGenderPreference.Genderless };
    private static string Fingerprint(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));
}
