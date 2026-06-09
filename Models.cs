namespace LuksAttendance;

public class EmployeeEntry
{
    public string Name { get; set; } = "";
    public int DailyRate { get; set; }
    public string Type { get; set; } = "weekly"; // weekly, monthly, excluded
}

public static class DefaultData
{
    public static readonly (string Name, int Rate, string Type)[] Employees =
    {
        ("Qadir Paapa", 2100, "weekly"),
        ("Ch Iftikhar", 1520, "weekly"),
        ("Zubair Khan", 970, "weekly"),
        ("Rab Nawaz", 1400, "weekly"),
        ("Adeel Ustad", 1520, "weekly"),
        ("Ishtiaq Ustad", 1780, "weekly"),
        ("Imran Ustad", 1695, "weekly"),
        ("Saqib Ustad", 1685, "weekly"),
        ("Waseem Doctor", 1695, "weekly"),
        ("Irfan Maana", 1695, "weekly"),
        ("Abdul Rehman", 1460, "weekly"),
        ("Haseeb", 540, "weekly"),
        ("Saad Ali", 595, "weekly"),
        ("Gulzaib", 1405, "weekly"),
        ("Abid", 1460, "weekly"),
        ("Khalil Anwar", 1460, "weekly"),
        ("Bhola Ustad", 1510, "weekly"),
        ("Mumtaz Ustad", 3200, "weekly"),
        ("Ishtiaq", 1295, "weekly"),
        ("Aziz", 1170, "weekly"),
        ("Saif Ur Rehman", 1225, "weekly"),
        ("Khalid Ustad", 1620, "weekly"),
        ("Haris", 540, "weekly"),
        ("Arman", 250, "weekly"),
        ("Akash", 1000, "weekly"),
        ("Ali Rashd", 0, "excluded"),
        ("Faisal Habib", 0, "excluded"),
        ("Imran Habib", 0, "excluded"),
        ("Khyber Lala", 0, "excluded"),
        ("Zameer Ustad", 0, "excluded"),
        ("Arif Chacha", 1000, "monthly"),
        ("Hafeez Chacha", 1000, "monthly"),
        ("Kamran", 1000, "monthly"),
    };
}
