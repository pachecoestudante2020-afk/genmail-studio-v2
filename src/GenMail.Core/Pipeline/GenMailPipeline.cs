using System.Text;
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
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        if (!File.Exists(inputPath)) throw new FileNotFoundException(inputPath);
        if (!Path.GetExtension(inputPath).Equals(".txt", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Input must be .txt");

        EmailBuilder emailBuilder = new EmailBuilder();
        emailBuilder.ValidateDomain(options.Domain);
        if (options.SplitOutputFiles)
        {
            if (!options.RowsPerOutputFile.HasValue) throw new ArgumentException("RowsPerOutputFile is required when SplitOutputFiles is enabled.");
            if (options.RowsPerOutputFile.Value <= 0 || options.RowsPerOutputFile.Value > 10_000_000) throw new ArgumentOutOfRangeException(nameof(options.RowsPerOutputFile));
        }

        string outputDir = Path.Combine(options.OutputRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(outputDir);
        string usernamesPath = Path.Combine(outputDir, "usernames.txt");
        string emailsPath = Path.Combine(outputDir, "emails.txt");
        int rowsPerFile = options.RowsPerOutputFile ?? int.MaxValue;
        int fileIndex = 1;
        int rowsInCurrentFile = 0;
        int outputFilesCreated = 0;
        StreamWriter? usernamesWriter = null;
        StreamWriter? emailsWriter = null;
        string CurrentUsernamesPath() => options.SplitOutputFiles ? Path.Combine(outputDir, $"usernames_{fileIndex:000}.txt") : usernamesPath;
        string CurrentEmailsPath() => options.SplitOutputFiles ? Path.Combine(outputDir, $"emails_{fileIndex:000}.txt") : emailsPath;
        void OpenWriters()
        {
            usernamesWriter = new StreamWriter(new FileStream(CurrentUsernamesPath(), FileMode.Create, FileAccess.Write, FileShare.None, 65536, true), new UTF8Encoding(false));
            emailsWriter = new StreamWriter(new FileStream(CurrentEmailsPath(), FileMode.Create, FileAccess.Write, FileShare.None, 65536, true), new UTF8Encoding(false));
            outputFilesCreated++;
            rowsInCurrentFile = 0;
        }
        async Task RotateIfNeededAsync()
        {
            if (!options.SplitOutputFiles || rowsInCurrentFile < rowsPerFile) return;
            if (usernamesWriter is not null) await usernamesWriter.DisposeAsync().ConfigureAwait(false);
            if (emailsWriter is not null) await emailsWriter.DisposeAsync().ConfigureAwait(false);
            fileIndex++;
            OpenWriters();
        }
        OpenWriters();

        FastLineReader reader = new FastLineReader();
        INameNormalizer normalizer = new DefaultNameNormalizer();
        IDirectUsernameDetector detector = new DefaultDirectUsernameDetector();
        RuleCatalog catalog = new RuleCatalog(BuiltInUsernameRules.CreateDefault());
        IReadOnlyList<IUsernameRule> rules = options.SelectedRuleIds is { Count: > 0 } ? options.SelectedRuleIds.Select(catalog.GetById).ToList() : catalog.All.ToList();

        IReadOnlyList<string> numbers = new NumberRangeParser().Parse(options.NumberPattern, options.MaxNumbersPerBase);
        SafetyEstimate estimate = new OutputEstimator().Estimate(1, rules.Count, Math.Max(1, numbers.Count));
        new SafetyGuard().EnsureWithinLimits(estimate, options);

        await using IDedupeStore dedupeStore = options.DedupeMode == DedupeMode.Persistent ? new SqliteDedupeStore(options.DedupeDbPath ?? Path.Combine(outputDir, "dedupe.db")) : options.DedupeMode == DedupeMode.PerRun ? new InMemoryDedupeStore() : new NoopDedupeStore();

        List<DuplicateSkippedRow> duplicateRows = new List<DuplicateSkippedRow>();
        List<QualityRejectedRow> qualityRows = new List<QualityRejectedRow>();
        List<RejectedInputRow> rejectedRows = new List<RejectedInputRow>();
        List<string> warnings = new List<string>();

        long inputLines = 0; long validInputs = 0; long usernamesGenerated = 0; long emailsWritten = 0; long duplicates = 0; long qualityRejected = 0; long rejectedInputs = 0;
        UsernameGenerator generator = new UsernameGenerator();
        NumberExpansionService expander = new NumberExpansionService();
        UsernameQualityPolicy quality = new UsernameQualityPolicy();


        await foreach (InputRecord record in reader.ReadAsync(inputPath, options.SkipEmptyLines, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            inputLines++;
            IEnumerable<string> baseCandidates;
            if (detector.IsDirectUsername(record.TrimmedInput))
            {
                validInputs++;
                baseCandidates = new[] { record.TrimmedInput.ToLowerInvariant() };
            }
            else
            {
                NormalizedName normalized = normalizer.Normalize(record.TrimmedInput);
                if (string.IsNullOrWhiteSpace(normalized.All))
                {
                    rejectedInputs++;
                    rejectedRows.Add(new RejectedInputRow(record.OriginalInput, RejectionReason.Empty.ToString(), DateTimeOffset.UtcNow));
                    continue;
                }
                validInputs++;
                baseCandidates = generator.Generate(normalized, rules).Select(c => c.Username);
            }

            foreach (string baseCandidate in baseCandidates)
            {
                foreach (string username in expander.Expand(baseCandidate, numbers, options.NumberMode, options.NumberPlacementMode))
                {
                    RejectionReason? rejection = quality.Validate(username, options);
                    if (rejection.HasValue)
                    {
                        qualityRejected++;
                        qualityRows.Add(new QualityRejectedRow(username, string.Empty, record.OriginalInput, rejection.Value.ToString(), DateTimeOffset.UtcNow));
                        continue;
                    }
                    string email = emailBuilder.Build(username, options.Domain);
                    DedupeEntry entry = new DedupeEntry("global", "username", username, username, email, record.OriginalInput, DateTimeOffset.UtcNow);
                    bool added = await dedupeStore.TryAddAsync(entry, cancellationToken).ConfigureAwait(false);
                    if (!added)
                    {
                        duplicates++;
                        duplicateRows.Add(new DuplicateSkippedRow(entry.Key, entry.KeyMode, entry.Username, entry.Email, entry.SourceInput, "duplicate", entry.CreatedAtUtc));
                        continue;
                    }

                    usernamesGenerated++;
                    emailsWritten++;
                    await usernamesWriter!.WriteLineAsync(username).ConfigureAwait(false);
                    await emailsWriter!.WriteLineAsync(email).ConfigureAwait(false);
                    rowsInCurrentFile++;
                    await RotateIfNeededAsync().ConfigureAwait(false);
                }
            }

            if (inputLines % options.ProgressReportInterval == 0)
            {
                progress?.Report(new ProgressSnapshot(inputLines, usernamesGenerated, emailsWritten, duplicates, qualityRejected, "running"));
            }
        }

        if (usernamesWriter is not null) await usernamesWriter.DisposeAsync().ConfigureAwait(false);
        if (emailsWriter is not null) await emailsWriter.DisposeAsync().ConfigureAwait(false);

        ProcessingCounters counters = new ProcessingCounters(inputLines, validInputs, usernamesGenerated, emailsWritten, duplicates, qualityRejected, rejectedInputs, outputFilesCreated, options.RowsPerOutputFile);
        List<string> generatedFiles = new List<string>();
        if (options.SplitOutputFiles)
        {
            for (int i = 1; i <= outputFilesCreated; i++)
            {
                generatedFiles.Add($"usernames_{i:000}.txt");
                generatedFiles.Add($"emails_{i:000}.txt");
            }
        }
        else
        {
            generatedFiles.Add("usernames.txt");
            generatedFiles.Add("emails.txt");
        }
        generatedFiles.Add("duplicate_skipped.csv");
        generatedFiles.Add("quality_rejected.csv");
        generatedFiles.Add("rejected_inputs.csv");
        generatedFiles.Add("summary.txt");
        ProcessingResult result = new ProcessingResult(outputDir, counters, estimate, generatedFiles, warnings);

        CsvReportWriter reportWriter = new CsvReportWriter();
        await reportWriter.WriteDuplicateSkippedAsync(Path.Combine(outputDir, "duplicate_skipped.csv"), duplicateRows, cancellationToken).ConfigureAwait(false);
        await reportWriter.WriteQualityRejectedAsync(Path.Combine(outputDir, "quality_rejected.csv"), qualityRows, cancellationToken).ConfigureAwait(false);
        await reportWriter.WriteRejectedInputsAsync(Path.Combine(outputDir, "rejected_inputs.csv"), rejectedRows, cancellationToken).ConfigureAwait(false);

        await new SummaryWriter().WriteAsync(Path.Combine(outputDir, "summary.txt"), startedAt, DateTimeOffset.UtcNow, inputPath, options, result, generatedFiles, warnings, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ProgressSnapshot(inputLines, usernamesGenerated, emailsWritten, duplicates, qualityRejected, "completed"));
        return result;
    }
}
