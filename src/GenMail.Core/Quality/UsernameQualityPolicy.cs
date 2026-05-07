using System.Text.RegularExpressions;
using GenMail.Core.Models;

namespace GenMail.Core.Quality;

public sealed class UsernameQualityPolicy
{
    private static readonly Regex Allowed = new Regex("^[a-z0-9._-]+$", RegexOptions.Compiled);

    public RejectionReason? Validate(string username, GenerationOptions options)
    {
        if (string.IsNullOrWhiteSpace(username)) return RejectionReason.Empty;
        if (username.Length < options.MinUsernameLength) return RejectionReason.TooShort;
        if (username.Length > options.MaxUsernameLength) return RejectionReason.TooLong;
        if (!Allowed.IsMatch(username)) return RejectionReason.InvalidCharacters;
        if (username.Contains("..") || username.Contains("__") || username.Contains("--")) return RejectionReason.RepeatedSeparators;
        if (username.StartsWith('.') || username.StartsWith('_') || username.StartsWith('-') || username.EndsWith('.') || username.EndsWith('_') || username.EndsWith('-')) return RejectionReason.LeadingOrTrailingSeparator;
        if (username.Contains('@')) return RejectionReason.LooksLikeEmail;
        if (username.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || username.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return RejectionReason.LooksLikeUrl;
        if (!options.AllowAllDigitsUsernames && username.All(char.IsDigit)) return RejectionReason.AllDigitsDisallowed;
        return null;
    }
}
