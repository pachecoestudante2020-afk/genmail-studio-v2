using GenMail.Core.Models;

namespace GenMail.Core.Reports;

public sealed class CsvReportWriter
{
    public async Task WriteRowsAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken)
    {
        await File.WriteAllLinesAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SummaryWriter
{
    public async Task WriteAsync(string path, ProcessingResult result, CancellationToken cancellationToken)
    {
        List<string> lines = new List<string>
        {
            $"InputLines: {result.Counters.InputLines}",
            $"ValidInputs: {result.Counters.ValidInputs}",
            $"EmailsGenerated: {result.Counters.EmailsGenerated}",
            $"DuplicateSkipped: {result.Counters.DuplicateSkipped}",
            $"QualityRejected: {result.Counters.QualityRejected}",
            $"RejectedInputs: {result.Counters.RejectedInputs}",
        };
        await File.WriteAllLinesAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }
}
