using System.Text.RegularExpressions;

namespace GenMail.Core.Emailing;

public sealed class EmailBuilder
{
    private static readonly Regex DomainPattern = new Regex("^[a-zA-Z0-9-]+(\\.[a-zA-Z0-9-]+)+$", RegexOptions.Compiled);

    public string Build(string username, string domain)
    {
        ValidateDomain(domain);
        return $"{username}@{domain}";
    }

    public void ValidateDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Domain cannot be empty.");
        if (domain.Contains('@')) throw new ArgumentException("Domain cannot contain @.");
        if (!DomainPattern.IsMatch(domain)) throw new ArgumentException("Domain must be domain-like.");
    }
}
