using System.Text.RegularExpressions;
using GenMail.Core.Models;

namespace GenMail.Core.Quality;

public sealed class UsernameQualityPolicy
{
    private static readonly Regex AllowedPattern = new Regex("^[a-z0-9._-]+$", RegexOptions.Compiled);

    public RejectionReason? Validate(string username, GenerationOptions options)
    {
        if (username is null || username.Length == 0)
        {
            return RejectionReason.Empty;
        }

        if (username.Any(char.IsWhiteSpace))
        {
            return RejectionReason.Whitespace;
        }

        if (username.Length < options.MinUsernameLength)
        {
            return RejectionReason.TooShort;
        }

        if (username.Length > options.MaxUsernameLength)
        {
            return RejectionReason.TooLong;
        }

        if (LooksLikeEmail(username))
        {
            return RejectionReason.LooksLikeEmail;
        }

        if (LooksLikeUrl(username))
        {
            return RejectionReason.LooksLikeUrl;
        }

        if (HasRepeatedSeparator(username))
        {
            return RejectionReason.RepeatedSeparator;
        }

        if (HasLeadingOrTrailingSeparator(username))
        {
            return RejectionReason.LeadingOrTrailingSeparator;
        }

        if (!AllowedPattern.IsMatch(username))
        {
            return RejectionReason.InvalidCharacter;
        }

        if (!options.AllowAllDigitsUsernames && username.All(char.IsDigit))
        {
            return RejectionReason.AllDigits;
        }

        return null;
    }

    private static bool HasRepeatedSeparator(string username) =>
        username.Contains("..", StringComparison.Ordinal) ||
        username.Contains("__", StringComparison.Ordinal) ||
        username.Contains("--", StringComparison.Ordinal);

    private static bool HasLeadingOrTrailingSeparator(string username)
    {
        return username.StartsWith('.') || username.StartsWith('_') || username.StartsWith('-') ||
               username.EndsWith('.') || username.EndsWith('_') || username.EndsWith('-');
    }

    private static bool LooksLikeEmail(string username)
    {
        int atIndex = username.IndexOf('@');
        return atIndex > 0 && atIndex < username.Length - 1;
    }

    private static bool LooksLikeUrl(string username)
    {
        return username.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               username.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               username.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }
}
