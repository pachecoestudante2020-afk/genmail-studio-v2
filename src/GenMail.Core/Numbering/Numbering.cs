using GenMail.Core.Models;

namespace GenMail.Core.Numbering;

public sealed class NumberRangeParser
{
    public IReadOnlyList<string> Parse(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return Array.Empty<string>();
        List<string> numbers = new List<string>();
        foreach (string part in pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                string[] range = part.Split('-', StringSplitOptions.TrimEntries);
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);
                int width = Math.Max(range[0].Length, range[1].Length);
                for (int i = start; i <= end; i++)
                {
                    numbers.Add(i.ToString($"D{width}"));
                }
            }
            else
            {
                numbers.Add(part);
            }
        }
        return numbers;
    }
}

public sealed class NumberExpansionService
{
    public IReadOnlyList<string> Expand(string baseUsername, IReadOnlyList<string> numbers, NumberMode mode, NumberPlacementMode placement)
    {
        HashSet<string> output = new HashSet<string>(StringComparer.Ordinal);
        if (mode != NumberMode.NumberedOnly)
        {
            output.Add(baseUsername);
        }

        if (mode != NumberMode.BaseOnly)
        {
            foreach (string num in numbers)
            {
                foreach (string value in Place(baseUsername, num, placement))
                {
                    output.Add(value);
                }
            }
        }

        return output.ToList();
    }

    private static IEnumerable<string> Place(string username, string number, NumberPlacementMode placement)
    {
        if (placement is NumberPlacementMode.SuffixOnly or NumberPlacementMode.SuffixAndPrefix or NumberPlacementMode.All)
        {
            yield return username + number;
        }

        if (placement is NumberPlacementMode.PrefixOnly or NumberPlacementMode.SuffixAndPrefix or NumberPlacementMode.All)
        {
            yield return number + username;
        }

        if (placement is NumberPlacementMode.InfixBeforeLastToken or NumberPlacementMode.All)
        {
            int idx = username.LastIndexOf('.');
            if (idx > 0)
            {
                yield return username[..idx] + number + username[idx..];
            }
            else
            {
                yield return username + number;
            }
        }
    }
}
