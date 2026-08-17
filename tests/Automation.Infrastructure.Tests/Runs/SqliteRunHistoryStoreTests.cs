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
        CollectionAssert.AreEqual(
            new RunFailure[]
            {
                new("web", "POI-1", "detail", "ELEMENT_NOT_FOUND", "missing", "shot.png"),
                new("import", "POI-2", "validate", "DUPLICATE", "duplicate", null)
            },
            saved.Failures.ToArray());
        CollectionAssert.AreEqual(
            new RunArtifact[]
            {
                new("report", "Report.xlsx", "out/Report.xlsx"),
                new("log", "Log.txt", "out/Log.txt")
            },
            saved.Artifacts.ToArray());
    }

    [TestMethod]
    public async Task Complete_rejects_an_unknown_run()
    {
        await using var fixture = await SqliteFixture.CreateAsync();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Store.CompleteAsync(
            Guid.NewGuid(),
            RunStatus.Failed,
            fixture.Now,
            [new RunFailure("web", "POI-1", "detail", "ELEMENT_NOT_FOUND", "missing", null)],
            [new RunArtifact("report", "Report.xlsx", "out/Report.xlsx")]));

        StringAssert.Contains(exception.Message, "RUNNING");
    }

    [TestMethod]
    public async Task Complete_cannot_overwrite_terminal_or_interrupted_runs()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var successfulRunId = Guid.NewGuid();
        var interruptedRunId = Guid.NewGuid();
        var originalArtifact = new RunArtifact("report", "Original.xlsx", "out/Original.xlsx");

        await fixture.Store.CreateAsync(new RunRecord(successfulRunId, "rebate", "successful", RunStatus.Success, fixture.Now, fixture.Now, [], [originalArtifact]));
        await fixture.Store.CreateAsync(new RunRecord(interruptedRunId, "rebate", "interrupted", RunStatus.Running, fixture.Now.AddMinutes(1), null, [], [originalArtifact]));
        await fixture.Store.MarkRunningAsInterruptedAsync(fixture.Now.AddMinutes(2));

        foreach (var runId in new[] { successfulRunId, interruptedRunId })
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Store.CompleteAsync(
                runId,
                RunStatus.Failed,
                fixture.Now.AddMinutes(3),
                [new RunFailure("web", "replacement", "detail", "ELEMENT_NOT_FOUND", "missing", null)],
                [new RunArtifact("report", "Replacement.xlsx", "out/Replacement.xlsx")]));
        }

        var saved = await fixture.Store.ListByModuleAsync("rebate", 20);
        Assert.AreEqual(RunStatus.Success, saved.Single(run => run.RunId == successfulRunId).Status);
        Assert.AreEqual(RunStatus.Interrupted, saved.Single(run => run.RunId == interruptedRunId).Status);
        CollectionAssert.AreEqual(new[] { originalArtifact }, saved.Single(run => run.RunId == successfulRunId).Artifacts.ToArray());
        CollectionAssert.AreEqual(new[] { originalArtifact }, saved.Single(run => run.RunId == interruptedRunId).Artifacts.ToArray());
    }

    [TestMethod]
    public async Task Mark_running_as_interrupted_updates_every_running_row()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "first", RunStatus.Running, fixture.Now, null, [], []));
        await fixture.Store.CreateAsync(new RunRecord(Guid.NewGuid(), "rebate", "second", RunStatus.Running, fixture.Now.AddMinutes(1), null, [], []));
        var terminalRunId = Guid.NewGuid();
        await fixture.Store.CreateAsync(new RunRecord(terminalRunId, "rebate", "complete", RunStatus.Success, fixture.Now.AddMinutes(2), fixture.Now.AddMinutes(2), [], []));

        var affected = await fixture.Store.MarkRunningAsInterruptedAsync(fixture.Now.AddMinutes(2));
        var saved = await fixture.Store.ListByModuleAsync("rebate", 20);

        Assert.AreEqual(2, affected);
        Assert.AreEqual(2, saved.Count(run => run.Status == RunStatus.Interrupted));
        Assert.AreEqual(RunStatus.Success, saved.Single(run => run.RunId == terminalRunId).Status);
        Assert.AreEqual(fixture.Now.AddMinutes(2), saved.Single(run => run.RunId == terminalRunId).EndedAt);
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
