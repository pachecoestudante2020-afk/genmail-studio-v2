using System.Text;
using GenMail.Core.Models;

namespace GenMail.Core.Reports;

public sealed record DuplicateSkippedRow(string DedupeKey, string KeyMode, string Username, string Email, string SourceInput, string Reason, DateTimeOffset CreatedAtUtc);
public sealed record QualityRejectedRow(string Username, string Email, string SourceInput, string Reason, DateTimeOffset CreatedAtUtc);
public sealed record RejectedInputRow(string SourceInput, string Reason, DateTimeOffset CreatedAtUtc);

public sealed class CsvReportWriter
{
    public async Task WriteDuplicateSkippedAsync(string path, IReadOnlyList<DuplicateSkippedRow> rows, CancellationToken cancellationToken)
        => await WriteAsync(path, new[] { "dedupe_key", "key_mode", "username", "email", "source_input", "reason", "created_at_utc" }, rows.Select(r => new[] { r.DedupeKey, r.KeyMode, r.Username, r.Email, r.SourceInput, r.Reason, r.CreatedAtUtc.ToString("O") }), cancellationToken).ConfigureAwait(false);

    public async Task WriteQualityRejectedAsync(string path, IReadOnlyList<QualityRejectedRow> rows, CancellationToken cancellationToken)
        => await WriteAsync(path, new[] { "username", "email", "source_input", "reason", "created_at_utc" }, rows.Select(r => new[] { r.Username, r.Email, r.SourceInput, r.Reason, r.CreatedAtUtc.ToString("O") }), cancellationToken).ConfigureAwait(false);

    public async Task WriteRejectedInputsAsync(string path, IReadOnlyList<RejectedInputRow> rows, CancellationToken cancellationToken)
        => await WriteAsync(path, new[] { "source_input", "reason", "created_at_utc" }, rows.Select(r => new[] { r.SourceInput, r.Reason, r.CreatedAtUtc.ToString("O") }), cancellationToken).ConfigureAwait(false);

    private static async Task WriteAsync(string path, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, CancellationToken cancellationToken)
    {
        await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await using StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(false), 65536);
        await writer.WriteLineAsync(string.Join(',', headers.Select(Escape))).ConfigureAwait(false);
        foreach (IReadOnlyList<string> row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(',', row.Select(Escape))).ConfigureAwait(false);
        }
    }

    private static string Escape(string? value)
    {
        string v = value ?? string.Empty;
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
        {
            return '"' + v.Replace("\"", "\"\"") + '"';
        }
        return v;
    }
}

public sealed class SummaryWriter
{
    public async Task WriteAsync(string path, DateTimeOffset startedAt, DateTimeOffset finishedAt, string inputPath, GenerationOptions options, ProcessingResult result, IReadOnlyList<string> generatedFiles, IReadOnlyList<string> warnings, CancellationToken cancellationToken)
    {
        TimeSpan elapsed = finishedAt - startedAt;
        List<string> lines = new List<string>
        {
            $"started_at={startedAt:O}",
            $"finished_at={finishedAt:O}",
            $"elapsed={elapsed}",
            $"input_path={inputPath}",
            $"output_folder={result.OutputDirectory}",
            $"domain={options.Domain}",
            $"selected_rules_count={(options.SelectedRuleIds?.Count ?? 0)}",
            $"number_mode={options.NumberMode}",
            $"dedupe_mode={options.DedupeMode}",
            $"input_lines_read={result.Counters.InputLines}",
            $"rejected_inputs={result.Counters.RejectedInputs}",
            $"usernames_generated={result.Counters.UsernamesGenerated}",
            $"emails_written={result.Counters.EmailsGenerated}",
            $"split_output_files={options.SplitOutputFiles}",
            $"rows_per_output_file={(options.RowsPerOutputFile?.ToString() ?? "null")}",
            $"output_files_created={result.Counters.OutputFilesCreated}",
            $"total_emails_written={result.Counters.EmailsGenerated}",
            $"total_usernames_written={result.Counters.UsernamesGenerated}",
            $"duplicates_skipped={result.Counters.DuplicateSkipped}",
            $"quality_rejected={result.Counters.QualityRejected}",
            $"generated_files={string.Join(';', generatedFiles)}",
            $"warnings={string.Join(';', warnings)}",
        };
        await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }
}
