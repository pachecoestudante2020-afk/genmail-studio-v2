using GenMail.Core.Models;

namespace GenMail.Core.Quality;

public sealed class UsernameQualityPolicy
{
    public RejectionReason? Validate(string username, GenerationOptions options)
    {
        if (username is null || username.Length == 0) return RejectionReason.Empty;
        if (username.Any(char.IsWhiteSpace)) return RejectionReason.Whitespace;
        if (username.Length < options.MinUsernameLength) return RejectionReason.TooShort;
        if (username.Length > options.MaxUsernameLength) return RejectionReason.TooLong;
        if (LooksLikeEmail(username)) return RejectionReason.LooksLikeEmail;
        if (LooksLikeUrl(username)) return RejectionReason.LooksLikeUrl;
        if (HasLeadingOrTrailingSeparator(username)) return RejectionReason.LeadingOrTrailingSeparator;
        if (HasRepeatedSeparator(username)) return RejectionReason.RepeatedSeparator;
        if (!HasOnlyAllowedCharacters(username)) return RejectionReason.InvalidCharacter;
        if (!options.AllowAllDigitsUsernames && username.All(char.IsDigit)) return RejectionReason.AllDigits;
        return null;
    }

    private static bool HasOnlyAllowedCharacters(string username)
    {
        foreach (char c in username)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c is '.' or '_' or '-') continue;
            return false;
        }
        return true;
    }

    private static bool HasRepeatedSeparator(string username)
        => username.Contains("..", StringComparison.Ordinal) || username.Contains("__", StringComparison.Ordinal) || username.Contains("--", StringComparison.Ordinal);

    private static bool HasLeadingOrTrailingSeparator(string username)
        => username.StartsWith('.') || username.StartsWith('_') || username.StartsWith('-') || username.EndsWith('.') || username.EndsWith('_') || username.EndsWith('-');

    private static bool LooksLikeEmail(string username)
    {
        int at = username.IndexOf('@');
        return at > 0 && at < username.Length - 1;
    }

    private static bool LooksLikeUrl(string username)
        => username.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || username.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || username.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
}
