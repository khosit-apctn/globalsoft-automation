# Live Web and Legacy Collector Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** สร้างเครื่องมือ read-only สำหรับเก็บหลักฐาน endpoint/schema ของ Globalsoft Web และ UI Automation tree ของ Windows Legacy แล้วผลิต adapter specification ที่มี identifiers จริงเพียงพอสำหรับเขียน live-collector implementation plan โดยไม่เดา selector หรือ locator

**Architecture:** Discovery tools เป็น console applications แยกจาก production app และเขียน raw artifacts ลง ignored local directory เท่านั้น Web tool ใช้ Playwright บันทึก URL path/status/JSON shape แบบตัดค่าอ่อนไหว ส่วน Legacy tool ใช้ FlaUI UIA3 สำรวจ automation tree หลังผู้ใช้เปิดหน้าที่ต้องการ

**Tech Stack:** .NET 10 LTS, Microsoft.Playwright 1.61.0, FlaUI.UIA3 5.0.0, MSTest 4.3.3

## Global Constraints

- ทำหลัง Platform Foundation; สามารถทำคู่ขนานกับ Rebate Offline Pipeline ได้เมื่อไม่แก้ไฟล์เดียวกัน
- Discovery เป็น read-only ต่อข้อมูลธุรกิจ: ห้าม create/update/delete เอกสารใน Web หรือ Legacy
- ห้าม commit credential, cookies, tokens, HAR, trace, screenshots หรือ raw UI tree ที่มีข้อมูลลูกค้า
- Network inventory เก็บ method, sanitized path, status, content type และ JSON property names เท่านั้น
- UI inventory เก็บ AutomationId, ControlType, supported patterns และ stable label ที่ผู้ใช้ตรวจว่าไม่เป็นข้อมูลธุรกิจ
- ไม่มี retry และไม่มี runtime fallback
- ยังไม่เขียน production collector จนกว่า adapter specification จะมี endpoint/selector/locator จริงและผ่าน review

---

## File Map

- `tools/Automation.Discovery.Web/` — Playwright network/schema observer
- `tools/Automation.Discovery.Legacy/` — FlaUI automation-tree observer
- `tests/Automation.Discovery.Tests/` — sanitizer and deterministic-output tests
- `artifacts/discovery/` — ignored raw local evidence
- `docs/discovery/globalsoft-web-adapter-spec.md` — reviewed Web endpoint/UI map
- `docs/discovery/globalhouse-legacy-adapter-spec.md` — reviewed Legacy window/control map
- `docs/discovery/rebate-source-field-map.md` — seven-field mapping and POI key evidence

### Task 1: Scaffold isolated discovery tools and secure artifact paths

**Files:**
- Create: `tools/Automation.Discovery.Web/Automation.Discovery.Web.csproj`
- Create: `tools/Automation.Discovery.Legacy/Automation.Discovery.Legacy.csproj`
- Create: `tests/Automation.Discovery.Tests/Automation.Discovery.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `GlobalsoftAutomation.sln`
- Modify: `.gitignore`
- Create: `tools/README.md`

**Interfaces:**
- Consumes: none
- Produces: buildable discovery executables and an ignored `artifacts/discovery/` boundary

- [ ] **Step 1: Pin discovery dependencies**

Add:

```xml
<PackageVersion Include="Microsoft.Playwright" Version="1.61.0" />
<PackageVersion Include="FlaUI.UIA3" Version="5.0.0" />
```

- [ ] **Step 2: Scaffold projects**

```powershell
dotnet new console -n Automation.Discovery.Web -o tools/Automation.Discovery.Web -f net10.0
dotnet new console -n Automation.Discovery.Legacy -o tools/Automation.Discovery.Legacy -f net10.0
dotnet new mstest -n Automation.Discovery.Tests -o tests/Automation.Discovery.Tests -f net10.0
dotnet sln GlobalsoftAutomation.sln add tools/Automation.Discovery.Web tools/Automation.Discovery.Legacy tests/Automation.Discovery.Tests
dotnet add tools/Automation.Discovery.Web package Microsoft.Playwright
dotnet add tools/Automation.Discovery.Legacy package FlaUI.UIA3
dotnet add tests/Automation.Discovery.Tests reference tools/Automation.Discovery.Web tools/Automation.Discovery.Legacy
```

Change `Automation.Discovery.Legacy` and `Automation.Discovery.Tests` target frameworks to `net10.0-windows` before restore.

- [ ] **Step 3: Protect evidence from source control**

Append to `.gitignore`:

```gitignore
artifacts/discovery/
playwright/.auth/
```

Document in `tools/README.md` that discovery tools require an authorized user session, never mutate documents, and raw artifacts must remain local.

- [ ] **Step 4: Build and install Playwright browser runtime**

```powershell
dotnet build tools/Automation.Discovery.Web
pwsh tools/Automation.Discovery.Web/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet build tools/Automation.Discovery.Legacy
dotnet test tests/Automation.Discovery.Tests
```

Expected: all commands exit `0`; browser download requires explicit network approval during execution.

- [ ] **Step 5: Commit**

```powershell
git add .gitignore Directory.Packages.props GlobalsoftAutomation.sln tools tests/Automation.Discovery.Tests
git commit -m "build: add isolated collector discovery tools"
```

### Task 2: Capture a sanitized Globalsoft network inventory

**Files:**
- Create: `tools/Automation.Discovery.Web/NetworkObservation.cs`
- Create: `tools/Automation.Discovery.Web/UrlSanitizer.cs`
- Create: `tools/Automation.Discovery.Web/JsonShapeExtractor.cs`
- Modify: `tools/Automation.Discovery.Web/Program.cs`
- Test: `tests/Automation.Discovery.Tests/Web/UrlSanitizerTests.cs`
- Test: `tests/Automation.Discovery.Tests/Web/JsonShapeExtractorTests.cs`
- Create after authorized run: `docs/discovery/globalsoft-web-adapter-spec.md`

**Interfaces:**
- Consumes: authorized interactive browser session
- Produces: sanitized `web-network-inventory.json` and reviewed endpoint/UI specification

- [ ] **Step 1: Write sanitizer tests before browser code**

```csharp
[TestMethod]
public void Sanitizer_removes_query_values_and_fragments()
{
    var sanitized = UrlSanitizer.Sanitize("https://host/api/poi?page=2&token=secret#detail");
    Assert.AreEqual("https://host/api/poi?page={value}&token={value}", sanitized);
}

[TestMethod]
public void Json_shape_keeps_property_names_not_values()
{
    var shape = JsonShapeExtractor.Extract("{\"invoiceNo\":\"INV-SECRET\",\"lines\":[{\"qty\":10}]}");
    StringAssert.Contains(shape, "invoiceNo:string");
    StringAssert.DoesNotContain(shape, "INV-SECRET");
}
```

- [ ] **Step 2: Run RED, implement sanitizers, run GREEN**

```powershell
dotnet test tests/Automation.Discovery.Tests --filter "FullyQualifiedName~UrlSanitizerTests|FullyQualifiedName~JsonShapeExtractorTests"
```

Expected first run FAIL, second run PASS. Limit response-body inspection to JSON smaller than 1 MB and output only recursive property/type shape with array samples capped at one item.

- [ ] **Step 3: Implement the interactive observer**

Use headed Chromium and this workflow:

```csharp
await using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();

page.Response += async (_, response) =>
{
    if (!response.Url.StartsWith("https://globalsoft-app.platform-center.com/", StringComparison.OrdinalIgnoreCase)) return;
    await observer.RecordAsync(response);
};

await page.GotoAsync("https://globalsoft-app.platform-center.com/dashboard/DcPurchaseOrder/DcPoInvoice/");
Console.WriteLine("Sign in, filter one known month, open one POI detail, then return here and press Enter.");
Console.ReadLine();
await observer.SaveAsync(outputPath);
```

Do not persist storage state. Do not enable HAR or tracing. Deduplicate observations by method + sanitized URL + status + JSON shape.

- [ ] **Step 4: Run the authorized discovery session**

```powershell
dotnet run --project tools/Automation.Discovery.Web -- --output artifacts/discovery/web-network-inventory.json
```

Manual actions: login, filter a small known date range, open one POI, inspect invoice details, return to list. Do not submit or edit any record.

- [ ] **Step 5: Write the reviewed Web adapter specification**

`docs/discovery/globalsoft-web-adapter-spec.md` must contain concrete rows for:

- login URL and stable labels/test IDs;
- POI list endpoint or UI locator;
- date filter request fields and date semantics;
- pagination fields;
- POI Document No field;
- invoice-detail endpoint or UI locator;
- all seven output fields and JSON paths/locators;
- evidence that Tax Invoice Date is the field used for monthly inclusion;
- authentication/cookie behavior without storing token values;
- decision per operation: API or Playwright;
- observed failure response/status behavior.

Every row cites a sanitized observation ID from the local inventory. The document contains no credential, token, invoice value, SKU, customer name, or screenshot.

- [ ] **Step 6: Commit reviewed source code and sanitized specification only**

```powershell
git status --short
git add tools/Automation.Discovery.Web tests/Automation.Discovery.Tests/Web docs/discovery/globalsoft-web-adapter-spec.md
git commit -m "docs: map Globalsoft Web collector surface"
```

Before commit, verify `artifacts/discovery/` is absent from `git status`.

### Task 3: Capture and review the Windows Legacy UI Automation map

**Files:**
- Create: `tools/Automation.Discovery.Legacy/AutomationTreeNode.cs`
- Create: `tools/Automation.Discovery.Legacy/AutomationTreeScanner.cs`
- Create: `tools/Automation.Discovery.Legacy/SensitiveNameRedactor.cs`
- Modify: `tools/Automation.Discovery.Legacy/Program.cs`
- Test: `tests/Automation.Discovery.Tests/Legacy/SensitiveNameRedactorTests.cs`
- Test: `tests/Automation.Discovery.Tests/Legacy/AutomationTreeScannerTests.cs`
- Create after authorized run: `docs/discovery/globalhouse-legacy-adapter-spec.md`

**Interfaces:**
- Consumes: manually opened and authenticated Legacy window during discovery
- Produces: sanitized automation-tree evidence and reviewed window/control map

- [ ] **Step 1: Write redaction and depth-limit tests**

```csharp
[TestMethod]
public void Redactor_keeps_known_labels_and_removes_business_values()
{
    var redactor = new SensitiveNameRedactor(["Document No.", "Tax Invoice Date", "Search"]);
    Assert.AreEqual("Document No.", redactor.Redact("Document No."));
    Assert.AreEqual("{redacted}", redactor.Redact("INV-2026-000123"));
}
```

Add a scanner test using an in-memory node adapter and assert `--max-depth 8` never emits deeper nodes.

- [ ] **Step 2: Run RED, implement scanner/redactor, run GREEN**

```powershell
dotnet test tests/Automation.Discovery.Tests --filter "FullyQualifiedName~SensitiveNameRedactorTests|FullyQualifiedName~AutomationTreeScannerTests"
```

Expected: tests pass after implementation. Output fields are `Depth`, `AutomationId`, `ControlType`, `ClassName`, `RedactedName`, `SupportedPatterns`, `IsEnabled`, `IsOffscreen`.

- [ ] **Step 3: Implement attach-only Legacy discovery**

Accept `--process-name`, `--output`, and `--max-depth`. Attach with FlaUI without starting, closing, clicking, typing, or changing the target application. Enumerate the main window and descendants through UIA3. Reject missing or multiple matching processes with an explicit error.

- [ ] **Step 4: Run discovery on each required screen**

The authorized user manually logs in and opens these states before each capture:

1. main menu;
2. POI search/list;
3. one POI detail;
4. invoice/product grid;
5. export/copy menu if present.

Run one output file per state:

```powershell
$legacyProcessName = Read-Host 'Enter the exact process name shown in Task Manager (without .exe)'
dotnet run --project tools/Automation.Discovery.Legacy -- --process-name $legacyProcessName --max-depth 8 --output artifacts/discovery/legacy-poi-list.json
```

Enter the exact process name discovered from Task Manager; do not guess or commit it until reviewed.

- [ ] **Step 5: Write the reviewed Legacy adapter specification**

`docs/discovery/globalhouse-legacy-adapter-spec.md` must record:

- executable/process identity and main-window matcher;
- login field/button AutomationIds and supported patterns;
- POI search/list controls;
- exact POI Document No source;
- detail navigation controls;
- seven output field controls/grid columns;
- pagination/scroll behavior;
- export or copy capability and whether it is complete;
- chosen extraction route: Export/Copy or UI Automation;
- stable wait condition after every action;
- controls that lack stable IDs and the containment path used instead.

No coordinate-only locator is accepted as the primary locator.

- [ ] **Step 6: Commit reviewed source and specification only**

```powershell
git add tools/Automation.Discovery.Legacy tests/Automation.Discovery.Tests/Legacy docs/discovery/globalhouse-legacy-adapter-spec.md
git commit -m "docs: map Globalhouse Legacy automation surface"
```

### Task 4: Produce the cross-source field map and implementation gate

**Files:**
- Create: `docs/discovery/rebate-source-field-map.md`
- Modify: `docs/superpowers/specs/2026-08-16-generic-automation-platform-design.md` only if verified behavior contradicts the approved assumptions

**Interfaces:**
- Consumes: both reviewed adapter specifications
- Produces: exact mapping needed for a live-collector implementation plan

- [ ] **Step 1: Build the seven-field mapping table**

Create one row per canonical field with columns:

```text
Canonical Field | Web Route | Web Path/Locator | Legacy Route | Legacy Locator/Column | Type Conversion | Required | Evidence
```

Add separate rows for internal `POI Document No`, source row ID and source ordinal.

- [ ] **Step 2: Verify reconciliation assumptions with three cases**

Using authorized sample records, document evidence for:

1. POI present only on Web;
2. POI present only on Legacy;
3. same POI present on both systems.

Record only anonymized IDs such as `WEB_ONLY_SAMPLE`, not actual document numbers. Confirm that Web contains all seven fields when it wins.

- [ ] **Step 3: Apply the implementation readiness gate**

The live collector is ready to plan only when all checks are true:

- POI enumeration path is known for both systems;
- login selectors/controls are stable;
- all seven fields have exact paths/locators and type conversions;
- POI Document No is available in both systems;
- pagination/scroll termination is known;
- no primary action depends only on screen coordinates;
- API/UI choice is explicit per Web operation;
- Export/Copy/UIA choice is explicit for Legacy;
- no sensitive evidence is tracked by Git.

If any check is false, stop and report that specific discovery blocker; do not invent production selectors.

- [ ] **Step 4: Commit and request review**

```powershell
git add docs/discovery/rebate-source-field-map.md docs/superpowers/specs/2026-08-16-generic-automation-platform-design.md
git commit -m "docs: define verified Rebate source mapping"
```

Request user review of the three discovery documents. After approval, use `writing-plans` again to create the production Web/Legacy collector plan with the verified endpoint paths and UI Automation locators.

## Completion Check

Run:

```powershell
dotnet build GlobalsoftAutomation.sln
dotnet test tests/Automation.Discovery.Tests
git status --short
git check-ignore artifacts/discovery/web-network-inventory.json
```

Expected: build/test exit `0`; raw evidence is ignored; three reviewed discovery documents exist and contain no secrets or real invoice/product values. This plan deliberately ends before production collectors because exact selectors and endpoint schemas cannot be responsibly specified before authorized discovery.
