using System.Runtime.CompilerServices;
using GenMail.Core.Models;

namespace GenMail.Core.IO;

public sealed class FastLineReader
{
    public async IAsyncEnumerable<InputRecord> ReadAsync(string inputPath, bool skipEmptyLines, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FileStream stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        using StreamReader reader = new StreamReader(stream);
        long lineNumber = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? raw = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (raw is null)
            {
                continue;
            }

            lineNumber++;
            string trimmed = raw.Trim();
            if (skipEmptyLines && trimmed.Length == 0)
            {
                continue;
            }

            yield return new InputRecord(lineNumber, raw, trimmed);
        }
    }
}
