#nullable enable
namespace LuksAttendance;

public class EmployeeEntry
{
    public string Name { get; set; } = "";
    public int DailyRate { get; set; }
    public string Type { get; set; } = "weekly"; // weekly, monthly, excluded
    public string Category { get; set; } = "Cushioning"; // Cushioning, Polishing, Carpenter, etc.
    public bool IsGrace { get; set; } // Bug #11: configurable grace flag
}

public static class DefaultData
{
    public static readonly (string Name, int Rate, string Type, string Category, bool IsGrace)[] Employees =
    {
        ("Abdul Rehman", 1460, "weekly", "Carpenter", false),
        ("Abid Ustad", 1460, "weekly", "Carpenter", false),
        ("Abdul Razaq", 1000, "monthly", "Helper", false),
        ("Adeel Ustad", 1520, "weekly", "Polishing", false),
        ("Akash", 1000, "monthly", "Helper", false),
        ("Ali Rashd", 0, "excluded", "", false),
        ("Arif Chacha", 1000, "monthly", "Helper", false),
        ("Arman", 250, "weekly", "Cushioning", false),
        ("Aziz", 1170, "weekly", "Polishing", false),
        ("Bhola Ustad", 1510, "weekly", "Polishing", false),
        ("Ch Iftikhar", 1520, "weekly", "Polishing", false),
        ("Faisal Habib", 0, "excluded", "", false),
        ("Gulzaib", 1405, "weekly", "Polishing", false),
        ("Hafeez Chacha", 1000, "monthly", "Helper", false),
        ("Haris", 540, "weekly", "Polishing", false),
        ("Haseeb", 540, "weekly", "Polishing", true),
        ("Imran Habib", 0, "excluded", "", false),
        ("Imran Ustad", 1695, "weekly", "Carpenter", false),
        ("Irfan Maana", 1695, "weekly", "Polishing", false),
        ("Ishtiaq Ustad", 1780, "weekly", "Polishing", false),
        ("Kamran", 1000, "monthly", "Helper", false),
        ("Khalid Ustad", 1620, "weekly", "Polishing", false),
        ("Khalil Anwar", 1460, "weekly", "Carpenter", false),
        ("Khyber Lala", 0, "excluded", "", false),
        ("Liaqat Khan", 1000, "monthly", "Helper", false),
        ("Mumtaz Ustad", 3200, "weekly", "Polishing", false),
        ("Qadir Paapa", 2100, "weekly", "Cushioning", false),
        ("Rab Nawaz", 1400, "weekly", "Polishing", false),
        ("Saad Ali", 595, "weekly", "Polishing", false),
        ("Saif Ur Rehman", 1225, "weekly", "Polishing", false),
        ("Saqib Ustad", 1685, "weekly", "Carpenter", false),
        ("Tanveer", 1000, "monthly", "Helper", false),
        ("Waseem Doctor", 1695, "weekly", "Polishing", false),
        ("Zameer Ustad", 2085, "weekly", "Carpenter", false),
        ("Zubair Khan", 970, "weekly", "Polishing", true),
    };
}
