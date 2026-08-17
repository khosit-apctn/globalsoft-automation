using Automation.Infrastructure.Runs;

namespace Automation.Infrastructure.Tests.Runs;

internal sealed class SqliteFixture : IAsyncDisposable
{
    private readonly string _directory;

    internal DateTimeOffset Now { get; } = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    internal SqliteRunHistoryStore Store { get; }

    private SqliteFixture(string directory, SqliteRunHistoryStore store)
        => (_directory, Store) = (directory, store);

    internal static async Task<SqliteFixture> CreateAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"automation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var store = new SqliteRunHistoryStore(Path.Combine(directory, "runs.db"));
        await store.InitializeAsync();
        return new SqliteFixture(directory, store);
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
