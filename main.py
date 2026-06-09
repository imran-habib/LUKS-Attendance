"""LUKS-Attendance - Salary Processing Tool
Usage: python main.py <attendance_file.xls|xlsx> [previous_output.xlsx]
"""
import sys
import os
import re
from datetime import timedelta

from src.file_io import read_attendance
from src.parser import parse_punches, calc_worked_hours
from src.salary_rules import (
    EXCLUDED_FROM_SALARY, MONTHLY_WORKERS,
    round_to_nearest, td_to_hhmm,
    STANDARD_DAY_HOURS, ROUNDING_MINUTES
)
from src.exporter import generate_output, load_employee_db

# Default employee DB (used if no previous output exists)
DEFAULT_EMPLOYEE_DB = {
    "qadir paapa": {"daily_rate": 2100, "type": "weekly"},
    "ch iftikhar": {"daily_rate": 1520, "type": "weekly"},
    "zubair khan": {"daily_rate": 970, "type": "weekly"},
    "rab nawaz": {"daily_rate": 1400, "type": "weekly"},
    "adeel ustad": {"daily_rate": 1520, "type": "weekly"},
    "ishtiaq ustad": {"daily_rate": 1780, "type": "weekly"},
    "imran ustad": {"daily_rate": 1695, "type": "weekly"},
    "saqib ustad": {"daily_rate": 1685, "type": "weekly"},
    "waseem doctor": {"daily_rate": 1695, "type": "weekly"},
    "irfan maana": {"daily_rate": 1695, "type": "weekly"},
    "abdul rehman": {"daily_rate": 1460, "type": "weekly"},
    "haseeb": {"daily_rate": 540, "type": "weekly"},
    "saad ali": {"daily_rate": 595, "type": "weekly"},
    "gulzaib": {"daily_rate": 1405, "type": "weekly"},
    "abid": {"daily_rate": 1460, "type": "weekly"},
    "khalil anwar": {"daily_rate": 1460, "type": "weekly"},
    "bhola ustad": {"daily_rate": 1510, "type": "weekly"},
    "mumtaz ustad": {"daily_rate": 3200, "type": "weekly"},
    "ishtiaq": {"daily_rate": 1295, "type": "weekly"},
    "aziz": {"daily_rate": 1170, "type": "weekly"},
    "saif ur rehman": {"daily_rate": 1225, "type": "weekly"},
    "khalid ustad": {"daily_rate": 1620, "type": "weekly"},
    "haris": {"daily_rate": 540, "type": "weekly"},
    "arman": {"daily_rate": 250, "type": "weekly"},
    "akash": {"daily_rate": 1000, "type": "weekly"},
    "ali rashd": {"daily_rate": 0, "type": "excluded"},
    "faisal habib": {"daily_rate": 0, "type": "excluded"},
    "imran habib": {"daily_rate": 0, "type": "excluded"},
    "khyber lala": {"daily_rate": 0, "type": "excluded"},
    "zameer ustad": {"daily_rate": 0, "type": "excluded"},
    "arif chacha": {"daily_rate": 1000, "type": "monthly"},
    "hafeez chacha": {"daily_rate": 1000, "type": "monthly"},
    "kamran": {"daily_rate": 1000, "type": "monthly"},
}


def main():
    print("\n\u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557")
    print("\u2551     LUKS Attendance & Salary Tool    \u2551")
    print("\u255a\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255d\n")

    # Get input file
    if len(sys.argv) > 1:
        input_file = sys.argv[1]
    else:
        input_file = input("Enter attendance file path (.xls/.xlsx): ").strip().strip('"')

    if not os.path.exists(input_file):
        print(f"\u274c File not found: {input_file}")
        sys.exit(1)

    # Previous output for carry-over
    prev_file = sys.argv[2] if len(sys.argv) > 2 else None
    if not prev_file:
        prev_input = input("Previous salary output file (Enter to skip): ").strip().strip('"')
        prev_file = prev_input if prev_input and os.path.exists(prev_input) else None

    # Load employee DB and carry-over from previous
    employee_db, carry_over = load_employee_db(prev_file)
    if not employee_db:
        employee_db = DEFAULT_EMPLOYEE_DB.copy()
        print("\u2139\ufe0f  Using default employee database (no previous output found)")

    # Read attendance
    print(f"\n\U0001f4c2 Reading: {input_file}")
    data = read_attendance(input_file)
    print(f"   Period: {data['duration']}")
    print(f"   Days: {[d[1] for d in data['days']]}")
    print(f"   Employees found: {len(data['employees'])}")

    last_day_num = data["days"][-1][1] if data["days"] else 0

    # Parse punches
    records, issues = parse_punches(data["employees"], data["days"], last_day_num)

    # --- HR Interactive Resolution ---
    flagged = set()

    # Handle last-day OUT times
    last_day_issues = [i for i in issues if i["type"] == "last_day_out"]
    if last_day_issues:
        print(f"\n\u23f0 Last day (day {last_day_num}) - Enter OUT times:")
        print("   (Press Enter for default 17:00, type 'skip' to exclude)")
        for issue in last_day_issues:
            while True:
                ans = input(f"   {issue['name']} (IN: {issue['in_time']}): ").strip()
                if ans.lower() == "skip":
                    break
                if not ans:
                    ans = "17:00"
                if _valid_time(ans):
                    records.append({
                        "name": issue["name"], "day": issue["day"],
                        "in_time": issue["in_time"], "out_time": ans,
                        "status": "hr_entered"
                    })
                    break
                print("     \u26a0\ufe0f  Invalid time format. Use HH:MM")

    # Handle missing OUT punches
    missing_issues = [i for i in issues if i["type"] == "missing_out"]
    if missing_issues:
        print(f"\n\u26a0\ufe0f  Missing OUT punches ({len(missing_issues)} records):")
        print("   (Enter OUT time, or 'skip' to exclude from salary)")
        for issue in missing_issues:
            while True:
                ans = input(f"   {issue['name']} Day {issue['day']} (IN: {issue['in_time']}): ").strip()
                if ans.lower() == "skip":
                    flagged.add((issue["name"], issue["day"]))
                    break
                if _valid_time(ans):
                    records.append({
                        "name": issue["name"], "day": issue["day"],
                        "in_time": issue["in_time"], "out_time": ans,
                        "status": "hr_entered"
                    })
                    break
                print("     \u26a0\ufe0f  Invalid time format. Use HH:MM")

    # Flag multi-punch for verification
    multi_issues = [i for i in issues if i["type"] == "multi_punch"]
    if multi_issues:
        print(f"\n\U0001f50d Multi-punch records (auto-resolved, flagged for verification):")
        for issue in multi_issues:
            print(f"   {issue['name']} Day {issue['day']}: {issue['times']} -> IN={issue['resolved_in']} OUT={issue['resolved_out']}")
            flagged.add((issue["name"], issue["day"]))

    # --- Build attendance output rows ---
    standard = timedelta(hours=STANDARD_DAY_HOURS)
    attendance_rows = []
    emp_records = {}  # name_lower -> [records]

    for rec in records:
        name = rec["name"]
        name_lower = name.lower().strip()

        if rec.get("in_time") and rec.get("out_time"):
            worked = calc_worked_hours(rec["in_time"], rec["out_time"])
            worked = round_to_nearest(worked, ROUNDING_MINUTES)
            diff = worked - standard
            ot = diff if diff > timedelta(0) else timedelta(0)
            ded = abs(diff) if diff < timedelta(0) else timedelta(0)
        else:
            worked = ot = ded = timedelta(0)

        att_row = {
            "name": name, "day": rec["day"],
            "in_time": rec.get("in_time", ""),
            "out_time": rec.get("out_time", ""),
            "worked": td_to_hhmm(worked),
            "ot": td_to_hhmm(ot) if ot > timedelta(0) else "",
            "deduction": td_to_hhmm(ded) if ded > timedelta(0) else "",
            "status": rec.get("status", "ok"),
        }
        attendance_rows.append(att_row)

        if name_lower not in emp_records:
            emp_records[name_lower] = []
        emp_records[name_lower].append(rec)

    # --- Salary Calculation ---
    salary_records = []
    excluded_summary = []

    for name_lower, recs in sorted(emp_records.items()):
        db_entry = employee_db.get(name_lower, {})
        etype = db_entry.get("type", "weekly")
        daily_rate = db_entry.get("daily_rate", 0)

        if etype == "excluded" or name_lower in EXCLUDED_FROM_SALARY:
            excluded_summary.append((name_lower, len(recs)))
            continue

        if etype == "monthly" or name_lower in MONTHLY_WORKERS:
            pass  # include anyway, HR decides

        prev = carry_over.get(name_lower, {})
        hourly_rate = daily_rate / 8 if daily_rate else 0

        # Calculate OT and deduction
        total_ot = timedelta()
        total_ded = timedelta()
        days_worked = 0

        for r in recs:
            if r.get("in_time") and r.get("out_time"):
                w = calc_worked_hours(r["in_time"], r["out_time"])
                w = round_to_nearest(w, ROUNDING_MINUTES)
                diff = w - standard
                if diff > timedelta(0):
                    total_ot += diff
                elif diff < timedelta(0):
                    total_ded += abs(diff)
                days_worked += 1

        ot_hours = round(total_ot.total_seconds() / 3600, 2)
        ded_hours = round(total_ded.total_seconds() / 3600, 2)
        net_hours = round(ot_hours - ded_hours, 2)

        salary_records.append({
            "name": " ".join(w.capitalize() for w in name_lower.split()),
            "days": days_worked,
            "ot_hours": ot_hours,
            "ded_hours": ded_hours,
            "net_hours": net_hours,
            "daily_rate": daily_rate,
            "hourly_rate": round(hourly_rate, 2),
            "advance": prev.get("advance", 0),
            "arrears": prev.get("arrears", 0),
            "net_salary": round(days_worked * daily_rate + net_hours * hourly_rate - prev.get("advance", 0) + prev.get("arrears", 0), 2),
        })

    salary_records.sort(key=lambda x: x["name"])

    # --- Output ---
    output_dir = os.path.dirname(os.path.abspath(input_file))
    output_path = os.path.join(output_dir, "Salary_Sheet.xlsx")

    generate_output(attendance_rows, salary_records, employee_db, output_path, flagged)

    # --- Summary ---
    print(f"\n{'='*45}")
    print(f"\u2705 DONE! Output: {output_path}")
    print(f"   Employees processed: {len(emp_records)}")
    print(f"   Salary calculated for: {len(salary_records)}")
    print(f"   Days in period: {len(data['days'])}")
    if excluded_summary:
        print(f"   Excluded (owners/managers): {', '.join(n for n, _ in excluded_summary)}")
    if flagged:
        print(f"   \u26a0\ufe0f  Records needing verification: {len(flagged)}")
    print()


def _valid_time(s):
    """Check if string is valid HH:MM format."""
    return bool(re.match(r"^\d{2}:\d{2}$", s))


if __name__ == "__main__":
    main()
