using System;
#nullable enable
namespace LuksAttendance;

public class EmployeeEntry
{
    public string Name { get; set; } = "";
    public int DailyRate { get; set; }
    public string Type { get; set; } = "weekly"; // weekly, monthly, excluded
    public string Category { get; set; } = "Worker"; // Cushioning, Polishing, Carpenter, etc.
    public bool IsGrace { get; set; } // Grace period flag (configurable per employee)
}

public static class DefaultData
{
    /// <summary>
    /// Real employee data from LUKS INTERIORS production payroll (Jun 2026).
    /// Used as fallback on first run before DB is configured.
    /// </summary>
    public static readonly (string Name, int Rate, string Type, string Category, bool IsGrace)[] Employees =
    {
        // ═══ Carpenter ═══
        ("Abdul Rehman",    1460, "weekly", "Carpenter",  false),
        ("Abid Ustad",     1460, "weekly", "Carpenter",  false),
        ("Imran Ustad",    1695, "weekly", "Carpenter",  false),
        ("Khalil Anwar",   1460, "weekly", "Carpenter",  false),
        ("Saqib Ustad",    1685, "weekly", "Carpenter",  false),
        ("Zameer Ustad",   2085, "weekly", "Carpenter",  false),

        // ═══ Cushioning ═══
        ("Arman",           250, "weekly", "Cushioning", false),
        ("Qadir Paapa",    2100, "weekly", "Cushioning", false),

        // ═══ Polishing ═══
        ("Adeel Ustad",    1520, "weekly", "Polishing",  false),
        ("Ch Iftikhar",    1520, "weekly", "Polishing",  false),
        ("Gulzaib",        1405, "weekly", "Polishing",  false),
        ("Haseeb",          540, "weekly", "Polishing",  true),
        ("Irfan Maana",    1695, "weekly", "Polishing",  false),
        ("Ishtiaq Ustad",  1780, "weekly", "Polishing",  false),
        ("Mumtaz Ustad",   3200, "weekly", "Polishing",  false),
        ("Rab Nawaz",      1400, "weekly", "Polishing",  false),
        ("Saif Ur Rehman", 1225, "weekly", "Polishing",  false),
        ("Waseem Doctor",  1695, "weekly", "Polishing",  false),
        ("Zubair Khan",     970, "weekly", "Polishing",  true),
    };
}
