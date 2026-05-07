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
        Assert.Equal(RejectionReason.RepeatedSeparator, q.Validate("john..smith", o));
        Assert.Equal(RejectionReason.LooksLikeEmail, q.Validate("john@example.com", o));
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
        Assert.True(await s.TryAddAsync(new DedupeEntry("g", "u", "john", "john", "john@example.com", "src", DateTimeOffset.UtcNow), CancellationToken.None));
        Assert.False(await s.TryAddAsync(new DedupeEntry("g", "u", "john", "john", "john@example.com", "src", DateTimeOffset.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteDedupe_PersistsAcrossInstances()
    {
        string path = Path.GetTempFileName();
        await using (SqliteDedupeStore a = new SqliteDedupeStore(path))
        {
            Assert.True(await a.TryAddAsync(new DedupeEntry("g", "u", "john", "john", "john@example.com", "src", DateTimeOffset.UtcNow), CancellationToken.None));
        }
        await using (SqliteDedupeStore b = new SqliteDedupeStore(path))
        {
            Assert.False(await b.TryAddAsync(new DedupeEntry("g", "u", "john", "john", "john@example.com", "src", DateTimeOffset.UtcNow), CancellationToken.None));
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


    [Fact]
    public void NumberRangeParser_Padded00_02()
    {
        IReadOnlyList<string> vals = new NumberRangeParser().Parse("00-02");
        Assert.Equal(new[] { "00", "01", "02" }, vals);
    }

    [Fact]
    public void NumberRangeParser_CustomPadded001_003()
    {
        IReadOnlyList<string> vals = new NumberRangeParser().Parse("001-003");
        Assert.Equal(new[] { "001", "002", "003" }, vals);
    }

    [Fact]
    public void NumberRangeParser_CommaList()
    {
        IReadOnlyList<string> vals = new NumberRangeParser().Parse("1,2,3,10");
        Assert.Equal(new[] { "1", "2", "3", "10" }, vals);
    }

    [Fact]
    public void NumberRangeParser_Mixed()
    {
        IReadOnlyList<string> vals = new NumberRangeParser().Parse("01-03,99");
        Assert.Equal(new[] { "01", "02", "03", "99" }, vals);
    }

    [Fact]
    public void NumberRangeParser_InvalidDescendingRange_Rejects()
    {
        Assert.Throws<ArgumentException>(() => new NumberRangeParser().Parse("10-01"));
    }

    [Fact]
    public void NumberExpansion_Suffix()
    {
        IReadOnlyList<string> vals = new NumberExpansionService().Expand("john", new[] { "00", "01" }, NumberMode.NumberedOnly, NumberPlacementMode.SuffixOnly);
        Assert.Equal(new[] { "john00", "john01" }, vals);
    }

    [Fact]
    public void NumberExpansion_Prefix()
    {
        IReadOnlyList<string> vals = new NumberExpansionService().Expand("john", new[] { "1" }, NumberMode.NumberedOnly, NumberPlacementMode.PrefixOnly);
        Assert.Contains("1john", vals);
    }

    [Fact]
    public void NumberExpansion_Infix()
    {
        IReadOnlyList<string> vals = new NumberExpansionService().Expand("john.smith", new[] { "99" }, NumberMode.NumberedOnly, NumberPlacementMode.InfixBeforeLastToken);
        Assert.Contains("john99.smith", vals);
    }

    [Fact]
    public void NumberExpansion_BaseOnly()
    {
        IReadOnlyList<string> vals = new NumberExpansionService().Expand("john", new[] { "01" }, NumberMode.BaseOnly, NumberPlacementMode.SuffixOnly);
        Assert.Equal(new[] { "john" }, vals);
    }

    [Fact]
    public void NumberExpansion_BaseAndNumbered()
    {
        IReadOnlyList<string> vals = new NumberExpansionService().Expand("john", new[] { "01" }, NumberMode.BaseAndNumbered, NumberPlacementMode.SuffixOnly);
        Assert.Contains("john", vals);
        Assert.Contains("john01", vals);
    }



    [Fact]
    public void Quality_EmptyUsernameRejected()
    {
        UsernameQualityPolicy q = new UsernameQualityPolicy();
        Assert.Equal(RejectionReason.Empty, q.Validate(string.Empty, new GenerationOptions("example.com", "out")));
    }

    [Fact]
    public void Quality_TooShortAndTooLongRejected()
    {
        UsernameQualityPolicy q = new UsernameQualityPolicy();
        GenerationOptions o = new GenerationOptions("example.com", "out", MinUsernameLength: 3, MaxUsernameLength: 5);
        Assert.Equal(RejectionReason.TooShort, q.Validate("ab", o));
        Assert.Equal(RejectionReason.TooLong, q.Validate("abcdef", o));
    }

    [Fact]
    public void Quality_InvalidRepeatedLeadingTrailingRejected()
    {
        UsernameQualityPolicy q = new UsernameQualityPolicy();
        GenerationOptions o = new GenerationOptions("example.com", "out");
        Assert.Equal(RejectionReason.InvalidCharacter, q.Validate("john$", o));
        Assert.Equal(RejectionReason.RepeatedSeparator, q.Validate("john..smith", o));
        Assert.Equal(RejectionReason.LeadingOrTrailingSeparator, q.Validate(".john", o));
        Assert.Equal(RejectionReason.LeadingOrTrailingSeparator, q.Validate("john-", o));
    }

    [Fact]
    public void Quality_EmailUrlAndDigitsRules()
    {
        UsernameQualityPolicy q = new UsernameQualityPolicy();
        GenerationOptions disabled = new GenerationOptions("example.com", "out", AllowAllDigitsUsernames: false);
        GenerationOptions enabled = new GenerationOptions("example.com", "out", AllowAllDigitsUsernames: true);
        Assert.Equal(RejectionReason.LooksLikeEmail, q.Validate("john@example.com", disabled));
        Assert.Equal(RejectionReason.LooksLikeUrl, q.Validate("http://example.com", disabled));
        Assert.Equal(RejectionReason.AllDigits, q.Validate("12345", disabled));
        Assert.Null(q.Validate("12345", enabled));
        Assert.Null(q.Validate("john.smith_1", disabled));
    }

    [Fact]
    public void EmailBuilder_BuildsAndLowercasesDomain()
    {
        EmailBuilder b = new EmailBuilder();
        Assert.Equal("john@example.com", b.Build("john", "Example.COM"));
    }

    [Fact]
    public void EmailBuilder_DomainValidationRejectsBadInputs()
    {
        EmailBuilder b = new EmailBuilder();
        Assert.Throws<ArgumentException>(() => b.Build("john", ""));
        Assert.Throws<ArgumentException>(() => b.Build("john", "exa@mple.com"));
        Assert.Throws<ArgumentException>(() => b.Build("john", "invalid_domain"));
    }



    [Fact]
    public async Task NoopDedupeStore_AcceptsDuplicates()
    {
        await using NoopDedupeStore store = new NoopDedupeStore();
        DedupeEntry entry = new DedupeEntry("scope", "mode", "key", "user", "user@example.com", "src", DateTimeOffset.UtcNow);
        Assert.True(await store.TryAddAsync(entry, CancellationToken.None));
        Assert.True(await store.TryAddAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task InMemoryDedupeStore_CaseInsensitiveByDefault()
    {
        await using InMemoryDedupeStore store = new InMemoryDedupeStore();
        Assert.True(await store.TryAddAsync(new DedupeEntry("scope", "mode", "KeyA", "u", "e", "s", DateTimeOffset.UtcNow), CancellationToken.None));
        Assert.False(await store.TryAddAsync(new DedupeEntry("scope", "mode", "keya", "u", "e", "s", DateTimeOffset.UtcNow), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteDedupeStore_AllowsSameKeyWithDifferentScopeOrMode()
    {
        string path = Path.GetTempFileName();
        await using SqliteDedupeStore store = new SqliteDedupeStore(path);
        Assert.True(await store.TryAddAsync(new DedupeEntry("scope1", "mode1", "same", "u", "e", "s", DateTimeOffset.UtcNow), CancellationToken.None));
        Assert.True(await store.TryAddAsync(new DedupeEntry("scope2", "mode1", "same", "u", "e", "s", DateTimeOffset.UtcNow), CancellationToken.None));
        Assert.True(await store.TryAddAsync(new DedupeEntry("scope1", "mode2", "same", "u", "e", "s", DateTimeOffset.UtcNow), CancellationToken.None));
    }

}

public class PipelineIntegrationTests
{
    [Fact]
    public async Task Pipeline_RejectsMissingInput()
    {
        GenMailPipeline p = new GenMailPipeline();
        await Assert.ThrowsAsync<FileNotFoundException>(() => p.RunAsync("/tmp/not-exists.txt", new GenerationOptions("example.com", Path.GetTempPath()), null, CancellationToken.None));
    }

    [Fact]
    public async Task Pipeline_RejectsNonTxtInput()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gm_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(path, "x");
        GenMailPipeline p = new GenMailPipeline();
        await Assert.ThrowsAsync<ArgumentException>(() => p.RunAsync(path, new GenerationOptions("example.com", Path.GetTempPath()), null, CancellationToken.None));
    }
}

public class SplitOutputTests
{
    [Fact]
    public async Task SplitFalse_WritesSingleFiles()
    {
        string input = Path.Combine(Path.GetTempPath(), $"gm_split_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(input, new[] { "john smith", "jdoe" });
        GenMailPipeline p = new GenMailPipeline();
        GenerationOptions o = new GenerationOptions("example.com", Path.Combine(Path.GetTempPath(), "gm_split_out"), SplitOutputFiles: false, SelectedRuleIds: new[] { "firstlast" });
        ProcessingResult r = await p.RunAsync(input, o, null, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(r.OutputDirectory, "emails.txt")));
        Assert.True(File.Exists(Path.Combine(r.OutputDirectory, "usernames.txt")));
    }

    [Fact]
    public async Task SplitTrue_RotatesByRowCount()
    {
        string input = Path.Combine(Path.GetTempPath(), $"gm_split_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(input, new[] { "a a", "b b", "c c", "d d", "e e" });
        GenMailPipeline p = new GenMailPipeline();
        GenerationOptions o = new GenerationOptions("example.com", Path.Combine(Path.GetTempPath(), "gm_split_out"), SplitOutputFiles: true, RowsPerOutputFile: 2, SelectedRuleIds: new[] { "firstlast" });
        ProcessingResult r = await p.RunAsync(input, o, null, CancellationToken.None);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(r.OutputDirectory, "emails_001.txt")).Length);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(r.OutputDirectory, "emails_002.txt")).Length);
        Assert.Equal(1, File.ReadAllLines(Path.Combine(r.OutputDirectory, "emails_003.txt")).Length);
        Assert.False(File.Exists(Path.Combine(r.OutputDirectory, "emails_004.txt")));
        Assert.Equal(File.ReadAllLines(Path.Combine(r.OutputDirectory, "emails_003.txt")).Length, File.ReadAllLines(Path.Combine(r.OutputDirectory, "usernames_003.txt")).Length);
    }

    [Fact]
    public async Task SplitValidation_RejectsInvalidRowsPerFile()
    {
        string input = Path.Combine(Path.GetTempPath(), $"gm_split_{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(input, new[] { "john smith" });
        GenMailPipeline p = new GenMailPipeline();
        await Assert.ThrowsAsync<ArgumentException>(() => p.RunAsync(input, new GenerationOptions("example.com", Path.Combine(Path.GetTempPath(), "gm_split_out"), SplitOutputFiles: true, RowsPerOutputFile: null), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => p.RunAsync(input, new GenerationOptions("example.com", Path.Combine(Path.GetTempPath(), "gm_split_out"), SplitOutputFiles: true, RowsPerOutputFile: 0), null, CancellationToken.None));
    }
}
