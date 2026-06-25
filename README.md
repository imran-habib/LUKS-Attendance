# LUKS Attendance & Salary Software

Windows desktop application for attendance processing and salary calculation. Built with C# WPF (.NET 8).

## ⬇️ Download

**[Download LUKS-Attendance.exe (Latest)](https://github.com/imran-habib/LUKS-Attendance/releases/latest/download/LUKS-Attendance.exe)**

No installation needed — just download and run the .exe.

> Size: ~91 MB | Requires: Windows 10/11 (64-bit)

---

## How to Use

1. Run `LUKS-Attendance.exe`
2. Login with your credentials
3. Click **📂 Load Attendance** → select your `.xlsx` file from the attendance machine
4. Go to **⚠️ Issues** tab → enter OUT times for last day and missing punches
5. Review **💰 Salary** tab → edit Advance/Arrears/Extra Hrs (auto-recalculates)
6. Click **📄 Export PDF** or **🖨️ Print** for records

## Features

- Load `.xls/.xlsx` attendance files (single or multi-file merge for cross-month weeks)
- Payroll Wizard (4-step guided flow)
- Auto-resolve midnight exits (punches before 3:30am = previous day's exit)
- Weekly workers: full OT/deduction (8h standard, 15-min rounding, 1h lunch if OUT ≥ 13:00)
- Monthly workers: just count presence days (any punch = 1 day)
- Grace period for configurable employees (checkbox in Employee DB)
- Traffic light indicators (🟢🟡🔴) comparing salary to 4-week average
- Analytics tab with trend charts, category breakdown, OT tracking, forecasting
- Direct device connection (ZK protocol over TCP)
- PDF/Excel export with DailyRate and HourlyRate columns
- SQLite history for previous payrolls
- Auto-updater checks GitHub for new builds

## Salary Formula

```
Net Salary = Days × DailyRate + (NetHours + ExtraHrs) × HourlyRate - Advance + Arrears
```

Where:
- HourlyRate = DailyRate ÷ 8
- NetHours = OT hours − Deduction hours
- Worked hours = Presence − 1h lunch (if OUT ≥ 13:00)
- 15-minute rounding applied after lunch deduction

## Auto-Build

Every push to `main` triggers GitHub Actions which builds a fresh .exe and publishes it as a [GitHub Release](https://github.com/imran-habib/LUKS-Attendance/releases/latest).
