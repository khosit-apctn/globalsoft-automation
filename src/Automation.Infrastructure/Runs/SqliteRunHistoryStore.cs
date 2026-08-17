using Automation.Platform.Contracts.Runs;
using Microsoft.Data.Sqlite;

namespace Automation.Infrastructure.Runs;

public sealed class SqliteRunHistoryStore : IRunHistoryStore
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteRunHistoryStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS runs (
                run_id TEXT NOT NULL PRIMARY KEY,
                module_id TEXT NOT NULL,
                input_label TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at_utc_ticks INTEGER NOT NULL,
                ended_at_utc_ticks INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS run_failures (
                run_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                source TEXT NOT NULL,
                item_key TEXT NULL,
                step TEXT NOT NULL,
                error_code TEXT NOT NULL,
                message TEXT NOT NULL,
                screenshot_path TEXT NULL,
                PRIMARY KEY (run_id, ordinal),
                FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS run_artifacts (
                run_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                kind TEXT NOT NULL,
                display_name TEXT NOT NULL,
                path TEXT NOT NULL,
                PRIMARY KEY (run_id, ordinal),
                FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_runs_module_started ON runs(module_id, started_at_utc_ticks DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateAsync(RunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(run.Failures);
        ArgumentNullException.ThrowIfNull(run.Artifacts);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await InsertRunAsync(connection, transaction, run, cancellationToken);
        await InsertFailuresAsync(connection, transaction, run.RunId, run.Failures, cancellationToken);
        await InsertArtifactsAsync(connection, transaction, run.RunId, run.Artifacts, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        Guid runId,
        RunStatus status,
        DateTimeOffset endedAt,
        IReadOnlyList<RunFailure> failures,
        IReadOnlyList<RunArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(artifacts);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE runs SET status = $status, ended_at_utc_ticks = $endedAt WHERE run_id = $runId;";
            update.Parameters.AddWithValue("$status", ToDatabaseStatus(status));
            update.Parameters.AddWithValue("$endedAt", endedAt.UtcDateTime.Ticks);
            update.Parameters.AddWithValue("$runId", runId.ToString("N"));
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await DeleteChildrenAsync(connection, transaction, "run_failures", runId, cancellationToken);
        await DeleteChildrenAsync(connection, transaction, "run_artifacts", runId, cancellationToken);
        await InsertFailuresAsync(connection, transaction, runId, failures, cancellationToken);
        await InsertArtifactsAsync(connection, transaction, runId, artifacts, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> MarkRunningAsInterruptedAsync(DateTimeOffset endedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE runs
            SET status = $interrupted, ended_at_utc_ticks = $endedAt
            WHERE status = $running;
            """;
        command.Parameters.AddWithValue("$interrupted", ToDatabaseStatus(RunStatus.Interrupted));
        command.Parameters.AddWithValue("$endedAt", endedAt.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$running", ToDatabaseStatus(RunStatus.Running));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RunRecord>> ListByModuleAsync(
        string moduleId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var runs = new List<RunRecord>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, module_id, input_label, status, started_at_utc_ticks, ended_at_utc_ticks
            FROM runs
            WHERE module_id = $moduleId
            ORDER BY started_at_utc_ticks DESC, run_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$moduleId", moduleId);
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var runId = Guid.ParseExact(reader.GetString(0), "N");
            var failures = await ReadFailuresAsync(connection, runId, cancellationToken);
            var artifacts = await ReadArtifactsAsync(connection, runId, cancellationToken);
            runs.Add(new RunRecord(
                runId,
                reader.GetString(1),
                reader.GetString(2),
                FromDatabaseStatus(reader.GetString(3)),
                FromUtcTicks(reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : FromUtcTicks(reader.GetInt64(5)),
                failures,
                artifacts));
        }

        return runs;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertRunAsync(SqliteConnection connection, SqliteTransaction transaction, RunRecord run, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO runs (run_id, module_id, input_label, status, started_at_utc_ticks, ended_at_utc_ticks)
            VALUES ($runId, $moduleId, $inputLabel, $status, $startedAt, $endedAt);
            """;
        command.Parameters.AddWithValue("$runId", run.RunId.ToString("N"));
        command.Parameters.AddWithValue("$moduleId", run.ModuleId);
        command.Parameters.AddWithValue("$inputLabel", run.InputLabel);
        command.Parameters.AddWithValue("$status", ToDatabaseStatus(run.Status));
        command.Parameters.AddWithValue("$startedAt", run.StartedAt.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$endedAt", run.EndedAt?.UtcDateTime.Ticks ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertFailuresAsync(SqliteConnection connection, SqliteTransaction transaction, Guid runId, IReadOnlyList<RunFailure> failures, CancellationToken cancellationToken)
    {
        for (var index = 0; index < failures.Count; index++)
        {
            var failure = failures[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO run_failures (run_id, ordinal, source, item_key, step, error_code, message, screenshot_path)
                VALUES ($runId, $ordinal, $source, $itemKey, $step, $errorCode, $message, $screenshotPath);
                """;
            command.Parameters.AddWithValue("$runId", runId.ToString("N"));
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$source", failure.Source);
            command.Parameters.AddWithValue("$itemKey", failure.ItemKey ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$step", failure.Step);
            command.Parameters.AddWithValue("$errorCode", failure.ErrorCode);
            command.Parameters.AddWithValue("$message", failure.Message);
            command.Parameters.AddWithValue("$screenshotPath", failure.ScreenshotPath ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertArtifactsAsync(SqliteConnection connection, SqliteTransaction transaction, Guid runId, IReadOnlyList<RunArtifact> artifacts, CancellationToken cancellationToken)
    {
        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO run_artifacts (run_id, ordinal, kind, display_name, path)
                VALUES ($runId, $ordinal, $kind, $displayName, $path);
                """;
            command.Parameters.AddWithValue("$runId", runId.ToString("N"));
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$kind", artifact.Kind);
            command.Parameters.AddWithValue("$displayName", artifact.DisplayName);
            command.Parameters.AddWithValue("$path", artifact.Path);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task DeleteChildrenAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName, Guid runId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {tableName} WHERE run_id = $runId;";
        command.Parameters.AddWithValue("$runId", runId.ToString("N"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<RunFailure>> ReadFailuresAsync(SqliteConnection connection, Guid runId, CancellationToken cancellationToken)
    {
        var failures = new List<RunFailure>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source, item_key, step, error_code, message, screenshot_path
            FROM run_failures
            WHERE run_id = $runId
            ORDER BY ordinal ASC;
            """;
        command.Parameters.AddWithValue("$runId", runId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            failures.Add(new RunFailure(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return failures;
    }

    private static async Task<IReadOnlyList<RunArtifact>> ReadArtifactsAsync(SqliteConnection connection, Guid runId, CancellationToken cancellationToken)
    {
        var artifacts = new List<RunArtifact>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, display_name, path
            FROM run_artifacts
            WHERE run_id = $runId
            ORDER BY ordinal ASC;
            """;
        command.Parameters.AddWithValue("$runId", runId.ToString("N"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            artifacts.Add(new RunArtifact(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return artifacts;
    }

    private static string ToDatabaseStatus(RunStatus status) => status switch
    {
        RunStatus.Running => "RUNNING",
        RunStatus.Success => "SUCCESS",
        RunStatus.PartialFailed => "PARTIAL_FAILED",
        RunStatus.Failed => "FAILED",
        RunStatus.Interrupted => "INTERRUPTED",
        RunStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static RunStatus FromDatabaseStatus(string status) => status switch
    {
        "RUNNING" => RunStatus.Running,
        "SUCCESS" => RunStatus.Success,
        "PARTIAL_FAILED" => RunStatus.PartialFailed,
        "FAILED" => RunStatus.Failed,
        "INTERRUPTED" => RunStatus.Interrupted,
        "CANCELLED" => RunStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown persisted run status '{status}'.")
    };

    private static DateTimeOffset FromUtcTicks(long ticks) => new(new DateTime(ticks, DateTimeKind.Utc));
}
