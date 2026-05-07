using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GenMail.Core.Models;

namespace GenMail.Core.Normalization;

public interface INameNormalizer
{
    NormalizedName Normalize(string input);
}

public interface IDirectUsernameDetector
{
    bool IsDirectUsername(string input);
}

public sealed partial class DefaultDirectUsernameDetector : IDirectUsernameDetector
{
    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex UsernamePattern();

    public bool IsDirectUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Contains(' ')) return false;
        if (input.Contains('@')) return false;
        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Scheme)) return false;
        return UsernamePattern().IsMatch(input);
    }
}

public sealed class DefaultNameNormalizer : INameNormalizer
{
    public NormalizedName Normalize(string input)
    {
        string compact = Regex.Replace(input.Trim(), "\\s+", " ");
        string lowered = RemoveVietnameseAccents(compact).ToLowerInvariant();
        string[] tokens = lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string first = tokens.Length > 0 ? tokens[0] : string.Empty;
        string last = tokens.Length > 1 ? tokens[^1] : first;
        string middle = tokens.Length > 2 ? string.Join(string.Empty, tokens.Skip(1).Take(tokens.Length - 2)) : string.Empty;
        string all = string.Join(string.Empty, tokens);
        string reverseAll = string.Join(string.Empty, tokens.Reverse());
        return new NormalizedName(input, lowered, first, middle, last, all, reverseAll);
    }

    public static string RemoveVietnameseAccents(string value)
    {
        string replaced = value.Replace('đ', 'd').Replace('Đ', 'D');
        string normalized = replaced.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
