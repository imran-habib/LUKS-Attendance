# LUKS-Attendance

Windows desktop application for attendance processing and salary calculation. Built with C# WPF (.NET 8).

## Features

- Load attendance files (.xls/.xlsx) from attendance machine
- In-app review of attendance data, salary calculations, and employee database
- Interactive issue resolution (missing punches, last-day OUT times)
- PDF export for record-keeping
- Print directly from the application
- Excel export with Attendance, Salary, and Employee DB tabs
- Lunch break logic: 1h deducted if OUT >= 13:00, no deduction for half days
- 15-minute rounding on worked hours
- Carry-over of Advance/Arrears from previous salary sheets

## Build

Requires .NET 8 SDK on Windows:

```cmd
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\LUKS-Attendance.exe`

## Quick Start

1. Run `LUKS-Attendance.exe`
2. Click "Load Attendance" → select your attendance file
3. Resolve any issues in the Issues tab (enter OUT times for last day / missing punches)
4. Review Salary tab
5. Export PDF / Print / Export Excel

## Project Structure

```
LuksAttendance.csproj   - Project file with NuGet dependencies
App.xaml/.cs            - Application entry point
MainWindow.xaml/.cs     - Main GUI with tabs and toolbar
FileReader.cs           - .xls/.xlsx reading with ClosedXML
PunchParser.cs          - Punch parsing and issue detection
SalaryCalc.cs           - Salary calculation (lunch, rounding, OT/deduction)
ExcelExporter.cs        - Excel output generation
PdfExporter.cs          - PDF generation and printing (QuestPDF)
Models.cs               - Data models and default employee database
Assets/logo.png         - LUKS logo
```
