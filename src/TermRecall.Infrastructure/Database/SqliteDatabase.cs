using Microsoft.Data.Sqlite;
using TermRecall.Application.Abstractions;
using TermRecall.Infrastructure.Seeding;

namespace TermRecall.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public DatabaseOptions(string path) => Path = path;
    public string Path { get; }
}

public sealed class SqliteDatabase : ITransactionRunner, IDatabaseInitializer, IAsyncDisposable
{
    private static readonly object BatteriesGate = new();
    private static bool _batteriesReady;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AsyncLocal<SqliteTransaction?> _ambient = new();
    private readonly SqliteConnection _connection;

    public SqliteDatabase(DatabaseOptions options)
    {
        DatabasePath = options.Path;
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureBatteries();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = options.Path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        };
        _connection = new SqliteConnection(builder.ConnectionString);
        _connection.Open();
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ApplyPragmasAsync(cancellationToken);
            await MigrateAsync(cancellationToken);
            await TerminologySeeder.SeedIfEmptyAsync(this, cancellationToken);
        }
        catch (SqliteException) when (File.Exists(DatabasePath))
        {
            await RecoverAndRecreateAsync(cancellationToken);
        }
    }

    public async Task<T> QueryAsync<T>(
        Func<SqliteConnection, SqliteTransaction?, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        if (_ambient.Value is not null)
        {
            return await work(_connection, _ambient.Value, cancellationToken);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await work(_connection, null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken);
            _ambient.Value = transaction;
            try
            {
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                _ambient.Value = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]> CreateBackupBytesAsync(CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"termrecall-{Guid.NewGuid():N}.db");
        try
        {
            await QueryAsync(async (connection, _, token) =>
            {
                await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = temp,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }.ConnectionString);
                await destination.OpenAsync(token);
                connection.BackupDatabase(destination);
                return 0;
            }, cancellationToken);
            return await File.ReadAllBytesAsync(temp, cancellationToken);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    public async Task RestoreAsync(Stream backup, CancellationToken cancellationToken)
    {
        var incomingPath = Path.Combine(Path.GetTempPath(), $"termrecall-in-{Guid.NewGuid():N}.db");
        await using (var file = File.Create(incomingPath))
        {
            await backup.CopyToAsync(file, cancellationToken);
        }

        try
        {
            await using (var probe = new SqliteConnection($"Data Source={incomingPath};Mode=ReadOnly"))
            {
                await probe.OpenAsync(cancellationToken);
                await using var check = probe.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
                _ = await check.ExecuteScalarAsync(cancellationToken);
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                SqliteConnection.ClearAllPools();
                await using var incoming = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = incomingPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ConnectionString);
                await incoming.OpenAsync(cancellationToken);
                incoming.BackupDatabase(_connection);
                await ApplyPragmasCoreAsync(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            TryDelete(incomingPath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _gate.Dispose();
    }

    private async Task RecoverAndRecreateAsync(CancellationToken cancellationToken)
    {
        var broken = DatabasePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        await _connection.CloseAsync();
        TryDelete(DatabasePath + "-wal");
        TryDelete(DatabasePath + "-shm");
        if (File.Exists(DatabasePath))
        {
            File.Move(DatabasePath, broken, true);
        }

        await _connection.OpenAsync(cancellationToken);
        await ApplyPragmasAsync(cancellationToken);
        await MigrateAsync(cancellationToken);
        await TerminologySeeder.SeedIfEmptyAsync(this, cancellationToken);
    }

    private Task ApplyPragmasAsync(CancellationToken cancellationToken)
        => QueryAsync(async (connection, transaction, token) =>
        {
            await ApplyPragmasCoreAsync(connection, transaction, token);
            return 0;
        }, cancellationToken);

    private Task ApplyPragmasCoreAsync(CancellationToken cancellationToken)
        => ApplyPragmasCoreAsync(_connection, _ambient.Value, cancellationToken);

    private static async Task ApplyPragmasCoreAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA busy_timeout = 5000;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task MigrateAsync(CancellationToken cancellationToken)
        => QueryAsync(async (connection, transaction, token) =>
        {
            await MigrateCoreAsync(connection, transaction, token);
            return 0;
        }, cancellationToken);

    private Task MigrateCoreAsync(CancellationToken cancellationToken)
        => MigrateCoreAsync(_connection, _ambient.Value, cancellationToken);

    private static async Task MigrateCoreAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using (var ensure = connection.CreateCommand())
        {
            ensure.Transaction = transaction;
            ensure.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    applied_at TEXT NOT NULL
                );
                """;
            await ensure.ExecuteNonQueryAsync(cancellationToken);
        }

        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT name FROM schema_migrations;";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetString(0));
            }
        }

        var assembly = typeof(SqliteDatabase).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".sql", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal))
        {
            var name = resource.Split('.').Reverse().Skip(1).First();
            if (applied.Contains(name))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Не найдена миграция {resource}.");
            using var text = new StreamReader(stream);
            var sql = await text.ReadToEndAsync(cancellationToken);
            await using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = sql;
            await migrate.ExecuteNonQueryAsync(cancellationToken);

            await using var mark = connection.CreateCommand();
            mark.Transaction = transaction;
            mark.CommandText = "INSERT INTO schema_migrations(name, applied_at) VALUES ($name, $at);";
            mark.Parameters.AddWithValue("$name", name);
            mark.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void EnsureBatteries()
    {
        lock (BatteriesGate)
        {
            if (_batteriesReady)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();
            _batteriesReady = true;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
