namespace OrganizerMod.Domain;

public static class LivingDexBoxNameGenerator
{
    internal static IReadOnlyList<BoxRenameOperation> CreateRenames(
        IReadOnlyList<LivingDexBoxAssignment> layout,
        IReadOnlyDictionary<int, BoxState> boxStates,
        LivingDexOrganizerOptions options)
    {
        if (!options.RenameBoxes)
            return [];

        var mainBoxes = layout.Where(box => !box.IsOverflowOnly).ToArray();
        var overflowBoxes = layout.Where(box => box.IsOverflowOnly).ToArray();
        var mainBase = options.Mode switch
        {
            LivingDexMode.Species => "Living Dex",
            LivingDexMode.Form => "Form Dex",
            LivingDexMode.Shiny => "Shiny Dex",
            _ => throw new ArgumentOutOfRangeException(),
        };

        var result = new List<BoxRenameOperation>();
        AddNames(mainBoxes, mainBase, boxStates, options.MaximumBoxNameLength, result);
        AddNames(overflowBoxes, "Overflow", boxStates, options.MaximumBoxNameLength, result);
        return result;
    }

    private static void AddNames(
        IReadOnlyList<LivingDexBoxAssignment> boxes,
        string basis,
        IReadOnlyDictionary<int, BoxState> boxStates,
        int maximumLength,
        ICollection<BoxRenameOperation> result)
    {
        for (var index = 0; index < boxes.Count; index++)
        {
            var suffix = boxes.Count > 1 ? $" {index + 1}" : string.Empty;
            var newName = Fit(Sanitize(basis), suffix, maximumLength);
            var box = boxes[index];
            var original = boxStates[box.TargetBoxIndex].OriginalName;
            if (!string.Equals(original, newName, StringComparison.Ordinal))
                result.Add(new BoxRenameOperation(box.TargetBoxIndex, original, newName));
        }
    }

    private static string Sanitize(string value)
    {
        var sanitized = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return sanitized.Length == 0 ? "Dex" : sanitized;
    }

    private static string Fit(string basis, string suffix, int maximumLength)
    {
        if (suffix.Length >= maximumLength)
            return suffix[^maximumLength..];
        var basisLength = Math.Min(basis.Length, maximumLength - suffix.Length);
        return string.Concat(basis.AsSpan(0, basisLength), suffix);
    }
}
