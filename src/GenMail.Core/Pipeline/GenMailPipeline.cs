using GenMail.Core.Dedupe;
using GenMail.Core.Emailing;
using GenMail.Core.Generation;
using GenMail.Core.IO;
using GenMail.Core.Models;
using GenMail.Core.Normalization;
using GenMail.Core.Numbering;
using GenMail.Core.Quality;
using GenMail.Core.Reports;
using GenMail.Core.Safety;

namespace GenMail.Core.Pipeline;

public sealed class GenMailPipeline
{
    public async Task<ProcessingResult> RunAsync(string inputPath, GenerationOptions options, IProgress<ProgressSnapshot>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath)) throw new FileNotFoundException(inputPath);
        if (!Path.GetExtension(inputPath).Equals(".txt", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Input must be .txt");

        EmailBuilder emailBuilder = new EmailBuilder();
        emailBuilder.ValidateDomain(options.Domain);

        string outputDir = Path.Combine(options.OutputRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(outputDir);

        string usernamesPath = Path.Combine(outputDir, "usernames.txt");
        string emailsPath = Path.Combine(outputDir, "emails.txt");

        FastLineReader reader = new FastLineReader();
        INameNormalizer normalizer = new DefaultNameNormalizer();
        IDirectUsernameDetector detector = new DefaultDirectUsernameDetector();
        RuleCatalog ruleCatalog = new RuleCatalog(BuiltInUsernameRules.CreateDefault());
        IReadOnlyList<IUsernameRule> rules = options.SelectedRuleIds is { Count: > 0 }
            ? options.SelectedRuleIds.Select(ruleCatalog.GetById).ToList()
            : ruleCatalog.All.ToList();

        NumberRangeParser parser = new NumberRangeParser();
        IReadOnlyList<string> numbers = parser.Parse(options.NumberPattern);
        new SafetyGuard().EnsureWithinLimits(new OutputEstimator().Estimate(1000, rules.Count, Math.Max(1, numbers.Count)), options);

        UsernameGenerator generator = new UsernameGenerator();
        NumberExpansionService expansion = new NumberExpansionService();
        UsernameQualityPolicy quality = new UsernameQualityPolicy();
        CsvReportWriter reportWriter = new CsvReportWriter();
        List<string> duplicateSkipped = new List<string>();
        List<string> qualityRejected = new List<string>();
        List<string> rejectedInputs = new List<string>();

        await using IDedupeStore dedupeStore = options.DedupeMode switch
        {
            DedupeMode.Persistent => new SqliteDedupeStore(options.DedupeDbPath ?? Path.Combine(outputDir, "dedupe.db")),
            DedupeMode.PerRun => new InMemoryDedupeStore(),
            _ => new NoopDedupeStore(),
        };

        long inputLines = 0; long validInputs = 0; long usernamesGenerated = 0; long emailsGenerated = 0; long duplicates = 0; long qualityRejects = 0; long rejected = 0;
        await using StreamWriter usernamesWriter = new StreamWriter(usernamesPath);
        await using StreamWriter emailsWriter = new StreamWriter(emailsPath);

        await foreach (InputRecord record in reader.ReadAsync(inputPath, options.SkipEmptyLines, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            inputLines++;
            string seed;
            if (detector.IsDirectUsername(record.TrimmedInput))
            {
                seed = record.TrimmedInput.ToLowerInvariant();
            }
            else
            {
                NormalizedName name = normalizer.Normalize(record.TrimmedInput);
                if (string.IsNullOrWhiteSpace(name.All)) { rejected++; rejectedInputs.Add(record.OriginalInput); continue; }
                validInputs++;
                foreach (UsernameCandidate candidate in generator.Generate(name, rules))
                {
                    IReadOnlyList<string> expanded = expansion.Expand(candidate.Username, numbers, options.NumberMode, options.NumberPlacementMode);
                    foreach (string user in expanded)
                    {
                        RejectionReason? reason = quality.Validate(user, options);
                        if (reason.HasValue) { qualityRejects++; qualityRejected.Add($"{record.LineNumber},{user},{reason.Value}"); continue; }
                        bool added = await dedupeStore.TryAddAsync(new DedupeEntry("global", "username", user), cancellationToken).ConfigureAwait(false);
                        if (!added) { duplicates++; duplicateSkipped.Add($"{record.LineNumber},{user}"); continue; }
                        usernamesGenerated++;
                        await usernamesWriter.WriteLineAsync(user).ConfigureAwait(false);
                        string email = emailBuilder.Build(user, options.Domain);
                        await emailsWriter.WriteLineAsync(email).ConfigureAwait(false);
                        emailsGenerated++;
                    }
                }
                continue;
            }

            validInputs++;
            IReadOnlyList<string> expandedDirect = expansion.Expand(seed, numbers, options.NumberMode, options.NumberPlacementMode);
            foreach (string user in expandedDirect)
            {
                RejectionReason? reason = quality.Validate(user, options);
                if (reason.HasValue) { qualityRejects++; qualityRejected.Add($"{record.LineNumber},{user},{reason.Value}"); continue; }
                bool added = await dedupeStore.TryAddAsync(new DedupeEntry("global", "username", user), cancellationToken).ConfigureAwait(false);
                if (!added) { duplicates++; duplicateSkipped.Add($"{record.LineNumber},{user}"); continue; }
                usernamesGenerated++;
                await usernamesWriter.WriteLineAsync(user).ConfigureAwait(false);
                string email = emailBuilder.Build(user, options.Domain);
                await emailsWriter.WriteLineAsync(email).ConfigureAwait(false);
                emailsGenerated++;
            }

            if (inputLines % options.ProgressReportInterval == 0)
            {
                progress?.Report(new ProgressSnapshot(inputLines, emailsGenerated, record.OriginalInput));
            }
        }

        ProcessingResult result = new ProcessingResult(outputDir, new ProcessingCounters(inputLines, validInputs, usernamesGenerated, emailsGenerated, duplicates, qualityRejects, rejected), new List<string>(), false);
        await reportWriter.WriteRowsAsync(Path.Combine(outputDir, "duplicate_skipped.csv"), duplicateSkipped, cancellationToken).ConfigureAwait(false);
        await reportWriter.WriteRowsAsync(Path.Combine(outputDir, "quality_rejected.csv"), qualityRejected, cancellationToken).ConfigureAwait(false);
        await reportWriter.WriteRowsAsync(Path.Combine(outputDir, "rejected_inputs.csv"), rejectedInputs, cancellationToken).ConfigureAwait(false);
        await new SummaryWriter().WriteAsync(Path.Combine(outputDir, "summary.txt"), result, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
