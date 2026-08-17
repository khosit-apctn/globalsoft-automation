using Automation.Platform.Contracts.Runs;
using Automation.Platform.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Platform.Tests.Runs;

[TestClass]
public sealed class RunCoordinatorTests
{
    [TestMethod]
    public async Task Execute_records_success_and_returns_artifacts()
    {
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
        var artifact = new RunArtifact("rebate", "Rebate.xlsx", "artifacts/Rebate.xlsx");
        RunContext? receivedContext = null;

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (context, _) =>
            {
                receivedContext = context;
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], [artifact]));
            },
            default);

        Assert.AreEqual(RunStatus.Success, result.Status);
        Assert.AreEqual(1, store.Created.Count);
        Assert.AreEqual(RunStatus.Running, store.Created[0].Status);
        Assert.AreEqual("rebate", store.Created[0].ModuleId);
        Assert.AreEqual("2026-07", store.Created[0].InputLabel);
        Assert.AreEqual(1, store.Completed.Count);
        Assert.AreEqual(store.Created[0].RunId, store.Completed[0].RunId);
        Assert.IsNotNull(receivedContext);
        Assert.AreEqual(store.Created[0].RunId, receivedContext.RunId);
        Assert.AreEqual(Path.Combine("artifacts", receivedContext.RunId.ToString("N")), receivedContext.ArtifactDirectory);
        Assert.AreEqual(artifact, store.Completed[0].Artifacts[0]);
    }

    [TestMethod]
    public async Task Execute_invokes_workflow_exactly_once()
    {
        var invocationCount = 0;
        var coordinator = new RunCoordinator(new FakeRunHistoryStore(), new FakeArtifactDirectoryFactory("artifacts"));

        await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) =>
            {
                invocationCount++;
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], []));
            },
            default);

        Assert.AreEqual(1, invocationCount);
    }

    [TestMethod]
    public async Task Execute_converts_unhandled_exception_to_one_generic_failed_result()
    {
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => throw new InvalidOperationException("password=secret-token"),
            default);

        Assert.AreEqual(RunStatus.Failed, result.Status);
        Assert.AreEqual(1, result.Failures.Count);
        Assert.AreEqual("UNHANDLED", result.Failures[0].ErrorCode);
        Assert.AreEqual("The workflow failed unexpectedly.", result.Failures[0].Message);
        Assert.DoesNotContain("secret", result.Failures[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", result.Failures[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(RunStatus.Failed, store.Completed[0].Status);
        Assert.AreEqual(result.Failures[0], store.Completed[0].Failures.Single());
    }

    [TestMethod]
    public async Task Execute_converts_running_workflow_result_to_one_generic_failed_result()
    {
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Running, [], [])),
            default);

        AssertGenericUnhandledFailure(result, store);
    }

    [TestMethod]
    public async Task Execute_converts_undefined_workflow_result_to_one_generic_failed_result()
    {
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => Task.FromResult(RunResult.Create((RunStatus)999, [], [])),
            default);

        AssertGenericUnhandledFailure(result, store);
    }

    [TestMethod]
    public async Task Execute_with_pre_cancelled_token_still_records_running_invokes_workflow_and_persists_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
        var workflowInvocations = 0;

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, token) =>
            {
                workflowInvocations++;
                Assert.AreEqual(cancellation.Token, token);
                token.ThrowIfCancellationRequested();
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], []));
            },
            cancellation.Token);

        Assert.AreEqual(1, workflowInvocations);
        Assert.AreEqual(1, store.Created.Count);
        Assert.AreEqual(RunStatus.Running, store.Created[0].Status);
        Assert.AreEqual(1, store.Completed.Count);
        Assert.AreEqual(RunStatus.Cancelled, result.Status);
        Assert.AreEqual(RunStatus.Cancelled, store.Completed[0].Status);
    }

    [TestMethod]
    public async Task Execute_persists_running_with_a_non_cancelable_token()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        _ = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            cancellation.Token);

        Assert.AreEqual(1, store.CreateCancellationTokens.Count);
        Assert.IsFalse(store.CreateCancellationTokens[0].CanBeCanceled);
    }

    [TestMethod]
    public async Task Execute_converts_caller_cancellation_and_persists_with_non_cancelled_token()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        var result = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], []));
            },
            cancellation.Token);

        Assert.AreEqual(RunStatus.Cancelled, result.Status);
        Assert.AreEqual(1, store.Completed.Count);
        Assert.AreEqual(RunStatus.Cancelled, store.Completed[0].Status);
        Assert.IsFalse(store.Completed[0].CancellationToken.CanBeCanceled);
    }

    [TestMethod]
    public async Task Execute_rejects_concurrent_run_for_same_module_ignoring_case_without_waiting()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
        var first = coordinator.ExecuteAsync("rebate", "2026-07", async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
            return RunResult.Create(RunStatus.Success, [], []);
        }, default);
        await entered.Task;

        var rejected = coordinator.ExecuteAsync(
            "REBATE",
            "2026-08",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default);

        Assert.IsTrue(rejected.IsCompleted);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => rejected);
        Assert.AreEqual(1, store.Created.Count);
        release.SetResult();
        await first;
    }

    [TestMethod]
    public async Task Execute_releases_module_after_workflow_failure()
    {
        var coordinator = new RunCoordinator(new FakeRunHistoryStore(), new FakeArtifactDirectoryFactory("artifacts"));
        _ = await coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => throw new InvalidOperationException("boom"),
            default);

        var second = await coordinator.ExecuteAsync(
            "REBATE",
            "2026-08",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default);

        Assert.AreEqual(RunStatus.Success, second.Status);
    }

    [TestMethod]
    public async Task Execute_propagates_create_failure_without_running_or_completing_and_releases_module()
    {
        var createFailure = new IOException("database unavailable");
        var store = new FakeRunHistoryStore();
        store.CreateOverride = (_, _) => throw createFailure;
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
        var workflowInvocations = 0;

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) =>
            {
                workflowInvocations++;
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], []));
            },
            default));

        Assert.AreSame(createFailure, actual);
        Assert.AreEqual(0, workflowInvocations);
        Assert.AreEqual(0, store.Completed.Count);
        store.CreateOverride = null;
        var retry = await coordinator.ExecuteAsync(
            "REBATE",
            "2026-08",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default);
        Assert.AreEqual(RunStatus.Success, retry.Status);
    }

    [TestMethod]
    public async Task Execute_propagates_artifact_directory_failure_without_persistence_or_workflow_and_releases_module()
    {
        var artifactFailure = new IOException("artifact disk unavailable");
        var artifactFactory = new FakeArtifactDirectoryFactory("artifacts")
        {
            CreateFailure = artifactFailure
        };
        var store = new FakeRunHistoryStore();
        var coordinator = new RunCoordinator(store, artifactFactory);
        var workflowInvocations = 0;

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) =>
            {
                workflowInvocations++;
                return Task.FromResult(RunResult.Create(RunStatus.Success, [], []));
            },
            default));

        Assert.AreSame(artifactFailure, actual);
        Assert.AreEqual(0, store.CreateAttempts);
        Assert.AreEqual(0, workflowInvocations);
        Assert.AreEqual(0, store.CompleteAttempts);
        artifactFactory.CreateFailure = null;
        var retry = await coordinator.ExecuteAsync(
            "REBATE",
            "2026-08",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default);
        Assert.AreEqual(RunStatus.Success, retry.Status);
    }

    [TestMethod]
    public async Task Execute_propagates_completion_failure_once_and_releases_module()
    {
        var completionFailure = new IOException("database unavailable");
        var store = new FakeRunHistoryStore();
        store.CompleteOverride = (_, _, _, _, _, _) => throw completionFailure;
        var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.ExecuteAsync(
            "rebate",
            "2026-07",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default));

        Assert.AreSame(completionFailure, actual);
        Assert.AreEqual(1, store.CompleteAttempts);
        store.CompleteOverride = null;
        var retry = await coordinator.ExecuteAsync(
            "REBATE",
            "2026-08",
            (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [])),
            default);
        Assert.AreEqual(RunStatus.Success, retry.Status);
    }

    [TestMethod]
    public void AddAutomationPlatform_registers_coordinator_once_as_its_interface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRunHistoryStore>(new FakeRunHistoryStore());
        services.AddSingleton<IArtifactDirectoryFactory>(new FakeArtifactDirectoryFactory("artifacts"));
        services.AddAutomationPlatform();
        var descriptors = services.Where(service => service.ServiceType == typeof(IRunCoordinator)).ToArray();
        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<RunCoordinator>();
        var first = provider.GetRequiredService<IRunCoordinator>();
        var second = provider.GetRequiredService<IRunCoordinator>();

        Assert.AreEqual(1, descriptors.Length);
        Assert.AreEqual(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.AreSame(concrete, first);
        Assert.AreSame(first, second);
    }

    private static void AssertGenericUnhandledFailure(RunResult result, FakeRunHistoryStore store)
    {
        Assert.AreEqual(RunStatus.Failed, result.Status);
        Assert.AreEqual(1, result.Failures.Count);
        Assert.AreEqual("UNHANDLED", result.Failures[0].ErrorCode);
        Assert.AreEqual("The workflow failed unexpectedly.", result.Failures[0].Message);
        Assert.AreEqual(1, store.Completed.Count);
        Assert.AreEqual(RunStatus.Failed, store.Completed[0].Status);
        Assert.AreEqual(result.Failures[0], store.Completed[0].Failures.Single());
    }
}
