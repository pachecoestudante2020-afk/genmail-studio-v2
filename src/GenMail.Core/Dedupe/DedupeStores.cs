using GenMail.Core.Models;
using Microsoft.Data.Sqlite;

namespace GenMail.Core.Dedupe;

public interface IDedupeStore : IAsyncDisposable
{
    ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken);
}

public sealed class NoopDedupeStore : IDedupeStore
{
    public ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class InMemoryDedupeStore : IDedupeStore
{
    private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
    public ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken)
    {
        string key = $"{entry.Scope}|{entry.KeyMode}|{entry.DedupeKey}";
        return ValueTask.FromResult(_seen.Add(key));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SqliteDedupeStore : IDedupeStore
{
    private readonly SqliteConnection _connection;

    public SqliteDedupeStore(string path)
    {
        _connection = new SqliteConnection($"Data Source={path}");
        _connection.Open();
        using SqliteCommand pragmaWal = _connection.CreateCommand();
        pragmaWal.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragmaWal.ExecuteNonQuery();
        using SqliteCommand create = _connection.CreateCommand();
        create.CommandText = "CREATE TABLE IF NOT EXISTS generated_keys(scope TEXT NOT NULL,key_mode TEXT NOT NULL,dedupe_key TEXT NOT NULL,PRIMARY KEY(scope,key_mode,dedupe_key));";
        create.ExecuteNonQuery();
    }

    public ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO generated_keys(scope,key_mode,dedupe_key) VALUES ($s,$k,$d); SELECT changes();";
        cmd.Parameters.AddWithValue("$s", entry.Scope);
        cmd.Parameters.AddWithValue("$k", entry.KeyMode);
        cmd.Parameters.AddWithValue("$d", entry.DedupeKey);
        long changed = Convert.ToInt64(cmd.ExecuteScalar());
        return ValueTask.FromResult(changed > 0);
    }

    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
