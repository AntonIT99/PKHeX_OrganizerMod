using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal static class LivingDexDefinitionProvider
{
    public static IReadOnlyList<LivingDexEntryDefinition> CreateDefinitions(
        SaveFile save,
        LivingDexMode mode,
        LivingDexShinyScope shinyScope)
    {
        ArgumentNullException.ThrowIfNull(save);
        var requiresForms = mode == LivingDexMode.Form ||
                            (mode == LivingDexMode.Shiny &&
                             shinyScope == LivingDexShinyScope.Form);
        var requiresShiny = mode == LivingDexMode.Shiny;
        var speciesNames = GameInfo.Strings.Species;
        var definitions = new List<LivingDexEntryDefinition>();

        // Scope: every National Dex species currently named by this PKHeX
        // checkout. Transfer compatibility is deliberately not inferred.
        for (var species = 1; species < speciesNames.Count; species++)
        {
            var speciesName = string.IsNullOrWhiteSpace(speciesNames[species])
                ? $"Species {species}"
                : speciesNames[species];
            if (!requiresForms)
            {
                definitions.Add(new LivingDexEntryDefinition(
                    new LivingDexEntryKey(species, 0, requiresShiny),
                    speciesName,
                    null));
                continue;
            }

            var formNames = GetFormNames((ushort)species, save.Context);
            for (var form = 0; form < formNames.Length && form <= byte.MaxValue; form++)
            {
                if (!IsCollectibleStoredForm(
                        (ushort)species,
                        (byte)form,
                        save.Context))
                {
                    continue;
                }

                var formName = form == 0
                    ? null
                    : string.IsNullOrWhiteSpace(formNames[form])
                        ? $"Form {form}"
                        : formNames[form];
                definitions.Add(new LivingDexEntryDefinition(
                    new LivingDexEntryKey(species, form, requiresShiny),
                    speciesName,
                    formName));
            }
        }

        return definitions;
    }

    public static string[] GetFormNames(ushort species, EntityContext context)
    {
        try
        {
            var names = FormConverter.GetFormList(
                species,
                GameInfo.Strings.Types,
                GameInfo.Strings.forms,
                context);
            return names.Length == 0 ? [string.Empty] : names;
        }
        catch
        {
            return [string.Empty];
        }
    }

    public static bool IsCollectibleStoredForm(
        ushort species,
        byte form,
        EntityContext context) =>
        !FormInfo.IsBattleOnlyForm(species, form, context.Generation) &&
        !FormInfo.IsFusedForm(species, form, context.Generation);
}
