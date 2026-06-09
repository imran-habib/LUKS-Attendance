"""salary_rules.py - Salary calculation with configurable rules."""
from datetime import timedelta

# --- Configuration ---
STANDARD_DAY_HOURS = 9  # including 1h lunch
HOURLY_DIVISOR = 8      # daily_rate / 8 = hourly_rate
ROUNDING_MINUTES = 15   # round worked hours to nearest 15 min

# Excluded from salary (owners/managers) - just track presence
EXCLUDED_FROM_SALARY = {
    "ali rashd", "faisal habib", "imran habib",
    "khyber lala", "zameer ustad"
}

# Monthly workers (show only on first Thursday of month)
MONTHLY_WORKERS = {
    "arif chacha", "hafeez chacha", "kamran"
}


def round_to_nearest(td, minutes=ROUNDING_MINUTES):
    """Round timedelta to nearest N minutes."""
    total_seconds = td.total_seconds()
    chunk = minutes * 60
    rounded = round(total_seconds / chunk) * chunk
    return timedelta(seconds=rounded)


def td_to_hhmm(td):
    """Format timedelta as HH:MM string."""
    total = int(td.total_seconds())
    sign = "-" if total < 0 else ""
    total = abs(total)
    h, m = divmod(total // 60, 60)
    return f"{sign}{h:02d}:{m:02d}"
