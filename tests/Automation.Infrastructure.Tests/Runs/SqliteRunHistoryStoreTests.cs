using Automation.Platform.Contracts.Runs;

namespace Automation.Infrastructure.Tests.Runs;

[TestClass]
public sealed class SqliteRunHistoryStoreTests
{
    [TestMethod]
    public async Task Completed_run_round_trips_failures_and_artifacts_in_input_order()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var runId = Guid.NewGuid();
        await fixture.Store.CreateAsync(new RunRecord(runId, "rebate", "2026-07", RunStatus.Running, fixture.Now, null, [], []));
        await fixture.Store.CompleteAsync(runId, RunStatus.PartialFailed, fixture.Now.AddMinutes(1),
            [
                new RunFailure("web", "POI-1", "detail", "ELEMENT_NOT_FOUND", "missing", "shot.png"),
                new RunFailure("import", "POI-2", "validate", "DUPLICATE", "duplicate", null)
            ],
            [
                new RunArtifact("report", "Report.xlsx", "out/Report.xlsx"),
                new RunArtifact("log", "Log.txt", "out/Log.txt")
            ]);

        var saved = (await fixture.Store.ListByModuleAsync("rebate", 20)).Single();

        Assert.AreEqual(RunStatus.PartialFailed, saved.Status);
        Assert.AreEqual(fixture.Now.AddMinutes(1), saved.EndedAt);
        CollectionAssert.AreEqual(new[] { "POI-1", "POI-2" }, saved.Failures.Select(failure => failure.ItemKey).ToArray());
        CollectionAssert.AreEqual(new[] { "Report.xlsx", "Log.txt" }, saved.Artifacts.Select(artifact => artifact.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task Mark_running_as_interrupted_updates_every_running_row()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "first", RunStatus.Running, fixture.Now, null, [], []));
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "second", RunStatus.Running, fixture.Now.AddMinutes(1), null, [], []));

        var affected = await fixture.Store.MarkRunningAsInterruptedAsync(fixture.Now.AddMinutes(2));
        var saved = await fixture.Store.ListByModuleAsync("rebate", 20);

        Assert.AreEqual(2, affected);
        Assert.IsTrue(saved.All(run => run.Status == RunStatus.Interrupted));
        Assert.IsTrue(saved.All(run => run.EndedAt == fixture.Now.AddMinutes(2)));
    }

    [TestMethod]
    public async Task List_by_module_returns_newest_runs_first_and_honors_limit()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "old", RunStatus.Success, fixture.Now, fixture.Now, [], []));
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "other", "ignored", RunStatus.Success, fixture.Now.AddMinutes(3), fixture.Now.AddMinutes(3), [], []));
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "new", RunStatus.Success, fixture.Now.AddMinutes(2), fixture.Now.AddMinutes(2), [], []));

        var saved = await fixture.Store.ListByModuleAsync("rebate", 1);

        Assert.AreEqual(1, saved.Count);
        Assert.AreEqual("new", saved[0].InputLabel);
    }

    [TestMethod]
    public async Task Create_honors_an_already_cancelled_token()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => fixture.Store.CreateAsync(
            new RunRecord(Guid.NewGuid(), "rebate", "input", RunStatus.Running, fixture.Now, null, [], []), cancellation.Token));
    }
}
