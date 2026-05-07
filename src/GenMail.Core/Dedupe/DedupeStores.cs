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
    private readonly HashSet<string> _seenKeys;

    public InMemoryDedupeStore(StringComparer? comparer = null)
    {
        _seenKeys = new HashSet<string>(comparer ?? StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = $"{entry.Scope}|{entry.KeyMode}|{entry.Key}";
        return ValueTask.FromResult(_seenKeys.Add(key));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SqliteDedupeStore : IDedupeStore
{
    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _insertCommand;
    private SqliteTransaction _activeTransaction;
    private int _batchCount;
    private const int BatchSize = 1000;

    public SqliteDedupeStore(string path)
    {
        _connection = new SqliteConnection($"Data Source={path}");
        _connection.Open();

        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }

        using (SqliteCommand create = _connection.CreateCommand())
        {
            create.CommandText = @"CREATE TABLE IF NOT EXISTS generated_keys(
scope TEXT NOT NULL,
key_mode TEXT NOT NULL,
dedupe_key TEXT NOT NULL,
username TEXT,
email TEXT,
source_input TEXT,
created_at_utc TEXT NOT NULL,
PRIMARY KEY(scope, key_mode, dedupe_key)
);";
            create.ExecuteNonQuery();
        }

        _activeTransaction = _connection.BeginTransaction();
        _insertCommand = _connection.CreateCommand();
        _insertCommand.Transaction = _activeTransaction;
        _insertCommand.CommandText = @"INSERT OR IGNORE INTO generated_keys(scope,key_mode,dedupe_key,username,email,source_input,created_at_utc)
VALUES($scope,$key_mode,$dedupe_key,$username,$email,$source_input,$created_at_utc);";
        _insertCommand.Parameters.Add("$scope", SqliteType.Text);
        _insertCommand.Parameters.Add("$key_mode", SqliteType.Text);
        _insertCommand.Parameters.Add("$dedupe_key", SqliteType.Text);
        _insertCommand.Parameters.Add("$username", SqliteType.Text);
        _insertCommand.Parameters.Add("$email", SqliteType.Text);
        _insertCommand.Parameters.Add("$source_input", SqliteType.Text);
        _insertCommand.Parameters.Add("$created_at_utc", SqliteType.Text);
    }

    public ValueTask<bool> TryAddAsync(DedupeEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _insertCommand.Parameters["$scope"].Value = entry.Scope;
        _insertCommand.Parameters["$key_mode"].Value = entry.KeyMode;
        _insertCommand.Parameters["$dedupe_key"].Value = entry.Key;
        _insertCommand.Parameters["$username"].Value = entry.Username;
        _insertCommand.Parameters["$email"].Value = entry.Email;
        _insertCommand.Parameters["$source_input"].Value = entry.SourceInput;
        _insertCommand.Parameters["$created_at_utc"].Value = entry.CreatedAtUtc.UtcDateTime.ToString("O");

        int changed = _insertCommand.ExecuteNonQuery();
        _batchCount++;
        if (_batchCount >= BatchSize)
        {
            _activeTransaction.Commit();
            _activeTransaction.Dispose();
            _activeTransaction = _connection.BeginTransaction();
            _insertCommand.Transaction = _activeTransaction;
            _batchCount = 0;
        }
        return ValueTask.FromResult(changed > 0);
    }

    public ValueTask DisposeAsync()
    {
        _activeTransaction.Commit();
        _activeTransaction.Dispose();
        _insertCommand.Dispose();
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
