#nullable enable
namespace LuksAttendance;

public class EmployeeEntry
{
    public string Name { get; set; } = "";
    public int DailyRate { get; set; }
    public string Type { get; set; } = "weekly"; // weekly, monthly, excluded
    public string Category { get; set; } = "Cushioning"; // Cushioning, Polishing, Carpenter, etc.
}

public static class DefaultData
{
    public static readonly (string Name, int Rate, string Type, string Category)[] Employees =
    {
        ("Abdul Rehman", 1460, "weekly", "Carpenter"),
        ("Abid Ustad", 1460, "weekly", "Carpenter"),
        ("Abdul Razaq", 1000, "monthly", "Helper"),
        ("Adeel Ustad", 1520, "weekly", "Polishing"),
        ("Akash", 1000, "monthly", "Helper"),
        ("Ali Rashd", 0, "excluded", ""),
        ("Arif Chacha", 1000, "monthly", "Helper"),
        ("Arman", 250, "weekly", "Cushioning"),
        ("Aziz", 1170, "weekly", "Polishing"),
        ("Bhola Ustad", 1510, "weekly", "Polishing"),
        ("Ch Iftikhar", 1520, "weekly", "Polishing"),
        ("Faisal Habib", 0, "excluded", ""),
        ("Gulzaib", 1405, "weekly", "Polishing"),
        ("Hafeez Chacha", 1000, "monthly", "Helper"),
        ("Haris", 540, "weekly", "Polishing"),
        ("Haseeb", 540, "weekly", "Polishing"),
        ("Imran Habib", 0, "excluded", ""),
        ("Imran Ustad", 1695, "weekly", "Carpenter"),
        ("Irfan Maana", 1695, "weekly", "Polishing"),
        ("Ishtiaq Ustad", 1780, "weekly", "Polishing"),
        ("Kamran", 1000, "monthly", "Helper"),
        ("Khalid Ustad", 1620, "weekly", "Polishing"),
        ("Khalil Anwar", 1460, "weekly", "Carpenter"),
        ("Khyber Lala", 0, "excluded", ""),
        ("Liaqat Khan", 1000, "monthly", "Helper"),
        ("Mumtaz Ustad", 3200, "weekly", "Polishing"),
        ("Qadir Paapa", 2100, "weekly", "Cushioning"),
        ("Rab Nawaz", 1400, "weekly", "Polishing"),
        ("Saad Ali", 595, "weekly", "Polishing"),
        ("Saif Ur Rehman", 1225, "weekly", "Polishing"),
        ("Saqib Ustad", 1685, "weekly", "Carpenter"),
        ("Tanveer", 1000, "monthly", "Helper"),
        ("Waseem Doctor", 1695, "weekly", "Polishing"),
        ("Zameer Ustad", 2085, "weekly", "Carpenter"),
        ("Zubair Khan", 970, "weekly", "Polishing"),
    };
}
