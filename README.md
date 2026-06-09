# LUKS-Attendance

Salary processing tool for LUKS. Reads attendance machine exports (.xls/.xlsx), calculates working hours with OT/deduction, prompts HR for missing punches, and generates a formatted salary sheet.

## Usage

```
python main.py "C:\path\to\01Summary.xlsx"
python main.py "C:\path\to\01Summary.xlsx" "C:\path\to\previous_Salary_Sheet.xlsx"
```

Or just run the .exe and it will prompt for the file path.

## Build Windows .exe

```bash
pip install -r requirements.txt
pyinstaller --onefile --icon=assets/logo.png --name LUKS-Attendance main.py
```

The .exe will be in the `dist/` folder.

## Features

- Reads .xls and .xlsx attendance files (auto-converts .xls)
- Handles merged cells from attendance software
- Parses multi-punch cells (3+ times) with smart resolution
- Interactive HR prompts for missing OUT punches and last-day times
- 15-minute rounding on worked hours
- OT/Deduction at hourly rate (daily ÷ 8)
- Carries over Advance/Arrears from previous output
- Employee DB tab editable by HR
- Flagged rows highlighted in yellow/red for verification

## Project Structure

```
main.py              - CLI entry point, HR interaction
src/
  file_io.py         - .xls/.xlsx reading, merged cell handling
  parser.py          - Punch parsing, multi-time resolution
  salary_rules.py    - Configurable salary constants and rounding
  exporter.py        - Excel output generation (3 tabs)
assets/
  logo.png           - LUKS logo
requirements.txt     - Python dependencies
```
