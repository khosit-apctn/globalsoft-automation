# Automation Platform Delivery Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ส่งมอบ Generic Automation Platform และ Rebate Automation โดยแยกความเสี่ยงของ platform, business rules และ reverse-engineered integrations ออกจากกัน

**Architecture:** ทำงานเป็นสามแผนที่มี review gate ชัดเจน Foundation สร้าง runtime กลาง, Offline Pipeline พิสูจน์ Rebate rules/Excel ด้วย fixture, Discovery เก็บ endpoint/locator จริงก่อนเขียน live adapters จากนั้นจึงสร้าง collector implementation plan จากหลักฐานที่อนุมัติแล้ว

**Tech Stack:** .NET 10 LTS, WPF, Generic Host, CommunityToolkit.Mvvm, SQLite, ClosedXML, Playwright for .NET, FlaUI UIA3, MSTest

## Global Constraints

- ใช้ approved design: `docs/superpowers/specs/2026-08-16-generic-automation-platform-design.md`
- ห้ามเริ่ม production live collectors จาก selector/endpoint ที่เดา
- แต่ละแผนต้อง build/test ผ่านและได้รับ review ก่อนเริ่มแผนถัดไป
- ห้าม push, deploy หรือติดตั้ง dependency ระดับเครื่องโดยไม่ได้รับอนุญาต

---

### Task 1: Platform Foundation

**Files:**
- Execute: `docs/superpowers/plans/2026-08-16-platform-foundation.md`

**Interfaces:**
- Consumes: approved design spec
- Produces: contracts, module catalog, SQLite history, run coordinator, WPF shell

- [ ] Execute every checkbox in `2026-08-16-platform-foundation.md`.
- [ ] Verify `dotnet build` and `dotnet test` from a clean restore.
- [ ] Review module boundaries before continuing.

### Task 2: Rebate Offline Pipeline

**Files:**
- Execute: `docs/superpowers/plans/2026-08-16-rebate-offline-pipeline.md`

**Interfaces:**
- Consumes: Foundation contracts and runtime
- Produces: tested canonical model, reconciliation, workflow, Excel writers, Rebate UI and development fixtures

- [ ] Execute every checkbox in `2026-08-16-rebate-offline-pipeline.md`.
- [ ] Verify the anonymized fixture creates Rebate Excel and Run Report.
- [ ] Review generated workbook against the approved Template.

### Task 3: Live Collector Discovery

**Files:**
- Execute: `docs/superpowers/plans/2026-08-16-live-collector-discovery.md`

**Interfaces:**
- Consumes: authorized Web and Legacy user sessions
- Produces: reviewed Web adapter spec, Legacy adapter spec and cross-source field map

- [ ] Execute every checkbox in `2026-08-16-live-collector-discovery.md`.
- [ ] Confirm raw evidence and secrets are ignored by Git.
- [ ] Obtain user approval for all three discovery documents.

### Task 4: Write the production collector plan

**Files:**
- Create after discovery approval: `docs/superpowers/plans/2026-08-16-rebate-live-collectors.md`

**Interfaces:**
- Consumes: exact endpoint paths, JSON paths, Web selectors, Legacy AutomationIds/control patterns and field mappings from Task 3
- Produces: executable TDD plan for Web login/API/Playwright adapter, Windows credential vault, Legacy login/UIA adapter, production DI registration and acceptance test

- [ ] Invoke `writing-plans` after the discovery documents are approved.
- [ ] Copy exact verified endpoint/locator identifiers into the live-collector plan; do not use placeholders.
- [ ] Include production credential handling through Windows Credential Manager.
- [ ] Include per-POI `PARTIAL_FAILED`, no-retry assertions and one authorized monthly acceptance run.
- [ ] Review and approve the live-collector plan before implementation.

## Delivery Order

Task 1 must finish first. Tasks 2 and 3 may then proceed in parallel if separate workers avoid shared-file edits; otherwise execute Task 2 then Task 3. Task 4 is blocked until Task 3 evidence is reviewed. The first user-visible production release occurs only after Task 4 implementation passes its acceptance run.
