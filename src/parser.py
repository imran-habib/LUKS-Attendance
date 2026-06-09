"""parser.py - Parse punch data into IN/OUT pairs per employee per day."""
import re
from datetime import datetime, timedelta

TIME_RE = re.compile(r"\d{2}:\d{2}")


def parse_punches(employees, days, last_day_num):
    """Parse raw punch strings into structured records.
    
    Returns: (records, issues)
        records: [{name, day, in_time, out_time, status}, ...]
        issues: [{name, day, type, raw, ...}, ...]  # needs HR input
    """
    records = []
    issues = []
    
    for emp in employees:
        name = emp["name"]
        for _, day_num, _ in days:
            raw = emp["punches"].get(day_num, "")
            if not raw:
                continue
            
            times = TIME_RE.findall(raw)
            if not times:
                continue
            
            is_last_day = (day_num == last_day_num)
            
            if len(times) == 1:
                # Single punch - missing OUT (or last day needing HR input)
                if is_last_day:
                    issues.append({
                        "name": name, "day": day_num,
                        "type": "last_day_out",
                        "in_time": times[0], "raw": raw
                    })
                else:
                    issues.append({
                        "name": name, "day": day_num,
                        "type": "missing_out",
                        "in_time": times[0], "raw": raw
                    })
            
            elif len(times) == 2:
                # Normal IN/OUT pair
                records.append({
                    "name": name, "day": day_num,
                    "in_time": times[0], "out_time": times[1],
                    "status": "ok"
                })
            
            else:
                # 3+ punches: first after 06:00 = IN, last = OUT, flag for HR
                in_time, out_time = _resolve_multi(times)
                records.append({
                    "name": name, "day": day_num,
                    "in_time": in_time, "out_time": out_time,
                    "status": "multi_punch_verify"
                })
                issues.append({
                    "name": name, "day": day_num,
                    "type": "multi_punch",
                    "times": times, "resolved_in": in_time,
                    "resolved_out": out_time, "raw": raw
                })
    
    return records, issues


def _resolve_multi(times):
    """For 3+ punches: first punch >= 06:00 is IN, last punch is OUT."""
    in_time = None
    for t in times:
        h = int(t.split(":")[0])
        if h >= 6:
            in_time = t
            break
    if in_time is None:
        in_time = times[0]
    out_time = times[-1]
    return in_time, out_time


def calc_worked_hours(in_time_str, out_time_str):
    """Calculate worked timedelta from IN/OUT strings. Handles cross-midnight."""
    in_dt = datetime.strptime(in_time_str, "%H:%M")
    out_dt = datetime.strptime(out_time_str, "%H:%M")
    diff = out_dt - in_dt
    if diff < timedelta(0):
        diff += timedelta(hours=24)  # cross-midnight
    return diff
