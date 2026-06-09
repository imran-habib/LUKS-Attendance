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
        ("Qadir Paapa", 2100, "weekly", "Cushioning"),
        ("Ch Iftikhar", 1520, "weekly", "Polishing"),
        ("Zubair Khan", 970, "weekly", "Polishing"),
        ("Rab Nawaz", 1400, "weekly", "Polishing"),
        ("Adeel Ustad", 1520, "weekly", "Polishing"),
        ("Ishtiaq Ustad", 1780, "weekly", "Carpenter"),
        ("Imran Ustad", 1695, "weekly", "Carpenter"),
        ("Saqib Ustad", 1685, "weekly", "Carpenter"),
        ("Waseem Doctor", 1695, "weekly", "Carpenter"),
        ("Irfan Maana", 1695, "weekly", "Carpenter"),
        ("Abdul Rehman", 1460, "weekly", "Carpenter"),
        ("Haseeb", 540, "weekly", "Cushioning"),
        ("Saad Ali", 595, "weekly", "Polishing"),
        ("Gulzaib", 1405, "weekly", "Cushioning"),
        ("Abid", 1460, "weekly", "Cushioning"),
        ("Khalil Anwar", 1460, "weekly", "Carpenter"),
        ("Bhola Ustad", 1510, "weekly", "Polishing"),
        ("Mumtaz Ustad", 3200, "weekly", "Carpenter"),
        ("Ishtiaq", 1295, "weekly", "Cushioning"),
        ("Aziz", 1170, "weekly", "Cushioning"),
        ("Saif Ur Rehman", 1225, "weekly", "Cushioning"),
        ("Khalid Ustad", 1620, "weekly", "Polishing"),
        ("Haris", 540, "weekly", "Cushioning"),
        ("Arman", 250, "weekly", "Cushioning"),
        ("Akash", 1000, "weekly", "Cushioning"),
        ("Ali Rashd", 0, "excluded", ""),
        ("Faisal Habib", 0, "excluded", ""),
        ("Imran Habib", 0, "excluded", ""),
        ("Khyber Lala", 0, "excluded", ""),
        ("Zameer Ustad", 0, "excluded", ""),
        ("Arif Chacha", 1000, "monthly", ""),
        ("Hafeez Chacha", 1000, "monthly", ""),
        ("Kamran", 1000, "monthly", ""),
    };
}
