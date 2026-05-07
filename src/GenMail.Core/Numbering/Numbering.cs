using GenMail.Core.Models;

namespace GenMail.Core.Numbering;

public sealed class NumberRangeParser
{
    public IReadOnlyList<string> Parse(string pattern, int maxNumbersPerBase = 1_000)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Array.Empty<string>();
        }

        List<string> output = new List<string>();
        string[] parts = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                ParseRange(part, output, maxNumbersPerBase);
            }
            else
            {
                EnsureNumericToken(part);
                output.Add(part);
                EnsureLimit(output.Count, maxNumbersPerBase);
            }
        }

        return output;
    }

    private static void ParseRange(string part, List<string> output, int maxNumbersPerBase)
    {
        string[] bounds = part.Split('-', StringSplitOptions.TrimEntries);
        if (bounds.Length != 2)
        {
            throw new ArgumentException($"Invalid range segment: '{part}'.");
        }

        EnsureNumericToken(bounds[0]);
        EnsureNumericToken(bounds[1]);

        int start = int.Parse(bounds[0]);
        int end = int.Parse(bounds[1]);
        if (end < start)
        {
            throw new ArgumentException($"Invalid descending range: '{part}'.");
        }

        int width = Math.Max(bounds[0].Length, bounds[1].Length);
        int count = (end - start) + 1;
        EnsureLimit(output.Count + count, maxNumbersPerBase);

        for (int value = start; value <= end; value++)
        {
            output.Add(value.ToString($"D{width}"));
        }
    }

    private static void EnsureNumericToken(string token)
    {
        if (token.Length == 0 || token.Any(static c => !char.IsDigit(c)))
        {
            throw new ArgumentException($"Invalid number token: '{token}'.");
        }
    }

    private static void EnsureLimit(int count, int maxNumbersPerBase)
    {
        if (count > maxNumbersPerBase)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNumbersPerBase), $"Number pattern expands to {count} values, exceeding max {maxNumbersPerBase}.");
        }
    }
}

public sealed class NumberExpansionService
{
    public IReadOnlyList<string> Expand(string baseUsername, IReadOnlyList<string> numbers, NumberMode mode, NumberPlacementMode placement)
    {
        HashSet<string> output = new HashSet<string>(StringComparer.Ordinal);

        if (mode is NumberMode.BaseOnly or NumberMode.BaseAndNumbered)
        {
            output.Add(baseUsername);
        }

        if (mode is NumberMode.NumberedOnly or NumberMode.BaseAndNumbered)
        {
            foreach (string number in numbers)
            {
                foreach (string placed in Place(baseUsername, number, placement))
                {
                    output.Add(placed);
                }
            }
        }

        return output.ToList();
    }

    private static IEnumerable<string> Place(string baseUsername, string number, NumberPlacementMode placement)
    {
        if (placement is NumberPlacementMode.SuffixOnly or NumberPlacementMode.SuffixAndPrefix or NumberPlacementMode.All)
        {
            yield return baseUsername + number;
        }

        if (placement is NumberPlacementMode.PrefixOnly or NumberPlacementMode.SuffixAndPrefix or NumberPlacementMode.All)
        {
            yield return number + baseUsername;
        }

        if (placement is NumberPlacementMode.InfixBeforeLastToken or NumberPlacementMode.All)
        {
            int lastDot = baseUsername.LastIndexOf('.');
            if (lastDot > 0)
            {
                yield return baseUsername[..lastDot] + number + baseUsername[lastDot..];
            }
            else
            {
                yield return baseUsername + number;
            }
        }
    }
}
