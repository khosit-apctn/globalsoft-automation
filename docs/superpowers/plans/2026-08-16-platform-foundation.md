# Automation Platform Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** สร้าง .NET 10 WPF platform shell ที่ค้นพบ compile-time modules, บันทึก run history, ควบคุม run lifecycle และแสดง Automation catalog ได้โดยยังไม่ผูกกับ Rebate logic

**Architecture:** ใช้ Modular Monolith โดยให้ `Automation.Platform.Contracts` เป็น stable boundary, `Automation.Platform` เป็น application services, `Automation.Infrastructure` เป็น SQLite/filesystem adapters และ `Automation.Desktop` เป็น WPF composition root แบบ MVVM ทุก dependency ถูกประกอบผ่าน Generic Host

**Tech Stack:** .NET 10 LTS, WPF, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.10, Microsoft.Data.Sqlite 10.0.10, MSTest 4.3.3

## Global Constraints

- Target framework คือ `net10.0` หรือ `net10.0-windows` เท่านั้น
- เปิด `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` และ deterministic builds ทุก project
- ใช้ Central Package Management ผ่าน `Directory.Packages.props`
- Platform Core ห้ามมี POI, Invoice, selector, Legacy locator หรือ Rebate column
- Module registration เป็น compile-time; ห้ามใช้ dynamic DLL loading ใน MVP
- Run status ใช้เฉพาะ `RUNNING`, `SUCCESS`, `PARTIAL_FAILED`, `FAILED`, `INTERRUPTED`, `CANCELLED`
- ไม่มี automatic retry และไม่มี resume หลัง process interruption
- ไม่สร้าง automated WPF View/ViewModel test suite ใน MVP; ใช้ manual UI smoke check
- Environment ปัจจุบันไม่มี `dotnet`; การติดตั้ง SDK เป็น prerequisite ที่ต้องขออนุญาตเมื่อเริ่ม execution

---

## File Map

- `global.json` — กำหนด .NET 10 SDK family และ roll-forward policy
- `Directory.Build.props` — compiler/build rules ร่วม
- `Directory.Packages.props` — package versions กลาง
- `GlobalsoftAutomation.sln` — solution root
- `src/Automation.Platform.Contracts/` — module/run contracts ไม่มี infrastructure dependency
- `src/Automation.Platform/` — module catalog และ run coordinator
- `src/Automation.Infrastructure/` — SQLite run history
- `src/Automation.Desktop/` — WPF shell, Generic Host, navigation และ catalog UI
- `tests/Automation.Platform.Tests/` — catalog/run coordinator tests
- `tests/Automation.Infrastructure.Tests/` — SQLite integration tests

### Task 1: Bootstrap the .NET 10 solution

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `GlobalsoftAutomation.sln`
- Create: `src/Automation.Platform.Contracts/Automation.Platform.Contracts.csproj`
- Create: `src/Automation.Platform/Automation.Platform.csproj`
- Create: `src/Automation.Infrastructure/Automation.Infrastructure.csproj`
- Create: `src/Automation.Desktop/Automation.Desktop.csproj`
- Create: `tests/Automation.Platform.Tests/Automation.Platform.Tests.csproj`
- Create: `tests/Automation.Infrastructure.Tests/Automation.Infrastructure.Tests.csproj`

**Interfaces:**
- Consumes: approved design spec only
- Produces: buildable solution and project dependency graph used by all later tasks

- [ ] **Step 1: Verify or install the .NET 10 SDK**

Run:

```powershell
dotnet --list-sdks
```

Expected: at least one `10.0.x` SDK. If absent, request permission and run:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
dotnet --list-sdks
```

Expected: command exits `0` and lists a .NET 10 SDK.

- [ ] **Step 2: Create SDK and shared build configuration**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.10" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.10" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.3.3" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.3.3" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Scaffold projects and wire references**

Run:

```powershell
dotnet new sln -n GlobalsoftAutomation --format sln
dotnet new classlib -n Automation.Platform.Contracts -o src/Automation.Platform.Contracts -f net10.0
dotnet new classlib -n Automation.Platform -o src/Automation.Platform -f net10.0
dotnet new classlib -n Automation.Infrastructure -o src/Automation.Infrastructure -f net10.0
dotnet new wpf -n Automation.Desktop -o src/Automation.Desktop -f net10.0
dotnet new mstest -n Automation.Platform.Tests -o tests/Automation.Platform.Tests -f net10.0
dotnet new mstest -n Automation.Infrastructure.Tests -o tests/Automation.Infrastructure.Tests -f net10.0
dotnet sln GlobalsoftAutomation.sln add src/Automation.Platform.Contracts src/Automation.Platform src/Automation.Infrastructure src/Automation.Desktop tests/Automation.Platform.Tests tests/Automation.Infrastructure.Tests
dotnet add src/Automation.Platform reference src/Automation.Platform.Contracts
dotnet add src/Automation.Infrastructure reference src/Automation.Platform.Contracts
dotnet add src/Automation.Desktop reference src/Automation.Platform.Contracts src/Automation.Platform src/Automation.Infrastructure
dotnet add tests/Automation.Platform.Tests reference src/Automation.Platform src/Automation.Platform.Contracts
dotnet add tests/Automation.Infrastructure.Tests reference src/Automation.Infrastructure src/Automation.Platform.Contracts
```

Edit project files so package references have no inline versions:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" />
<PackageReference Include="CommunityToolkit.Mvvm" />
<PackageReference Include="Microsoft.Data.Sqlite" />
```

- [ ] **Step 4: Verify the empty solution**

Run:

```powershell
dotnet restore GlobalsoftAutomation.sln
dotnet build GlobalsoftAutomation.sln --no-restore
dotnet test GlobalsoftAutomation.sln --no-build
```

Expected: restore/build/test exit `0`; generated smoke tests pass.

- [ ] **Step 5: Commit**

```powershell
git add global.json Directory.Build.props Directory.Packages.props GlobalsoftAutomation.sln src tests
git commit -m "build: bootstrap .NET automation platform"
```

### Task 2: Define module and run contracts

**Files:**
- Create: `src/Automation.Platform.Contracts/Modules/AutomationDescriptor.cs`
- Create: `src/Automation.Platform.Contracts/Modules/IAutomationModule.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunStatus.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunContext.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunProgress.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunFailure.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunArtifact.cs`
- Create: `src/Automation.Platform.Contracts/Runs/RunResult.cs`
- Test: `tests/Automation.Platform.Tests/Contracts/RunResultTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `IAutomationModule`, `AutomationDescriptor`, `RunStatus`, `RunContext`, `RunProgress`, `RunFailure`, `RunArtifact`, `RunResult`

- [ ] **Step 1: Write failing result invariant tests**

```csharp
[TestClass]
public sealed class RunResultTests
{
    [TestMethod]
    public void Success_cannot_contain_failures()
    {
        var failure = new RunFailure("web", "POI-1", "read", "ELEMENT_NOT_FOUND", "missing", null);
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunResult.Create(RunStatus.Success, [failure], []));
    }

    [TestMethod]
    public void Partial_failed_requires_at_least_one_failure()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunResult.Create(RunStatus.PartialFailed, [], []));
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

```powershell
dotnet test tests/Automation.Platform.Tests --filter FullyQualifiedName~RunResultTests
```

Expected: FAIL because contract types do not exist.

- [ ] **Step 3: Implement immutable contracts**

Use exact public shapes:

```csharp
public sealed record AutomationDescriptor(string Id, string DisplayName, string IconKey);

public interface IAutomationModule
{
    AutomationDescriptor Descriptor { get; }
}

public enum RunStatus { Running, Success, PartialFailed, Failed, Interrupted, Cancelled }

public sealed record RunFailure(
    string Source,
    string? ItemKey,
    string Step,
    string ErrorCode,
    string Message,
    string? ScreenshotPath);

public sealed record RunArtifact(string Kind, string DisplayName, string Path);

public sealed record RunContext(Guid RunId, string ModuleId, DateTimeOffset StartedAt, string ArtifactDirectory);

public sealed record RunProgress(string Stage, string? CurrentItem, int Completed, int? Total);
```

Implement `RunResult.Create` so the two tested invariants throw `ArgumentException`; expose read-only `Failures` and `Artifacts`.

- [ ] **Step 4: Run tests and verify GREEN**

```powershell
dotnet test tests/Automation.Platform.Tests --filter FullyQualifiedName~RunResultTests
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Automation.Platform.Contracts tests/Automation.Platform.Tests
git commit -m "feat: define automation module and run contracts"
```

### Task 3: Implement the module catalog

**Files:**
- Create: `src/Automation.Platform/Modules/IModuleCatalog.cs`
- Create: `src/Automation.Platform/Modules/ModuleCatalog.cs`
- Create: `src/Automation.Platform/DependencyInjection.cs`
- Test: `tests/Automation.Platform.Tests/Modules/ModuleCatalogTests.cs`

**Interfaces:**
- Consumes: `IAutomationModule`, `AutomationDescriptor`
- Produces: `IModuleCatalog.Modules`, `IModuleCatalog.GetRequired(string moduleId)`, `AddAutomationPlatform(IServiceCollection)`

- [ ] **Step 1: Write duplicate-ID and ordering tests**

```csharp
[TestMethod]
public void Constructor_rejects_duplicate_module_ids()
{
    IAutomationModule[] modules = [new StubModule("rebate"), new StubModule("rebate")];
    Assert.ThrowsExactly<InvalidOperationException>(() => new ModuleCatalog(modules));
}

[TestMethod]
public void Modules_are_sorted_by_display_name()
{
    var catalog = new ModuleCatalog([new StubModule("z", "Zulu"), new StubModule("a", "Alpha")]);
    CollectionAssert.AreEqual(new[] { "a", "z" }, catalog.Modules.Select(x => x.Descriptor.Id).ToArray());
}

private sealed class StubModule(string id, string? name = null, string icon = "test") : IAutomationModule
{
    public AutomationDescriptor Descriptor { get; } = new(id, name ?? id, icon);
}
```

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/Automation.Platform.Tests --filter FullyQualifiedName~ModuleCatalogTests
```

Expected: FAIL because `ModuleCatalog` is missing.

- [ ] **Step 3: Implement catalog and DI registration**

```csharp
public interface IModuleCatalog
{
    IReadOnlyList<IAutomationModule> Modules { get; }
    IAutomationModule GetRequired(string moduleId);
}
```

`ModuleCatalog` materializes modules once, rejects duplicate IDs with `StringComparer.OrdinalIgnoreCase`, sorts by `DisplayName`, and throws `KeyNotFoundException` for unknown IDs. Register it as singleton in `AddAutomationPlatform`.

- [ ] **Step 4: Run full platform tests**

```powershell
dotnet test tests/Automation.Platform.Tests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Automation.Platform tests/Automation.Platform.Tests
git commit -m "feat: add compile-time automation module catalog"
```

### Task 4: Persist run history in SQLite

**Files:**
- Create: `src/Automation.Platform.Contracts/Runs/RunRecord.cs`
- Create: `src/Automation.Platform.Contracts/Runs/IRunHistoryStore.cs`
- Create: `src/Automation.Infrastructure/Runs/SqliteRunHistoryStore.cs`
- Create: `src/Automation.Infrastructure/DependencyInjection.cs`
- Modify: `src/Automation.Infrastructure/Automation.Infrastructure.csproj`
- Create: `tests/Automation.Infrastructure.Tests/Runs/SqliteFixture.cs`
- Test: `tests/Automation.Infrastructure.Tests/Runs/SqliteRunHistoryStoreTests.cs`

**Interfaces:**
- Consumes: `RunStatus`, `RunFailure`, `RunArtifact`
- Produces: `IRunHistoryStore.InitializeAsync`, `CreateAsync`, `CompleteAsync`, `MarkRunningAsInterruptedAsync`, `ListByModuleAsync`

- [ ] **Step 1: Add the isolated SQLite test fixture**

```csharp
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
```

- [ ] **Step 2: Write a failing round-trip test**

```csharp
[TestMethod]
public async Task Completed_run_round_trips_with_failures_and_artifacts()
{
    await using var fixture = await SqliteFixture.CreateAsync();
    var runId = Guid.NewGuid();
    await fixture.Store.CreateAsync(new RunRecord(runId, "rebate", "2026-07", RunStatus.Running, fixture.Now, null, [], []));
    await fixture.Store.CompleteAsync(runId, RunStatus.PartialFailed, fixture.Now.AddMinutes(1),
        [new RunFailure("web", "POI-1", "detail", "ELEMENT_NOT_FOUND", "missing", "shot.png")],
        [new RunArtifact("rebate", "Rebate.xlsx", "out/Rebate.xlsx")]);

    var savedRuns = await fixture.Store.ListByModuleAsync("rebate", 20);
    Assert.AreEqual(1, savedRuns.Count);
    var saved = savedRuns[0];
    Assert.AreEqual(RunStatus.PartialFailed, saved.Status);
    Assert.AreEqual(1, saved.Failures.Count);
    Assert.AreEqual("POI-1", saved.Failures[0].ItemKey);
}
```

- [ ] **Step 3: Run and verify RED**

```powershell
dotnet test tests/Automation.Infrastructure.Tests --filter FullyQualifiedName~SqliteRunHistoryStoreTests
```

Expected: FAIL because the store is missing.

- [ ] **Step 4: Implement schema and parameterized SQL**

Create tables `runs`, `run_failures`, and `run_artifacts`. Store enum values as uppercase strings. Every multi-table create/complete operation uses one SQLite transaction. Serialize no credentials or browser state.

Implement `AddAutomationInfrastructure(IServiceCollection, IConfiguration)` to resolve database path as `%LocalAppData%\GlobalsoftAutomation\runs.db`, register `SqliteRunHistoryStore` once as both concrete type and `IRunHistoryStore`, and register `ArtifactDirectoryFactory` after Task 5.

Use exact contract:

```csharp
public interface IRunHistoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(RunRecord run, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid runId, RunStatus status, DateTimeOffset endedAt,
        IReadOnlyList<RunFailure> failures, IReadOnlyList<RunArtifact> artifacts,
        CancellationToken cancellationToken = default);
    Task<int> MarkRunningAsInterruptedAsync(DateTimeOffset endedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RunRecord>> ListByModuleAsync(string moduleId, int limit,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Add interruption test and run GREEN**

Add a test that creates two `RUNNING` rows, calls `MarkRunningAsInterruptedAsync`, asserts return value `2`, and verifies both rows are `INTERRUPTED`.

```powershell
dotnet test tests/Automation.Infrastructure.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Automation.Platform.Contracts src/Automation.Infrastructure tests/Automation.Infrastructure.Tests
git commit -m "feat: persist automation run history"
```

### Task 5: Implement run coordination

**Files:**
- Create: `src/Automation.Platform/Runs/IRunCoordinator.cs`
- Create: `src/Automation.Platform/Runs/RunCoordinator.cs`
- Create: `src/Automation.Platform/Runs/IArtifactDirectoryFactory.cs`
- Create: `src/Automation.Infrastructure/Runs/ArtifactDirectoryFactory.cs`
- Modify: `src/Automation.Infrastructure/DependencyInjection.cs`
- Modify: `src/Automation.Platform/DependencyInjection.cs`
- Create: `tests/Automation.Platform.Tests/Runs/FakeRunHistoryStore.cs`
- Create: `tests/Automation.Platform.Tests/Runs/FakeArtifactDirectoryFactory.cs`
- Test: `tests/Automation.Platform.Tests/Runs/RunCoordinatorTests.cs`

**Interfaces:**
- Consumes: `IRunHistoryStore`, `RunContext`, `RunResult`
- Produces: `IRunCoordinator.ExecuteAsync(string moduleId, string inputLabel, Func<RunContext,CancellationToken,Task<RunResult>> workflow, CancellationToken)`

- [ ] **Step 1: Write failing lifecycle tests**

Cover these exact cases:

```csharp
[TestMethod]
public async Task Execute_records_success_and_returns_artifacts()
{
    var store = new FakeRunHistoryStore();
    var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
    var artifact = new RunArtifact("rebate", "Rebate.xlsx", "artifacts/Rebate.xlsx");
    var result = await coordinator.ExecuteAsync("rebate", "2026-07",
        (_, _) => Task.FromResult(RunResult.Create(RunStatus.Success, [], [artifact])), default);
    Assert.AreEqual(RunStatus.Success, result.Status);
    Assert.AreEqual(1, store.Created.Count);
    Assert.AreEqual(1, store.Completed.Count);
    Assert.AreEqual(artifact, store.Completed[0].Artifacts[0]);
}

[TestMethod]
public async Task Execute_converts_unhandled_exception_to_failed_result()
{
    var store = new FakeRunHistoryStore();
    var coordinator = new RunCoordinator(store, new FakeArtifactDirectoryFactory("artifacts"));
    var result = await coordinator.ExecuteAsync("rebate", "2026-07",
        (_, _) => throw new InvalidOperationException("boom"), default);
    Assert.AreEqual(RunStatus.Failed, result.Status);
    Assert.AreEqual("UNHANDLED", result.Failures[0].ErrorCode);
}

[TestMethod]
public async Task Execute_rejects_concurrent_run_for_same_module()
{
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var coordinator = new RunCoordinator(new FakeRunHistoryStore(), new FakeArtifactDirectoryFactory("artifacts"));
    var first = coordinator.ExecuteAsync("rebate", "2026-07", async (_, _) =>
    {
        entered.SetResult();
        await release.Task;
        return RunResult.Create(RunStatus.Success, [], []);
    }, default);
    await entered.Task;
    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
        coordinator.ExecuteAsync("rebate", "2026-08", (_, _) =>
            Task.FromResult(RunResult.Create(RunStatus.Success, [], [])), default));
    release.SetResult();
    await first;
}
```

Assert no retry by verifying the workflow delegate is invoked exactly once.

- [ ] **Step 2: Run and verify RED**

```powershell
dotnet test tests/Automation.Platform.Tests --filter FullyQualifiedName~RunCoordinatorTests
```

Expected: FAIL because coordinator types do not exist.

- [ ] **Step 3: Implement deterministic coordinator fakes**

`FakeRunHistoryStore` implements all `IRunHistoryStore` members in memory and exposes `Created` plus `Completed` collections for assertions. `FakeArtifactDirectoryFactory` returns `<root>/<runId:N>` without touching disk. Keep both types in the test project.

- [ ] **Step 4: Implement the coordinator**

Use a `ConcurrentDictionary<string, SemaphoreSlim>` keyed case-insensitively. `ExecuteAsync` must:

1. fail fast if the module lock is busy;
2. create Run ID and artifact directory;
3. persist `RUNNING`;
4. invoke the workflow once;
5. persist returned status/failures/artifacts;
6. convert cancellation to `CANCELLED`;
7. convert other unhandled exceptions to `FAILED` with error code `UNHANDLED`;
8. always release the module lock.

- [ ] **Step 5: Run coordinator and full solution tests**

```powershell
dotnet test GlobalsoftAutomation.sln
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Automation.Platform src/Automation.Infrastructure tests/Automation.Platform.Tests
git commit -m "feat: coordinate automation run lifecycle"
```

### Task 6: Build the WPF shell and Automation catalog

**Files:**
- Modify: `src/Automation.Desktop/App.xaml`
- Modify: `src/Automation.Desktop/App.xaml.cs`
- Modify: `src/Automation.Desktop/MainWindow.xaml`
- Modify: `src/Automation.Desktop/MainWindow.xaml.cs`
- Create: `src/Automation.Desktop/ViewModels/MainWindowViewModel.cs`
- Create: `src/Automation.Desktop/ViewModels/AutomationCatalogViewModel.cs`
- Create: `src/Automation.Desktop/ViewModels/AutomationCardViewModel.cs`
- Create: `src/Automation.Desktop/Views/AutomationCatalogView.xaml`
- Create: `src/Automation.Desktop/Views/AutomationCatalogView.xaml.cs`

**Interfaces:**
- Consumes: `IModuleCatalog`, `AutomationDescriptor`
- Produces: WPF shell whose default page is Automations and whose catalog is generated from registered modules

- [ ] **Step 1: Configure Generic Host and implement the shell**

`App.xaml` has no `StartupUri`. `App.xaml.cs` must:

```csharp
var builder = Host.CreateApplicationBuilder();
builder.Services.AddAutomationPlatform();
builder.Services.AddAutomationInfrastructure(builder.Configuration);
builder.Services.AddSingleton<MainWindow>();
builder.Services.AddSingleton<MainWindowViewModel>();
builder.Services.AddSingleton<AutomationCatalogViewModel>();
_host = builder.Build();
await _host.StartAsync();
await _host.Services.GetRequiredService<IRunHistoryStore>().InitializeAsync();
await _host.Services.GetRequiredService<IRunHistoryStore>()
    .MarkRunningAsInterruptedAsync(DateTimeOffset.UtcNow);
_host.Services.GetRequiredService<MainWindow>().Show();
```

On exit, call `StopAsync` and dispose the host. Bind MainWindow content to `AutomationCatalogViewModel`; show an explicit empty-state message when no modules are registered. Do not create Home or global Run History navigation entries.

- [ ] **Step 2: Run automated and manual smoke checks**

```powershell
dotnet test GlobalsoftAutomation.sln
dotnet run --project src/Automation.Desktop
```

Expected: all tests pass; WPF window opens on `Automations`, displays `บัญชีผู้ใช้` and `ตั้งค่าระบบ` navigation, and shows the empty catalog state without crashing.

- [ ] **Step 3: Commit**

```powershell
git add src/Automation.Desktop
git commit -m "feat: add WPF automation catalog shell"
```

## Completion Check

Run fresh:

```powershell
dotnet restore GlobalsoftAutomation.sln
dotnet build GlobalsoftAutomation.sln --no-restore
dotnet test GlobalsoftAutomation.sln --no-build
git status --short
```

Expected: build/test exit `0`; only intentionally untracked user assets may remain. Launch the WPF app once and verify the empty Automation catalog. Do not push or create a PR unless explicitly requested.
