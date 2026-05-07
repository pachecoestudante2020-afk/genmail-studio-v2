using System.Runtime.CompilerServices;
using System.Text;
using GenMail.Core.Models;

namespace GenMail.Core.IO;

public sealed class FastLineReader
{
    private const int BufferSize = 4 * 1024 * 1024;

    public async IAsyncEnumerable<InputRecord> ReadAsync(string inputPath, bool skipEmptyLines, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FileStream stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: BufferSize);
        long lineNumber = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? raw = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (raw is null) continue;
            lineNumber++;
            string trimmed = raw.Trim();
            if (skipEmptyLines && trimmed.Length == 0) continue;
            yield return new InputRecord(lineNumber, raw, trimmed);
        }
    }
}
