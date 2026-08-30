#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LuksAttendance;

/// <summary>
/// Shared salary computation service used by both MainWindow and PayrollWizard.
/// Eliminates duplicate logic and ensures consistent behavior.
/// </summary>
public static class SalaryService
{
    /// <summary>
    /// Compute salary rows from attendance data using the employee database.
    /// This is the single source of truth for salary calculation — both MainWindow
    /// and PayrollWizard call this instead of duplicating logic.
    /// </summary>
    public static List<SalaryRow> ComputeSalary(
        IEnumerable<AttendanceRow> attendanceRows,
        IEnumerable<EmployeeEntry> employeeDb,
        string mode, // "weekly" or "monthly"
        HashSet<string>? otExemptCategories = null,
        HashSet<string>? otExemptEmployees = null)
    {
        var salaryRows = new List<SalaryRow>();

        // Load OT-exempt lists from DB if not provided
        otExemptCategories ??= DatabaseService.LoadOtExemptCategories();
        otExemptEmployees ??= DatabaseService.LoadOtExemptEmployees();

        var dbDict = new Dictionary<string, EmployeeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in employeeDb)
        {
            var key = emp.Name.ToLower();
            if (!string.IsNullOrWhiteSpace(key) && !dbDict.ContainsKey(key))
                dbDict[key] = emp;
        }

        var grouped = attendanceRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .GroupBy(r => r.Name.ToLower());

        foreach (var grp in grouped.OrderBy(g => g.Key))
        {
            if (!dbDict.TryGetValue(grp.Key, out var entry)) continue;
            if (entry.Type == "excluded") continue;
            if (entry.DailyRate <= 0) continue;
            if (entry.Type != mode) continue;

            // OT-exempt if category is exempt OR individual employee is exempt
            bool isOtExempt = otExemptCategories.Contains(entry.Category)
                           || otExemptEmployees.Contains(entry.Name);
            var sal = SalaryCalc.Calculate(grp.Key, grp.ToList(), entry.DailyRate, 0, 0, entry.Category, entry.Type == "monthly", isOtExempt);
            salaryRows.Add(sal);
        }

        // Traffic light indicators (compare to 4-week average)
        try
        {
            var averages = DatabaseService.GetEmployeeAverages(4);
            foreach (var row in salaryRows)
            {
                if (averages.TryGetValue(row.Name, out double avg) && avg > 0)
                {
                    double diff = Math.Abs((double)row.NetSalary - avg) / avg;
                    row.StatusIndicator = diff <= 0.15 ? "🟢" : diff <= 0.30 ? "🟡" : "🔴";
                }
                else
                    row.StatusIndicator = "⚪";
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("SalaryService.TrafficLights", ex);
        }

        return salaryRows;
    }
}
