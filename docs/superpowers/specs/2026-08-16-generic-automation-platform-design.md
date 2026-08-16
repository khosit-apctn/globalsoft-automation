# Generic Automation Platform และ Rebate Automation — Design Specification

วันที่: 2026-08-16
สถานะ: อนุมัติแนวทางออกแบบแล้ว รอ review เอกสารก่อนจัดทำ implementation plan

## 1. เป้าหมาย

สร้างโปรแกรม Windows แบบ generic สำหรับรวม Automation หลายประเภทไว้ใน application เดียว โดย Automation แต่ละงานมี input, workflow, dashboard, history และ output ของตนเอง งานแรกคือ **Attach Tax Invoice (Rebate)** ซึ่งรวบรวม POI จาก Globalsoft Web และ Globalhouse Windows Legacy แล้วสร้าง Excel รายเดือนจากรายการที่สำเร็จ

ผลลัพธ์หลักของ Rebate คือ:

- Rebate Excel ที่มีหนึ่งแถวต่อหนึ่ง product line
- Run Report ที่อธิบายรายการสำเร็จ รายการซ้ำ และ POI ที่ล้มเหลว
- Run History ภายในโปรแกรมสำหรับตรวจย้อนหลัง

## 2. ขอบเขต MVP

### อยู่ในขอบเขต

- WPF desktop application สำหรับ Windows
- หน้า Automations เป็นหน้าแรกของโปรแกรม
- Automation module สำหรับ Rebate
- Login Globalsoft Web และ Windows Legacy อัตโนมัติ
- เก็บ credential ของแต่ละระบบแยกกันอย่างปลอดภัย
- ดึงข้อมูลแบบ batch ตามเดือนที่ผู้ใช้เลือก
- รวมข้อมูลจาก Web และ Legacy ด้วยกฎ Web priority
- สร้าง Rebate Excel จาก POI ที่สำเร็จ
- สร้าง Run Report แม้ผลรวมเป็น `PARTIAL_FAILED`
- ประวัติการรันอยู่ภายในแต่ละ Automation

### ไม่อยู่ในขอบเขต MVP

- Scheduling หรือ unattended execution
- Resume งานเดิมหลังโปรแกรมปิดหรือเครื่องดับ
- Retry network, API หรือ UI action อัตโนมัติ
- Dynamic DLL plugin installation
- Dashboard กลางที่บังคับให้ทุก Automation ใช้รูปแบบเดียวกัน
- Automated UI test suite ขนาดใหญ่

## 3. Technology Baseline

- .NET 10 LTS
- WPF
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting สำหรับ dependency injection, configuration, logging และ lifecycle
- Playwright for .NET สำหรับ Globalsoft Web
- Windows UI Automation เป็นกลไกหลักสำหรับ Windows Legacy
- SQLite สำหรับ Run History และ metadata
- Windows Credential Manager สำหรับ username/password
- ClosedXML สำหรับอ่านและเขียน `.xlsx` โดยต้องทำ smoke test กับ Template จริงก่อนเริ่ม Excel writer

เหตุผลที่เลือก .NET 10 แทน .NET 8 คือ .NET 8 สิ้นสุดการสนับสนุนวันที่ 2026-11-10 ขณะที่ .NET 10 LTS รองรับถึง 2028-11-14

## 4. Architectural Style

ใช้ **Modular Monolith + Vertical Slice + Ports and Adapters + MVVM**

### 4.1 Modular Monolith

โปรแกรม deploy และรันเป็น process เดียว แต่ Automation แต่ละงานเป็น module ที่มีขอบเขตชัดเจน Module หนึ่งห้ามอ้างอิง implementation ภายในของอีก module โดยตรง

MVP ใช้ compile-time module registration เช่น `AddRebateModule()` ไม่ใช้ dynamic plugin loader การใช้ `AssemblyLoadContext` จะพิจารณาเมื่อมี requirement ให้อัปเดตหรือติดตั้ง module แยกจากตัวโปรแกรมเท่านั้น

### 4.2 Vertical Slice

จัดโค้ดตาม Automation และ use case ไม่สร้างโฟลเดอร์กลางขนาดใหญ่ เช่น `Services`, `ViewModels` หรือ `Repositories` ที่รวมทุกงานไว้ด้วยกัน

### 4.3 Ports and Adapters

Application layer ของแต่ละ module กำหนด interfaces ที่ต้องใช้ เช่น Web source, Legacy source และ workbook writer ส่วน Playwright, UI Automation และ Excel library เป็น adapters ที่ implement interfaces เหล่านั้น

Business rules ต้องไม่อ้างอิง Playwright, WPF, SQLite, Windows UI Automation หรือ Excel library

### 4.4 MVVM

- View แสดงผลและรับ interaction เท่านั้น
- ViewModel เก็บ UI state และ commands
- Application workflow ทำ orchestration ของ Automation
- Domain เก็บ model และ business rules
- ห้ามใส่ selector, locator, reconciliation หรือ Excel logic ใน View/ViewModel

## 5. Solution Structure

```text
src/
├─ Automation.Desktop/
│  └─ WPF shell, navigation และ composition root
├─ Automation.Platform.Contracts/
│  └─ contract ที่เสถียรสำหรับ module และ run lifecycle
├─ Automation.Platform/
│  └─ module catalog, run coordinator, history และ shared policies
├─ Automation.Infrastructure/
│  └─ SQLite, filesystem, credential vault และ shared logging
└─ Modules/
   └─ Automation.Modules.Rebate/
      ├─ Presentation/
      ├─ Application/
      ├─ Domain/
      ├─ Adapters/
      │  ├─ Web/
      │  ├─ Legacy/
      │  └─ Excel/
      └─ RebateModule.cs

tests/
├─ Automation.Platform.Tests/
└─ Automation.Modules.Rebate.Tests/
```

ช่วง MVP ให้ Rebate เป็นหนึ่ง production project และหนึ่ง test project โดยแบ่ง layer ด้วย folders ก่อน หาก module โตจน dependency boundary ควบคุมยาก จึงค่อยแยก Domain/Application/Adapters เป็นหลาย projects

## 6. Platform Core

Platform Core รับผิดชอบเฉพาะความสามารถร่วม:

- Module catalog และ navigation
- Run lifecycle และสถานะมาตรฐาน
- Credential access
- Run History persistence
- Structured logging
- Error artifact และ screenshot paths
- Output artifact registration และคำสั่งเปิดไฟล์
- ป้องกันไม่ให้มี run ซ้อนกันใน module เดียว

Platform Core ต้องไม่รู้จักคำว่า POI, Invoice, Product Code, selector ของเว็บ, locator ของ Legacy หรือคอลัมน์ Rebate

Contract ขั้นต่ำประกอบด้วย:

- `IAutomationModule`: metadata และการลงทะเบียน module
- `AutomationDescriptor`: id, name, icon และ navigation entries
- `IRunCoordinator`: เริ่มและจบ run พร้อมบันทึกสถานะ
- `RunContext`: Run ID, เวลาเริ่ม, cancellation และ artifact directory
- `RunProgress`: stage, current item, completed count และ total count
- `RunResult`: status, summary, failures และ output artifacts
- `RunArtifact`: ชนิดไฟล์ ชื่อไฟล์ และ path

## 7. การเพิ่ม Automation ใหม่

การเพิ่มงานใหม่ต้องทำได้โดย:

1. สร้าง project `Automation.Modules.<Name>`
2. สร้าง View และ ViewModel ของงานนั้น
3. สร้าง request, workflow และ domain rules ของงานนั้น
4. สร้าง adapters ที่งานนั้นต้องใช้
5. Implement `IAutomationModule`
6. ลงทะเบียน module หนึ่งจุดใน composition root
7. เพิ่ม tests เฉพาะ core rules และ critical outputs ของ module

การเพิ่ม module ใหม่ต้องไม่แก้ business logic ของ Rebate และไม่เพิ่ม `if/switch` ตามชื่อ Automation ใน Platform Core

## 8. Navigation และ UI

### ระดับ Platform

- `Automations` เป็นหน้าแรกและ module catalog
- `บัญชีผู้ใช้` สำหรับจัดการ credential ที่ module ขอใช้
- `ตั้งค่าระบบ` สำหรับค่า shared เช่น output root และ log retention

ไม่มีเมนู `หน้าหลัก` แยกจาก `Automations` และไม่มี Run History รวมใน MVP

### ระดับ Rebate

- `เริ่มงาน Rebate`
- `ประวัติ Rebate`

ไม่มี `ตั้งค่า Rebate` ใน MVP

หน้าเริ่มงานให้ผู้ใช้เลือกเดือนและปี จากนั้นแสดง progress, จำนวน POI, จำนวนสำเร็จ, จำนวน POI ซ้ำ และจำนวน `PARTIAL_FAILED` เมื่อจบงานให้เปิด Rebate Excel, Run Report หรือ Error Folder ได้

## 9. Rebate Canonical Model

ข้อมูลที่ส่งออกมี 7 fields:

1. Tax Invoice No
2. Tax Invoice Date
3. Product Code (Global House)
4. Product Code (Supplier)
5. Product Name
6. Qty
7. Value (Excluding VAT)

ข้อมูลภายในที่ไม่เขียนลง Rebate Template:

- POI Document No
- Source System (`Web` หรือ `Legacy`)
- Source document/row identifier เมื่อมี
- Source line ordinal
- Run ID
- Processing status
- Failure information

Product Code และ Invoice No เป็น text เพื่อรักษา leading zeros ส่วน Tax Invoice Date, Qty และ Value เก็บเป็น typed values ไม่ใช่ formatted strings

## 10. Rebate Data Sources

### 10.1 Globalsoft Web

ใช้ API เฉพาะ endpoint ที่ discovery ยืนยันว่าดึงข้อมูลได้ครบและเสถียร ใช้ Playwright สำหรับขั้นตอนที่ API ไม่ครอบคลุม การเลือก API หรือ Playwright เป็นเส้นทางที่กำหนดไว้ล่วงหน้าตาม capability ไม่ใช่การสลับไปอีกวิธีเมื่อเกิด runtime error

หาก API, network หรือ web element ล้มเหลว จะไม่ retry และไม่ silently fallback

### 10.2 Windows Legacy

Automation เปิดโปรแกรมและ login ด้วย credential ของ Legacy โดยอัตโนมัติ

ลำดับการเลือกวิธีดึงข้อมูลคือ:

1. Export หรือ Copy Grid ถ้า Legacy รองรับและข้อมูลครบ
2. Windows UI Automation ผ่าน automation element properties และ control patterns

ไม่ใช้การคลิกตามพิกัดเป็นวิธีหลัก หาก controls ที่จำเป็นไม่ถูก expose ผ่าน UI Automation และไม่มี export/copy ที่เชื่อถือได้ discovery ต้องรายงานข้อจำกัดก่อน implementation ของ collector

## 11. Rebate Batch Workflow

1. ผู้ใช้เลือกเดือนและปี
2. Platform สร้าง Run ID และ artifact directory ใหม่
3. Preflight ตรวจ Template, output path, credential และ application availability
4. Login Web และ Legacy
5. ดึง POI superset จากแต่ละระบบ
6. เปิดรายละเอียดและสร้าง canonical invoice lines
7. Normalize field names, dates, identifiers และ numeric values
8. กรองด้วย Tax Invoice Date ให้เหลือวันแรกถึงวันสุดท้ายของเดือนที่เลือก
9. Reconcile POI จากสองระบบ
10. Validate 7 output fields
11. สร้าง Rebate Excel จาก POI ที่สำเร็จ
12. สร้าง Run Report
13. บันทึก Run History และ output artifacts

Collector ต้องดึงช่วง POI ที่กว้างพอให้ครอบคลุม Invoice ในเดือนเป้าหมาย ค่าเริ่มต้นคือวันแรกของเดือนก่อนหน้าถึงวันสุดท้ายของเดือนถัดไป แล้วใช้ Tax Invoice Date เป็นตัวตัดสินสุดท้าย ช่วงนี้เป็น typed module option ที่ validate ได้ ไม่ใช่ค่าที่ผู้ใช้ทั่วไปต้องตั้งใน UI

## 12. Reconciliation Rules

1. ใช้ `POI Document No` เป็น key หลักระหว่าง Web และ Legacy
2. ถ้า POI พบทั้งสองระบบ ให้ใช้ POI จาก Web ทั้งชุด
3. ไม่ผสม field รายคอลัมน์จาก Legacy เข้า POI ที่ Web ชนะ
4. ถ้า POI พบเฉพาะ Web ให้นำข้อมูล Web เข้า output
5. ถ้า POI พบเฉพาะ Legacy ให้นำข้อมูล Legacy เข้า output
6. ภายใน POI ที่เลือก ให้นำทุก product line ที่ Tax Invoice Date อยู่ในเดือนเป้าหมายเข้า output
7. ห้าม deduplicate ด้วย Invoice No + Product Code เพียงอย่างเดียว เพราะสินค้าเดียวกันอาจมีหลายบรรทัดที่ถูกต้อง
8. เมื่อมี source line identifier ให้ใช้ identifier นั้น หากไม่มี ให้สร้าง internal line identity จาก Invoice No, Tax Invoice Date, product codes, Qty, Value และ source line ordinal
9. Tax Invoice No และ Date ซ้ำในหลายแถวได้

## 13. Error Semantics

ไม่มี automatic retry

### Run-level failure

หาก preflight, login หรือการดึงรายการ POI ระดับต้นทางล้มเหลวจนไม่สามารถระบุรายการที่จะประมวลผลได้ ให้สถานะ run เป็น `FAILED` และไม่สร้าง Rebate Excel

### POI-level failure

หาก network/API, web element, Legacy window/control หรือ parsing ล้มเหลวขณะประมวลผล POI ที่ระบุได้แล้ว:

- บันทึก POI เป็น `PARTIAL_FAILED`
- ไม่ retry
- เก็บ Source System, POI Document No, failed step, error code/message, timestamp และ screenshot เมื่อทำได้
- ข้าม POI นั้นและทำ POI ถัดไป
- เมื่อจบงาน สร้าง Rebate Excel จาก POI ที่สำเร็จ
- สร้าง Run Report ที่ระบุ POI ที่ขาดทั้งหมด
- สถานะรวมเป็น `PARTIAL_FAILED`

### Process interruption

หากโปรแกรมถูกปิดหรือเครื่องดับ งานเดิมไม่ resume เมื่อเปิดโปรแกรมอีกครั้ง Platform เปลี่ยน run ที่ค้างเป็น `INTERRUPTED` และผู้ใช้ต้องเริ่ม run ใหม่ตั้งแต่ต้น

สถานะมาตรฐานคือ `RUNNING`, `SUCCESS`, `PARTIAL_FAILED`, `FAILED`, `INTERRUPTED` และ `CANCELLED`

## 14. Outputs

### Rebate Excel

- ใช้ `Excel_Template.xlsx` เป็นฐาน
- เขียนข้อมูลตั้งแต่แถว 2
- ขยาย Excel Table ให้ครอบคลุมข้อมูลทั้งหมด
- หนึ่งแถวต่อ product line
- Preserve header และ workbook structure เดิม
- สร้างไฟล์ผ่าน temporary path และย้ายเป็น final name เมื่อเขียนสำเร็จ
- ชื่อเริ่มต้น: `Rebate_YYYY-MM_<RunId>.xlsx`

### Run Report

ชื่อเริ่มต้น: `Rebate_YYYY-MM_<RunId>_RunReport.xlsx`

ประกอบด้วย:

- Run ID, เดือน, เวลาเริ่ม/จบ และสถานะรวม
- จำนวน POI จาก Web และ Legacy
- จำนวน POI ที่ซ้ำและถูกเลือกจาก Web
- จำนวน POI สำเร็จ ล้มเหลว และจำนวน output lines
- Failure rows: Source, POI Document No, failed step, error และ screenshot path
- Paths ของ output artifacts

## 15. Credential และ Security

- Web และ Legacy ใช้ credential แยกกัน
- Password เก็บใน Windows Credential Manager
- ห้ามเก็บ password, session token หรือ authentication state ใน log
- หากเก็บ browser authentication state ต้องป้องกันด้วย user-scoped encryption และไม่ commit ลง source control
- Screenshot ต้องพยายามหลีกเลี่ยงหน้าที่มี credential
- Log และ Run Report ใช้ structured error code ควบคู่กับข้อความสำหรับผู้ใช้

## 16. MVP Testing Strategy

### Core Rules Tests

- กรองด้วย Tax Invoice Date
- POI ซ้ำให้ Web ชนะ
- POI เฉพาะ Legacy ไม่หาย
- POI ล้มเหลวทำให้ผลรวมเป็น `PARTIAL_FAILED`

### Collector Smoke Tests

- Web login และดึง POI ตัวอย่างได้หนึ่งรายการ
- Legacy login และดึง POI ตัวอย่างได้หนึ่งรายการ
- รันเมื่อติดตั้งหรือเปลี่ยน selector/locator ไม่บังคับในทุก build

### Excel Output Test

- จำนวนแถวและ 7 columns ถูกต้อง
- leading zeros ของ identifiers ไม่หาย
- ไฟล์เปิดได้และ Excel Table ขยายถูกต้อง

### Manual Acceptance Test

- ใช้หนึ่งเดือนตัวอย่างที่มีผลตรวจด้วยคน
- เปรียบเทียบจำนวน POI, invoice lines และยอดรวม Value
- ตรวจว่า Run Report ระบุ POI ที่ล้มเหลวครบ

## 17. Acceptance Criteria

- ผู้ใช้เลือกเดือนและเริ่มงานจาก Rebate module ได้
- ระบบ login Web และ Legacy อัตโนมัติ
- POI จากทั้งสองระบบถูก union โดย Web ชนะเมื่อ POI Document No ซ้ำ
- รอบเดือนตัดสินด้วย Tax Invoice Date
- POI เฉพาะ Legacy ถูกส่งออก
- POI-level errors ไม่หยุด POI ถัดไปและไม่มี retry
- `PARTIAL_FAILED` สร้าง Excel จากรายการสำเร็จพร้อม Run Report
- Rebate Excel มี 7 columns ตรง Template และหนึ่งแถวต่อ product line
- Product codes และ invoice numbers ไม่เสีย leading zeros
- Run History แสดงสถานะและเปิด output artifacts ได้
- เพิ่ม Automation module ใหม่ได้โดยไม่แก้ Platform business logic หรือ Rebate business logic

## 18. Source References

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- Generic Host in WPF: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder
- CommunityToolkit.Mvvm: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- Clean Architecture: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
- .NET plugin sample: https://learn.microsoft.com/en-us/samples/dotnet/samples/appwithplugin-demo/
- Windows UI Automation overview: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview
- Playwright APIRequestContext: https://playwright.dev/dotnet/docs/api/class-apirequestcontext
