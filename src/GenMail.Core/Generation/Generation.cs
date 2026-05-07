using System.Text.RegularExpressions;
using GenMail.Core.Models;

namespace GenMail.Core.Generation;

public interface IUsernameRule
{
    string Id { get; }
    string Apply(NormalizedName name);
}

public sealed class TemplateUsernameRule : IUsernameRule
{
    public TemplateUsernameRule(string id, string template)
    {
        Id = id;
        Template = template;
    }

    public string Id { get; }
    public string Template { get; }

    public string Apply(NormalizedName name)
    {
        Dictionary<string, string> map = new Dictionary<string, string>
        {
            ["{first}"] = name.First,
            ["{last}"] = name.Last,
            ["{middle}"] = name.Middle,
            ["{all}"] = name.All,
            ["{reverseAll}"] = name.ReverseAll,
            ["{fi}"] = Slice(name.First, 1),
            ["{li}"] = Slice(name.Last, 1),
            ["{mi}"] = Slice(name.Middle, 1),
            ["{rmi}"] = Slice(new string(name.Middle.Reverse().ToArray()), 1),
            ["{first2}"] = Slice(name.First, 2),
            ["{first3}"] = Slice(name.First, 3),
            ["{first4}"] = Slice(name.First, 4),
            ["{last2}"] = Slice(name.Last, 2),
            ["{last3}"] = Slice(name.Last, 3),
            ["{last4}"] = Slice(name.Last, 4),
        };

        string value = Template;
        foreach (KeyValuePair<string, string> pair in map)
        {
            value = value.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        return Regex.Replace(value, "\\s+", string.Empty);
    }

    private static string Slice(string value, int count) => value.Length <= count ? value : value[..count];
}

public sealed class RuleCatalog
{
    private readonly Dictionary<string, IUsernameRule> _rules;

    public RuleCatalog(IEnumerable<IUsernameRule> rules)
    {
        _rules = rules.ToDictionary(x => x.Id, StringComparer.Ordinal);
        if (_rules.Count != rules.Count())
        {
            throw new InvalidOperationException("Rule IDs must be unique.");
        }
    }

    public IReadOnlyCollection<IUsernameRule> All => _rules.Values;
    public IUsernameRule GetById(string id) => _rules[id];
}

public static class BuiltInUsernameRules
{
    public static IReadOnlyList<IUsernameRule> CreateDefault()
    {
        return new List<IUsernameRule>
        {
            new TemplateUsernameRule("first", "{first}"),
            new TemplateUsernameRule("last", "{last}"),
            new TemplateUsernameRule("firstlast", "{first}{last}"),
            new TemplateUsernameRule("lastfirst", "{last}{first}"),
            new TemplateUsernameRule("first.last", "{first}.{last}"),
            new TemplateUsernameRule("last.first", "{last}.{first}"),
            new TemplateUsernameRule("first_last", "{first}_{last}"),
            new TemplateUsernameRule("last_first", "{last}_{first}"),
            new TemplateUsernameRule("first-last", "{first}-{last}"),
            new TemplateUsernameRule("last-first", "{last}-{first}"),
            new TemplateUsernameRule("flast", "{fi}{last}"),
            new TemplateUsernameRule("firstl", "{first}{li}"),
            new TemplateUsernameRule("f.last", "{fi}.{last}"),
            new TemplateUsernameRule("first.l", "{first}.{li}"),
            new TemplateUsernameRule("firstmiddlelast", "{first}{middle}{last}"),
            new TemplateUsernameRule("first.middle.last", "{first}.{middle}.{last}"),
            new TemplateUsernameRule("all", "{all}"),
            new TemplateUsernameRule("all.dot", "{first}.{middle}.{last}"),
            new TemplateUsernameRule("reverse.all", "{reverseAll}"),
            new TemplateUsernameRule("first3last", "{first3}{last}"),
            new TemplateUsernameRule("firstlast3", "{first}{last3}"),
            new TemplateUsernameRule("first3last3", "{first3}{last3}"),
        };
    }
}

public sealed class UsernameGenerator
{
    public IReadOnlyList<UsernameCandidate> Generate(NormalizedName name, IEnumerable<IUsernameRule> rules)
    {
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        List<UsernameCandidate> results = new List<UsernameCandidate>();
        foreach (IUsernameRule rule in rules)
        {
            string value = rule.Apply(name);
            if (unique.Add(value))
            {
                results.Add(new UsernameCandidate(rule.Id, value));
            }
        }
        return results;
    }
}
