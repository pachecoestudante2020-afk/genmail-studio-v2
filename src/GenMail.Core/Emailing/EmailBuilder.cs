using System.Text.RegularExpressions;

namespace GenMail.Core.Emailing;

public sealed class EmailBuilder
{
    private static readonly Regex DomainPattern = new Regex(@"^(?=.{3,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Build(string username, string domain)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        }

        string normalizedDomain = NormalizeAndValidateDomain(domain);
        return $"{username}@{normalizedDomain}";
    }

    public void ValidateDomain(string domain)
    {
        _ = NormalizeAndValidateDomain(domain);
    }

    public string NormalizeAndValidateDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain cannot be empty.", nameof(domain));
        }

        if (domain.Contains('@'))
        {
            throw new ArgumentException("Domain cannot contain @.", nameof(domain));
        }

        string normalized = domain.Trim().ToLowerInvariant();
        if (!DomainPattern.IsMatch(normalized))
        {
            throw new ArgumentException("Domain must be domain-like.", nameof(domain));
        }

        return normalized;
    }
}
