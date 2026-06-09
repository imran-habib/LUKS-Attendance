"""salary_rules.py - Salary calculation with configurable rules."""
from datetime import timedelta

# --- Configuration ---
STANDARD_WORK_HOURS = 8   # effective working hours expected per day
LUNCH_BREAK_HOURS = 1     # lunch deducted if OUT >= 13:00
LUNCH_CUTOFF = "13:00"    # if OUT before this, no lunch deduction
HOURLY_DIVISOR = 8        # daily_rate / 8 = hourly_rate
ROUNDING_MINUTES = 15     # round worked hours to nearest 15 min
STANDARD_DAY_HOURS = 9    # total presence expected (work + lunch)

# Excluded from salary (owners/managers) - just track presence
EXCLUDED_FROM_SALARY = {
    "ali rashd", "faisal habib", "imran habib",
    "khyber lala", "zameer ustad"
}

# Monthly workers
MONTHLY_WORKERS = {
    "arif chacha", "hafeez chacha", "kamran"
}


def round_to_nearest(td, minutes=ROUNDING_MINUTES):
    """Round timedelta to nearest N minutes."""
    total_seconds = td.total_seconds()
    chunk = minutes * 60
    rounded = round(total_seconds / chunk) * chunk
    return timedelta(seconds=rounded)


def calc_effective_hours(presence_td, out_time_str):
    """Calculate effective working hours from presence duration.
    
    Rules:
    - If OUT >= 13:00: deduct 1h lunch. Effective = presence - 1h.
    - If OUT < 13:00: no lunch deduction. Effective = presence.
    
    Returns: (effective_work timedelta, is_half_day bool)
    """
    out_hour = int(out_time_str.split(":")[0])
    out_min = int(out_time_str.split(":")[1])
    out_total = out_hour * 60 + out_min
    lunch_cutoff = 13 * 60  # 13:00 in minutes
    
    if out_total >= lunch_cutoff:
        # Full day: deduct lunch
        effective = presence_td - timedelta(hours=LUNCH_BREAK_HOURS)
    else:
        # Half day: no lunch deduction
        effective = presence_td
    
    return effective


def td_to_hhmm(td):
    """Format timedelta as HH:MM string."""
    total = int(td.total_seconds())
    sign = "-" if total < 0 else ""
    total = abs(total)
    h, m = divmod(total // 60, 60)
    return f"{sign}{h:02d}:{m:02d}"
