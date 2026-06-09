# LUKS-Attendance

Windows desktop application for attendance processing and salary calculation. Built with C# WPF (.NET 8).

## Download

Get the latest .exe from [Actions → latest build → Artifacts](https://github.com/imran-habib/LUKS-Attendance/actions).

No installation needed — just run the .exe.

## How to Use

1. Run `LUKS-Attendance.exe`
2. Click **📂 Load Attendance** → select your `.xlsx` file from the attendance machine
3. Go to **⚠️ Issues** tab → enter OUT times for last day and missing punches
4. Review **💰 Salary** tab → edit Advance/Arrears/Extra Hrs (auto-recalculates)
5. Click **📄 Export PDF** or **🖨️ Print** for records

## Features

- Load `.xlsx` attendance files from attendance software
- In-app review: Attendance, Salary, Employee DB, Issues tabs
- Interactive issue resolution (missing punches, last-day OUT times)
- Live salary recalculation when editing Advance/Arrears/Extra Hrs
- PDF export for record-keeping
- Print directly to any printer
- Excel export with 3 tabs (Attendance, Salary, Employee DB)
- Lunch break: 1h deducted if OUT ≥ 13:00, no deduction for half days (OUT < 13:00)
- 15-minute rounding on worked hours
- OT/Deduction at hourly rate (Daily Rate ÷ 8)
- Monthly workers tracked for presence only (not in salary)
- Owners/managers excluded from salary

## Salary Formula

```
Net Salary = Days × DailyRate + (NetHours + ExtraHrs) × HourlyRate - Advance + Arrears
```

Where:
- HourlyRate = DailyRate ÷ 8
- NetHours = OT hours − Deduction hours
- Worked hours = Presence − 1h lunch (if OUT ≥ 13:00)

## Auto-Build

Every push to `main` triggers GitHub Actions which builds a fresh Windows .exe automatically.
