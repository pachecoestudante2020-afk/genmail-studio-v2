using GenMail.Core.Dedupe;
using GenMail.Core.Emailing;
using GenMail.Core.Generation;
using GenMail.Core.Models;
using GenMail.Core.Normalization;
using GenMail.Core.Numbering;
using GenMail.Core.Pipeline;
using GenMail.Core.Quality;
using GenMail.Core.Safety;
using Xunit;

namespace GenMail.Core.Tests;

public class CoreFeatureTests
{
    [Fact]
    public void VietnameseAccentRemoval_Works() => Assert.Equal("dang van lam", new DefaultNameNormalizer().Normalize("Đặng Văn Lâm").Lowered);

    [Fact]
    public void DirectUsernameDetector_AcceptsRejects()
    {
        DefaultDirectUsernameDetector d = new DefaultDirectUsernameDetector();
        Assert.True(d.IsDirectUsername("jdoe"));
        Assert.True(d.IsDirectUsername("john.smith"));
        Assert.False(d.IsDirectUsername("john@example.com"));
        Assert.False(d.IsDirectUsername("http://example.com"));
        Assert.False(d.IsDirectUsername("two words"));
    }

    [Fact]
    public void RuleCatalog_UniqueIds() => Assert.Throws<InvalidOperationException>(() => new RuleCatalog(new[] { new TemplateUsernameRule("a", "{first}"), new TemplateUsernameRule("a", "{last}") }));

    [Fact]
    public void TemplateRule_Rendering()
    {
        NormalizedName n = new DefaultNameNormalizer().Normalize("John Michael Smith");
        Assert.Equal("j.smith", new TemplateUsernameRule("x", "{fi}.{last}").Apply(n));
    }

    [Fact]
    public void UsernameGenerator_RemovesDuplicates()
    {
        NormalizedName n = new DefaultNameNormalizer().Normalize("John John");
        UsernameGenerator g = new UsernameGenerator();
        IReadOnlyList<UsernameCandidate> list = g.Generate(n, new IUsernameRule[] { new TemplateUsernameRule("a", "{first}"), new TemplateUsernameRule("b", "{first}") });
        Assert.Single(list);
    }

    [Fact]
    public void NumberRangeParser_Padded()
    {
        IReadOnlyList<string> vals = new NumberRangeParser().Parse("001-003");
        Assert.Equal(new[] { "001", "002", "003" }, vals);
    }

    [Fact]
    public void NumberExpansion_Works()
    {
        NumberExpansionService s = new NumberExpansionService();
        IReadOnlyList<string> e = s.Expand("john.smith", new[] { "99" }, NumberMode.NumberedOnly, NumberPlacementMode.InfixBeforeLastToken);
        Assert.Contains("john99.smith", e);
    }

    [Fact]
    public void UsernameQualityPolicy_RejectsBadPatterns()
    {
        UsernameQualityPolicy q = new UsernameQualityPolicy();
        GenerationOptions o = new GenerationOptions("example.com", "out");
        Assert.Equal(RejectionReason.RepeatedSeparators, q.Validate("john..smith", o));
        Assert.Equal(RejectionReason.InvalidCharacters, q.Validate("john@example.com", o));
    }

    [Fact]
    public void EmailBuilder_ValidatesDomain()
    {
        EmailBuilder b = new EmailBuilder();
        Assert.Throws<ArgumentException>(() => b.ValidateDomain(""));
        Assert.Equal("john@example.com", b.Build("john", "example.com"));
    }

    [Fact]
    public async Task InMemoryDedupe_SkipsDuplicates()
    {
        await using InMemoryDedupeStore s = new InMemoryDedupeStore();
        Assert.True(await s.TryAddAsync(new DedupeEntry("g", "u", "john"), CancellationToken.None));
        Assert.False(await s.TryAddAsync(new DedupeEntry("g", "u", "john"), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteDedupe_PersistsAcrossInstances()
    {
        string path = Path.GetTempFileName();
        await using (SqliteDedupeStore a = new SqliteDedupeStore(path))
        {
            Assert.True(await a.TryAddAsync(new DedupeEntry("g", "u", "john"), CancellationToken.None));
        }
        await using (SqliteDedupeStore b = new SqliteDedupeStore(path))
        {
            Assert.False(await b.TryAddAsync(new DedupeEntry("g", "u", "john"), CancellationToken.None));
        }
    }

    [Fact]
    public void SafetyGuard_RejectsHuge()
    {
        SafetyGuard g = new SafetyGuard();
        GenerationOptions o = new GenerationOptions("example.com", "out", MaxOutputEmails: 10);
        Assert.Throws<InvalidOperationException>(() => g.EnsureWithinLimits(new SafetyEstimate(100, 2, 1, 1000), o));
    }

    [Fact]
    public async Task Pipeline_SmallIntegration()
    {
        string input = Path.Combine(Path.GetTempPath(), $"gm_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(input, new[] { "John Smith", "jdoe" });
        GenMailPipeline p = new GenMailPipeline();
        GenerationOptions o = new GenerationOptions("example.com", Path.Combine(Path.GetTempPath(), "gm_out"), NumberMode: NumberMode.BaseOnly, SelectedRuleIds: new[] { "firstlast" });
        ProcessingResult r = await p.RunAsync(input, o, null, CancellationToken.None);
        Assert.True(r.Counters.EmailsGenerated >= 2);
    }
}
