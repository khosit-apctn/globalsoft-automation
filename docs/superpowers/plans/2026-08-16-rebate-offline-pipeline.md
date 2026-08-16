# Rebate Offline Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** สร้าง Rebate module ที่ทดสอบ business rules, POI reconciliation, per-POI failure handling, Rebate Excel และ Run Report ได้ครบด้วย fake collectors โดยยังไม่เชื่อม Globalsoft Web หรือ Windows Legacy จริง

**Architecture:** Rebate เป็น vertical slice เดียวที่มี Domain, Application, Adapters/Excel และ Presentation อยู่ใน module project เดียว Application เรียก source ports และ output ports เท่านั้น ทำให้ live collectors ถูกเพิ่มภายหลังโดยไม่แก้ business rules

**Tech Stack:** .NET 10 LTS, ClosedXML 0.105.1, CommunityToolkit.Mvvm 8.4.2, MSTest 4.3.3, platform contracts จากแผน Foundation

## Global Constraints

- ต้องทำแผน `2026-08-16-platform-foundation.md` เสร็จก่อน
- Rebate module target `net10.0-windows`
- POI key ระหว่างระบบคือ `POI Document No`
- POI ซ้ำเลือก Web ทั้งชุด; ห้ามผสม field จาก Legacy
- รอบเดือนตัดสินด้วย `Tax Invoice Date`
- หนึ่ง output row ต่อหนึ่ง product line และ Invoice No/Date ซ้ำได้
- Product/Invoice identifiers เก็บเป็น `string`; Date/Qty/Value เป็น typed values
- ไม่มี retry; POI-level error ต้องข้ามรายการนั้นและทำรายการถัดไป
- `PARTIAL_FAILED` ต้องสร้าง Rebate Excel จาก POI สำเร็จพร้อม Run Report
- ห้าม deduplicate ด้วย Invoice No + Product Code เพียงอย่างเดียว
- ไม่สร้าง automated UI/ViewModel tests; UI ตรวจด้วย manual smoke เท่านั้น

---

## File Map

- `src/Automation.Modules.Rebate/Domain/` — canonical models และ pure rules
- `src/Automation.Modules.Rebate/Application/` — source/output ports และ `RunRebateWorkflow`
- `src/Automation.Modules.Rebate/Adapters/Excel/` — ClosedXML workbook/report writers
- `src/Automation.Modules.Rebate/Presentation/` — Rebate start/history ViewModels และ Views
- `src/Automation.Modules.Rebate/Assets/Excel_Template.xlsx` — approved workbook template copied as application content
- `tests/Automation.Modules.Rebate.Tests/` — core rules, workflow และ Excel verification

### Task 1: Scaffold the Rebate module and pin ClosedXML

**Files:**
- Create: `src/Automation.Modules.Rebate/Automation.Modules.Rebate.csproj`
- Create: `tests/Automation.Modules.Rebate.Tests/Automation.Modules.Rebate.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `GlobalsoftAutomation.sln`
- Create: `src/Automation.Modules.Rebate/RebateModule.cs`
- Create: `tests/Automation.Modules.Rebate.Tests/RebateTestData.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/RebateModuleTests.cs`

**Interfaces:**
- Consumes: `IAutomationModule`, `AutomationDescriptor`
- Produces: compile-time module descriptor with ID `rebate`

- [ ] **Step 1: Add package version and scaffold projects**

Add to `Directory.Packages.props`:

```xml
<PackageVersion Include="ClosedXML" Version="0.105.1" />
```

Run:

```powershell
dotnet new classlib -n Automation.Modules.Rebate -o src/Automation.Modules.Rebate -f net10.0
dotnet new mstest -n Automation.Modules.Rebate.Tests -o tests/Automation.Modules.Rebate.Tests -f net10.0
dotnet sln GlobalsoftAutomation.sln add src/Automation.Modules.Rebate tests/Automation.Modules.Rebate.Tests
dotnet add src/Automation.Modules.Rebate reference src/Automation.Platform.Contracts src/Automation.Platform
dotnet add tests/Automation.Modules.Rebate.Tests reference src/Automation.Modules.Rebate src/Automation.Platform.Contracts
```

Change both projects to `net10.0-windows`; add package references to `ClosedXML` and `CommunityToolkit.Mvvm` in the production project.

- [ ] **Step 2: Write the failing descriptor test**

```csharp
[TestMethod]
public void Descriptor_is_stable()
{
    var module = new RebateModule();
    Assert.AreEqual("rebate", module.Descriptor.Id);
    Assert.AreEqual("Rebate — Attach Tax Invoice", module.Descriptor.DisplayName);
}
```

- [ ] **Step 3: Run RED, implement descriptor, run GREEN**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~RebateModuleTests
```

Expected first run: FAIL. Implement:

```csharp
public sealed class RebateModule : IAutomationModule
{
    public AutomationDescriptor Descriptor { get; } =
        new("rebate", "Rebate — Attach Tax Invoice", "receipt");
}
```

Run the same test again; expected PASS.

- [ ] **Step 4: Add shared deterministic test data**

Create:

```csharp
internal static class RebateTestData
{
    internal static InvoiceLine Line(string productName, int ordinal = 1) =>
        new("INV-001", new DateOnly(2026, 7, 15), "001234", "SUP-01",
            productName, 10m, 1250.50m, null, ordinal);

    internal static InvoiceLine JulyLine(int ordinal = 1) => Line("July product", ordinal);

    internal static PoiDocument Poi(string documentNo, SourceSystem source, params InvoiceLine[] lines) =>
        new(documentNo, source, lines);
}
```

This helper is test-only and contains no customer data.

- [ ] **Step 5: Commit**

```powershell
git add Directory.Packages.props GlobalsoftAutomation.sln src/Automation.Modules.Rebate tests/Automation.Modules.Rebate.Tests
git commit -m "feat: scaffold Rebate automation module"
```

### Task 2: Implement canonical models and monthly filtering

**Files:**
- Create: `src/Automation.Modules.Rebate/Domain/SourceSystem.cs`
- Create: `src/Automation.Modules.Rebate/Domain/InvoiceLine.cs`
- Create: `src/Automation.Modules.Rebate/Domain/PoiDocument.cs`
- Create: `src/Automation.Modules.Rebate/Domain/RebatePeriod.cs`
- Create: `src/Automation.Modules.Rebate/Domain/RebateLineValidator.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Domain/RebatePeriodTests.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Domain/RebateLineValidatorTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `InvoiceLine`, `PoiDocument`, `RebatePeriod.Contains(DateOnly)`, `RebatePeriod.SearchWindow`, `RebateLineValidator.Validate`

- [ ] **Step 1: Write failing period tests**

```csharp
[TestMethod]
public void July_period_includes_only_July_tax_invoice_dates()
{
    var period = new RebatePeriod(2026, 7);
    Assert.IsTrue(period.Contains(new DateOnly(2026, 7, 1)));
    Assert.IsTrue(period.Contains(new DateOnly(2026, 7, 31)));
    Assert.IsFalse(period.Contains(new DateOnly(2026, 6, 30)));
    Assert.IsFalse(period.Contains(new DateOnly(2026, 8, 1)));
}

[TestMethod]
public void Search_window_spans_previous_through_next_month()
{
    var window = new RebatePeriod(2026, 7).SearchWindow;
    Assert.AreEqual(new DateOnly(2026, 6, 1), window.From);
    Assert.AreEqual(new DateOnly(2026, 8, 31), window.To);
}
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~RebatePeriodTests
```

Expected: FAIL because domain types do not exist.

- [ ] **Step 3: Implement immutable models**

Use these exact shapes:

```csharp
public enum SourceSystem { Web, Legacy }

public sealed record InvoiceLine(
    string TaxInvoiceNo,
    DateOnly TaxInvoiceDate,
    string GlobalHouseProductCode,
    string SupplierProductCode,
    string ProductName,
    decimal Quantity,
    decimal ValueExcludingVat,
    string? SourceLineId,
    int SourceOrdinal);

public sealed record PoiDocument(
    string DocumentNo,
    SourceSystem Source,
    IReadOnlyList<InvoiceLine> Lines);

public readonly record struct DateWindow(DateOnly From, DateOnly To);
```

`RebatePeriod` validates month `1..12`, exposes first/last day, `Contains`, and the three-month search window.

- [ ] **Step 4: Add validation tests and implementation**

Test whitespace identifiers/product name, negative quantity, and negative value. `Validate` returns stable string error codes: `TAX_INVOICE_REQUIRED`, `GH_CODE_REQUIRED`, `SUPPLIER_CODE_REQUIRED`, `PRODUCT_NAME_REQUIRED`, `QTY_NEGATIVE`, `VALUE_NEGATIVE`.

Run:

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~Domain
```

Expected: all domain tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Automation.Modules.Rebate/Domain tests/Automation.Modules.Rebate.Tests/Domain
git commit -m "feat: model and validate Rebate invoice lines"
```

### Task 3: Reconcile Web and Legacy POIs

**Files:**
- Create: `src/Automation.Modules.Rebate/Application/Reconciliation/ReconciliationResult.cs`
- Create: `src/Automation.Modules.Rebate/Application/Reconciliation/PoiReconciler.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Application/PoiReconcilerTests.cs`

**Interfaces:**
- Consumes: `PoiDocument`, `SourceSystem`
- Produces: `PoiReconciler.Reconcile(IReadOnlyList<PoiDocument> web, IReadOnlyList<PoiDocument> legacy)` and `ReconciliationResult.Documents/DuplicatePoiNumbers`

- [ ] **Step 1: Write failing priority tests**

```csharp
[TestMethod]
public void Duplicate_POI_uses_complete_Web_document()
{
    var web = RebateTestData.Poi("POI-1", SourceSystem.Web, RebateTestData.Line("WEB"));
    var legacy = RebateTestData.Poi("POI-1", SourceSystem.Legacy, RebateTestData.Line("LEGACY"));
    var result = new PoiReconciler().Reconcile([web], [legacy]);

    Assert.AreEqual(1, result.Documents.Count);
    var selected = result.Documents[0];
    Assert.AreEqual(SourceSystem.Web, selected.Source);
    Assert.AreEqual(1, selected.Lines.Count);
    Assert.AreEqual("WEB", selected.Lines[0].ProductName);
    CollectionAssert.AreEqual(new[] { "POI-1" }, result.DuplicatePoiNumbers.ToArray());
}

[TestMethod]
public void Legacy_only_POI_is_preserved()
{
    var result = new PoiReconciler().Reconcile([], [RebateTestData.Poi("POI-2", SourceSystem.Legacy, RebateTestData.Line("L"))]);
    Assert.AreEqual(1, result.Documents.Count);
    Assert.AreEqual("POI-2", result.Documents[0].DocumentNo);
}
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~PoiReconcilerTests
```

Expected: FAIL because reconciler types do not exist.

- [ ] **Step 3: Implement deterministic reconciliation**

Normalize only POI comparison keys with `Trim()` and `OrdinalIgnoreCase`; preserve original document number in output. Reject duplicate POI numbers within the same source with `InvalidOperationException` because silently selecting one would lose lines. Union source sets, choose Web when both exist, and sort documents by `DocumentNo`.

- [ ] **Step 4: Add repeated-product-line test and run GREEN**

Add a test where one selected POI contains two lines with identical Invoice No and product codes but different `SourceOrdinal`; assert both remain.

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~PoiReconcilerTests
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Automation.Modules.Rebate/Application/Reconciliation tests/Automation.Modules.Rebate.Tests/Application/PoiReconcilerTests.cs
git commit -m "feat: reconcile POIs with Web priority"
```

### Task 4: Orchestrate collectors with per-POI failures

**Files:**
- Create: `src/Automation.Modules.Rebate/Application/Sources/PoiReference.cs`
- Create: `src/Automation.Modules.Rebate/Application/Sources/PoiReadResult.cs`
- Create: `src/Automation.Modules.Rebate/Application/Sources/IPoiSource.cs`
- Create: `src/Automation.Modules.Rebate/Application/Outputs/IRebateWorkbookWriter.cs`
- Create: `src/Automation.Modules.Rebate/Application/Outputs/IRunReportWriter.cs`
- Create: `src/Automation.Modules.Rebate/Application/RunRebateRequest.cs`
- Create: `src/Automation.Modules.Rebate/Application/RunRebateWorkflow.cs`
- Create: `tests/Automation.Modules.Rebate.Tests/Fakes/FakePoiSource.cs`
- Create: `tests/Automation.Modules.Rebate.Tests/Fakes/CapturingWorkbookWriter.cs`
- Create: `tests/Automation.Modules.Rebate.Tests/Fakes/CapturingRunReportWriter.cs`
- Create: `tests/Automation.Modules.Rebate.Tests/Fakes/RebateWorkflowFactory.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Application/RunRebateWorkflowTests.cs`

**Interfaces:**
- Consumes: `RebatePeriod`, `PoiReconciler`, `RunContext`, `RunResult`
- Produces: `IPoiSource`, output writer ports, `RunRebateWorkflow.RunAsync`

- [ ] **Step 1: Write failing partial-failure workflow test**

```csharp
[TestMethod]
public async Task One_failed_POI_continues_and_writes_successful_rows()
{
    var web = new FakePoiSource(SourceSystem.Web,
        references: [new("WEB-OK"), new("WEB-BAD")],
        results: new()
        {
            ["WEB-OK"] = PoiReadResult.Success(RebateTestData.Poi("WEB-OK", SourceSystem.Web, RebateTestData.JulyLine())),
            ["WEB-BAD"] = PoiReadResult.Failed("detail", "ELEMENT_NOT_FOUND", "missing table", "shot.png")
        });
    var legacy = new FakePoiSource(SourceSystem.Legacy, [], new());
    var workbook = new CapturingWorkbookWriter();
    var report = new CapturingRunReportWriter();

    var result = await RebateWorkflowFactory.Create(web, legacy, workbook, report)
        .RunAsync(RebateWorkflowFactory.JulyRequest(), RebateWorkflowFactory.Context(), default);

    Assert.AreEqual(RunStatus.PartialFailed, result.Status);
    Assert.AreEqual(1, result.Failures.Count);
    Assert.AreEqual("WEB-BAD", result.Failures[0].ItemKey);
    Assert.AreEqual(1, workbook.WrittenLines.Count);
    Assert.AreEqual(1, report.Failures.Count);
    Assert.AreEqual(1, web.ReadCounts["WEB-BAD"]); // no retry
}
```

- [ ] **Step 2: Run RED**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~RunRebateWorkflowTests
```

Expected: FAIL because workflow ports/types do not exist.

- [ ] **Step 3: Implement source and output ports**

```csharp
public interface IPoiSource
{
    SourceSystem Source { get; }
    Task PreflightAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PoiReference>> ListAsync(DateWindow window, CancellationToken cancellationToken);
    Task<PoiReadResult> ReadAsync(PoiReference reference, CancellationToken cancellationToken);
}
```

`PoiReadResult` has exactly one of `Document` or `Failure`. `IRebateWorkbookWriter` accepts successful flattened lines and returns a `RunArtifact`; `IRunReportWriter` accepts run summary, failures, duplicates, and returns a `RunArtifact`.

- [ ] **Step 4: Implement deterministic test fakes**

`FakePoiSource` stores references/results passed to its constructor, increments `ReadCounts[reference.DocumentNo]` on each read, and returns results without delay. `CapturingWorkbookWriter` stores the received flattened lines and returns `new RunArtifact("rebate", "Rebate.xlsx", "Rebate.xlsx")`. `CapturingRunReportWriter` stores failures/duplicates and returns a `run-report` artifact. `RebateWorkflowFactory` exposes `Create(web, legacy, workbook, report)`, `JulyRequest()` and a temporary `RunContext`; no fake may be compiled into the production project.

- [ ] **Step 5: Implement workflow semantics**

Workflow order is Web preflight/list, Legacy preflight/list, read each reference exactly once, filter lines by Tax Invoice Date, reconcile by POI, validate lines, write Rebate workbook, then write report. A preflight/list exception bubbles to `IRunCoordinator` as run-level `FAILED`; a `PoiReadResult.Failed` becomes POI-level failure and processing continues.

Status rules:

```csharp
var status = failures.Count == 0 ? RunStatus.Success : RunStatus.PartialFailed;
```

Writers must still run when `failures.Count > 0`; they must not run after preflight/list failure.

- [ ] **Step 6: Add Web-priority workflow test and run GREEN**

Test Web and Legacy returning the same POI with different values; assert writer receives Web lines only. Run:

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~RunRebateWorkflowTests
```

Expected: all workflow tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Automation.Modules.Rebate/Application tests/Automation.Modules.Rebate.Tests/Application
git commit -m "feat: orchestrate Rebate batch with partial failures"
```

### Task 5: Write and verify Rebate Excel

**Files:**
- Copy: `Excel_Template.xlsx` to `src/Automation.Modules.Rebate/Assets/Excel_Template.xlsx`
- Modify: `src/Automation.Modules.Rebate/Automation.Modules.Rebate.csproj`
- Create: `src/Automation.Modules.Rebate/Adapters/Excel/ClosedXmlRebateWorkbookWriter.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Adapters/ClosedXmlRebateWorkbookWriterTests.cs`

**Interfaces:**
- Consumes: `IRebateWorkbookWriter`, flattened selected lines, approved template
- Produces: `Rebate_YYYY-MM_<RunId>.xlsx` artifact

- [ ] **Step 1: Include the approved template as content**

Copy the user-provided workbook without editing it. Add:

```xml
<ItemGroup>
  <Content Include="Assets\Excel_Template.xlsx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

- [ ] **Step 2: Write a failing workbook read-back test**

Create two lines with invoice/product identifiers beginning with zero. Write to a temp directory, reopen with ClosedXML, then assert:

```csharp
Assert.AreEqual("001234", sheet.Cell(2, 3).GetString());
Assert.AreEqual(10m, sheet.Cell(2, 6).GetValue<decimal>());
Assert.AreEqual(1250.50m, sheet.Cell(2, 7).GetValue<decimal>());
Assert.AreEqual(3, sheet.Table(0).RangeAddress.LastAddress.RowNumber);
```

- [ ] **Step 3: Run RED**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~ClosedXmlRebateWorkbookWriterTests
```

Expected: FAIL because writer is missing.

- [ ] **Step 4: Implement targeted template writing**

Copy template to `<ArtifactDirectory>/Rebate_YYYY-MM_<RunId>.tmp.xlsx`, open copied file, verify sheet `Template` and exact seven headers, insert rows starting at row 2, write typed values, resize the existing table to `A1:G{lastRow}`, save, then rename to `.xlsx`. Delete the temporary file on exception. Set identifier cells to text and date cells to `yyyy-mm-dd`; do not restyle the workbook.

- [ ] **Step 5: Run GREEN**

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~ClosedXmlRebateWorkbookWriterTests
```

Expected: workbook test passes and temporary output is deleted by test cleanup.

- [ ] **Step 6: Commit**

```powershell
git add src/Automation.Modules.Rebate/Assets src/Automation.Modules.Rebate/Adapters/Excel src/Automation.Modules.Rebate/Automation.Modules.Rebate.csproj tests/Automation.Modules.Rebate.Tests/Adapters
git commit -m "feat: generate Rebate workbook from approved template"
```

### Task 6: Generate the Run Report and register the module UI

**Files:**
- Create: `src/Automation.Modules.Rebate/Adapters/Excel/ClosedXmlRunReportWriter.cs`
- Create: `src/Automation.Modules.Rebate/Adapters/Fixtures/FixturePoiSource.cs`
- Create: `src/Automation.Modules.Rebate/Assets/RebateDevelopmentFixture.json`
- Create: `src/Automation.Modules.Rebate/Presentation/RebateStartViewModel.cs`
- Create: `src/Automation.Modules.Rebate/Presentation/RebateHistoryViewModel.cs`
- Create: `src/Automation.Modules.Rebate/Presentation/RebateStartView.xaml`
- Create: `src/Automation.Modules.Rebate/Presentation/RebateHistoryView.xaml`
- Create: `src/Automation.Modules.Rebate/DependencyInjection.cs`
- Modify: `src/Automation.Desktop/App.xaml.cs`
- Test: `tests/Automation.Modules.Rebate.Tests/Adapters/ClosedXmlRunReportWriterTests.cs`

**Interfaces:**
- Consumes: `IRunReportWriter`, `IRunCoordinator`, `IRunHistoryStore`, `RunRebateWorkflow`
- Produces: Rebate catalog card, Start page, History page, Run Report artifact และ development-only executable workflow

- [ ] **Step 1: Write a failing report test**

Create a `PARTIAL_FAILED` report with one Web failure and one duplicate POI. Reopen the workbook and assert Summary contains counts/status and Failures contains exact columns `Source`, `POI Document No`, `Failed Step`, `Error Code`, `Error`, `Screenshot Path`.

- [ ] **Step 2: Implement the report writer and run GREEN**

Create workbook with `Summary` and `Failures` sheets, one table per sheet, typed timestamps/counts, and file name `Rebate_YYYY-MM_<RunId>_RunReport.xlsx`. Run:

```powershell
dotnet test tests/Automation.Modules.Rebate.Tests --filter FullyQualifiedName~ClosedXmlRunReportWriterTests
```

Expected: report test passes.

- [ ] **Step 3: Add a deterministic development fixture source**

Create `RebateDevelopmentFixture.json` with anonymized POIs covering Web-only, Legacy-only, duplicate POI, repeated product line, out-of-month line and one failed POI. `FixturePoiSource` implements `IPoiSource`, reads this file, and is registered only when `IHostEnvironment.IsDevelopment()` is true. Production environment must refuse to resolve fixture sources. Add a test asserting the fixture produces `PARTIAL_FAILED`, selects Web for the duplicate, and writes the expected successful line count.

- [ ] **Step 4: Implement lean Rebate UI**

Start page contains month/year inputs, Start button, progress, counts, failure grid, and artifact buttons. History page calls `ListByModuleAsync("rebate", 100)` and shows only Rebate runs. Do not add Rebate Settings. `DependencyInjection.AddRebateModule()` registers descriptor, workflow, writers, Views and ViewModels; add one call in the Desktop composition root. In Development it registers the two fixture sources; in Production source registration is supplied only by live collector adapters.

- [ ] **Step 5: Run all tests and manual UI smoke**

```powershell
dotnet test GlobalsoftAutomation.sln
dotnet run --project src/Automation.Desktop
```

Expected: tests pass; Automations catalog shows Rebate; opening it shows only `เริ่มงาน Rebate` and `ประวัติ Rebate`. Run with `DOTNET_ENVIRONMENT=Development`; the anonymized fixture completes as `PARTIAL_FAILED` and creates both output workbooks. A Production run without live adapters must fail during host validation before the Start page is usable; it must never silently use fixtures.

- [ ] **Step 6: Commit**

```powershell
git add src/Automation.Modules.Rebate src/Automation.Desktop tests/Automation.Modules.Rebate.Tests
git commit -m "feat: add Rebate report and module UI"
```

## Completion Check

Run fresh:

```powershell
dotnet restore GlobalsoftAutomation.sln
dotnet build GlobalsoftAutomation.sln --no-restore
dotnet test GlobalsoftAutomation.sln --no-build
```

Expected: all commands exit `0`. Inspect one generated Rebate workbook and one Run Report from tests. Verify the original root `Excel_Template.xlsx` remains unchanged. Live Web and Legacy automation are intentionally excluded until the discovery plan records stable endpoints and UI identifiers.
