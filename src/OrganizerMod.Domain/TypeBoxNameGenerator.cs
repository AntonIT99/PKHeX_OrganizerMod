using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public static class TypeBoxNameGenerator
{
    public static IReadOnlyDictionary<PokemonElementType, string> EnglishTypeNames { get; } =
        new ReadOnlyDictionary<PokemonElementType, string>(
            Enum.GetValues<PokemonElementType>().ToDictionary(type => type, type => type.ToString()));

    internal static IReadOnlyList<BoxRenameOperation> CreateRenames(
        IReadOnlyList<TypeBoxAssignment> boxes,
        IReadOnlyDictionary<int, BoxState> boxStates,
        TypeBoxOrganizerOptions options)
    {
        if (!options.RenameBoxes)
            return [];

        var typeCounts = boxes
            .Where(box => !box.IsMixed && !box.IsLegendary)
            .GroupBy(box => box.SharedType!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var typeOrdinals = new Dictionary<PokemonElementType, int>();
        var mixedCount = boxes.Count(box => box.IsMixed);
        var mixedOrdinal = 0;
        var legendaryCount = boxes.Count(box => box.IsLegendary);
        var legendaryOrdinal = 0;
        var result = new List<BoxRenameOperation>();

        foreach (var box in boxes)
        {
            string basis;
            string suffix;
            if (box.IsLegendary)
            {
                basis = "Legendary";
                suffix = legendaryCount > 1 ? $" {++legendaryOrdinal}" : string.Empty;
            }
            else if (box.IsMixed)
            {
                basis = "Mixed";
                suffix = mixedCount > 1 ? $" {++mixedOrdinal}" : string.Empty;
            }
            else
            {
                var type = box.SharedType!.Value;
                basis = options.TypeNames.TryGetValue(type, out var displayName) &&
                        !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : EnglishTypeNames[type];
                suffix = typeCounts[type] > 1
                    ? $" {typeOrdinals.GetValueOrDefault(type) + 1}"
                    : string.Empty;
                typeOrdinals[type] = typeOrdinals.GetValueOrDefault(type) + 1;
            }

            var newName = Fit(Sanitize(basis), suffix, options.MaximumBoxNameLength);
            var original = boxStates[box.TargetBoxIndex].OriginalName;
            if (!string.Equals(original, newName, StringComparison.Ordinal))
                result.Add(new BoxRenameOperation(box.TargetBoxIndex, original, newName));
        }

        return result;
    }

    private static string Sanitize(string value)
    {
        var sanitized = new string(value
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        return sanitized.Length == 0 ? "Type" : sanitized;
    }

    private static string Fit(string basis, string suffix, int maximumLength)
    {
        if (suffix.Length >= maximumLength)
            return suffix[^maximumLength..];

        var basisLength = Math.Min(basis.Length, maximumLength - suffix.Length);
        return string.Concat(basis.AsSpan(0, basisLength), suffix);
    }
}
