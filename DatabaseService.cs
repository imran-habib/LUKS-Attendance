#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace LuksAttendance;

public static class DatabaseService
{
    private static string _dbPath = "";
    private static string _settingsFile = Path.Combine(AppContext.BaseDirectory, "luks_settings.txt");

    public static string DbPath => _dbPath;
    public static bool IsConfigured => !string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath);

    public static bool LoadSettings()
    {
        if (!File.Exists(_settingsFile)) return false;
        var path = File.ReadAllText(_settingsFile).Trim();
        if (string.IsNullOrEmpty(path)) return false;
        _dbPath = path;
        if (!File.Exists(_dbPath)) InitializeDb();
        return true;
    }

    public static void Configure(string folderPath)
    {
        _dbPath = Path.Combine(folderPath, "luks_salary.db");
        File.WriteAllText(_settingsFile, _dbPath);
        InitializeDb();
    }

    private static void InitializeDb()
    {
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SalaryRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WeekStart TEXT NOT NULL,
                WeekEnd TEXT NOT NULL,
                Name TEXT NOT NULL,
                Category TEXT,
                Days INTEGER,
                OtHours REAL,
                DedHours REAL,
                NetHours REAL,
                DailyRate INTEGER,
                HourlyRate REAL,
                ExtraHrs REAL,
                Advance REAL,
                Arrears REAL,
                NetSalary REAL,
                SavedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS WeeklySummary (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WeekStart TEXT NOT NULL,
                WeekEnd TEXT NOT NULL,
                TotalEmployees INTEGER,
                TotalPayout REAL,
                TotalOtHours REAL,
                TotalDedHours REAL,
                SavedAt TEXT NOT NULL,
                UNIQUE(WeekStart, WeekEnd)
            );
            CREATE INDEX IF NOT EXISTS idx_record_week ON SalaryRecord(WeekStart, WeekEnd);
            CREATE INDEX IF NOT EXISTS idx_record_name ON SalaryRecord(Name);
        ";
        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection Open() => new($"Data Source={_dbPath}");

    public static void SaveWeek(string weekStart, string weekEnd, IEnumerable<SalaryRow> rows)
    {
        if (!IsConfigured) return;
        using var conn = Open();
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Delete existing records for this week (re-save overwrites)
        using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM SalaryRecord WHERE WeekStart=@s AND WeekEnd=@e";
            del.Parameters.AddWithValue("@s", weekStart);
            del.Parameters.AddWithValue("@e", weekEnd);
            del.ExecuteNonQuery();
        }
        using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM WeeklySummary WHERE WeekStart=@s AND WeekEnd=@e";
            del.Parameters.AddWithValue("@s", weekStart);
            del.Parameters.AddWithValue("@e", weekEnd);
            del.ExecuteNonQuery();
        }

        var now = DateTime.Now.ToString("o");
        var list = rows.ToList();

        foreach (var r in list)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = @"INSERT INTO SalaryRecord
                (WeekStart,WeekEnd,Name,Category,Days,OtHours,DedHours,NetHours,DailyRate,HourlyRate,ExtraHrs,Advance,Arrears,NetSalary,SavedAt)
                VALUES(@ws,@we,@n,@c,@d,@ot,@ded,@nh,@dr,@hr,@eh,@adv,@arr,@ns,@sa)";
            ins.Parameters.AddWithValue("@ws", weekStart);
            ins.Parameters.AddWithValue("@we", weekEnd);
            ins.Parameters.AddWithValue("@n", r.Name);
            ins.Parameters.AddWithValue("@c", r.Category);
            ins.Parameters.AddWithValue("@d", r.Days);
            ins.Parameters.AddWithValue("@ot", r.OtHours);
            ins.Parameters.AddWithValue("@ded", r.DedHours);
            ins.Parameters.AddWithValue("@nh", r.NetHours);
            ins.Parameters.AddWithValue("@dr", r.DailyRate);
            ins.Parameters.AddWithValue("@hr", r.HourlyRate);
            ins.Parameters.AddWithValue("@eh", r.ExtraHrs);
            ins.Parameters.AddWithValue("@adv", (double)r.Advance);
            ins.Parameters.AddWithValue("@arr", (double)r.Arrears);
            ins.Parameters.AddWithValue("@ns", (double)r.NetSalary);
            ins.Parameters.AddWithValue("@sa", now);
            ins.ExecuteNonQuery();
        }

        using (var sum = conn.CreateCommand())
        {
            sum.CommandText = @"INSERT INTO WeeklySummary
                (WeekStart,WeekEnd,TotalEmployees,TotalPayout,TotalOtHours,TotalDedHours,SavedAt)
                VALUES(@ws,@we,@te,@tp,@to,@td,@sa)";
            sum.Parameters.AddWithValue("@ws", weekStart);
            sum.Parameters.AddWithValue("@we", weekEnd);
            sum.Parameters.AddWithValue("@te", list.Count);
            sum.Parameters.AddWithValue("@tp", list.Sum(r => (double)r.NetSalary));
            sum.Parameters.AddWithValue("@to", list.Sum(r => r.OtHours));
            sum.Parameters.AddWithValue("@td", list.Sum(r => r.DedHours));
            sum.Parameters.AddWithValue("@sa", now);
            sum.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public static List<WeeklySummaryData> GetWeeklySummaries()
    {
        var results = new List<WeeklySummaryData>();
        if (!IsConfigured) return results;
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT WeekStart,WeekEnd,TotalEmployees,TotalPayout,TotalOtHours,TotalDedHours FROM WeeklySummary ORDER BY WeekStart";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            results.Add(new WeeklySummaryData
            {
                WeekStart = r.GetString(0), WeekEnd = r.GetString(1),
                TotalEmployees = r.GetInt32(2), TotalPayout = r.GetDouble(3),
                TotalOtHours = r.GetDouble(4), TotalDedHours = r.GetDouble(5)
            });
        }
        return results;
    }

    public static List<CategoryBreakdown> GetCategoryBreakdown()
    {
        var results = new List<CategoryBreakdown>();
        if (!IsConfigured) return results;
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT WeekStart, Category, SUM(NetSalary) as Total
            FROM SalaryRecord GROUP BY WeekStart, Category ORDER BY WeekStart, Category";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new CategoryBreakdown { WeekStart = r.GetString(0), Category = r.GetString(1), Total = r.GetDouble(2) });
        return results;
    }

    public static List<WorkerOtData> GetWorkerOtTrend(int topN = 5)
    {
        var results = new List<WorkerOtData>();
        if (!IsConfigured) return results;
        using var conn = Open();
        conn.Open();

        // Get top OT workers
        using var top = conn.CreateCommand();
        top.CommandText = $"SELECT Name, SUM(OtHours) as TotalOt FROM SalaryRecord GROUP BY Name ORDER BY TotalOt DESC LIMIT {topN}";
        var names = new List<string>();
        using (var r = top.ExecuteReader())
            while (r.Read()) names.Add(r.GetString(0));

        foreach (var name in names)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WeekStart, OtHours FROM SalaryRecord WHERE Name=@n ORDER BY WeekStart";
            cmd.Parameters.AddWithValue("@n", name);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                results.Add(new WorkerOtData { Name = name, WeekStart = r.GetString(0), OtHours = r.GetDouble(1) });
        }
        return results;
    }


    public static List<SalaryRow> GetPeriodRecords(string weekStart, string weekEnd)
    {
        var results = new List<SalaryRow>();
        if (!IsConfigured) return results;
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name,Category,Days,OtHours,DedHours,NetHours,DailyRate,HourlyRate,ExtraHrs,Advance,Arrears,NetSalary FROM SalaryRecord WHERE WeekStart=@s AND WeekEnd=@e ORDER BY Category,Name";
        cmd.Parameters.AddWithValue("@s", weekStart);
        cmd.Parameters.AddWithValue("@e", weekEnd);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            results.Add(new SalaryRow
            {
                Name = r.GetString(0), Category = r.GetString(1),
                Days = r.GetInt32(2), OtHours = r.GetDouble(3),
                DedHours = r.GetDouble(4), NetHours = r.GetDouble(5),
                DailyRate = r.GetInt32(6), HourlyRate = r.GetDouble(7),
                ExtraHrs = r.GetDouble(8), Advance = (decimal)r.GetDouble(9),
                Arrears = (decimal)r.GetDouble(10), NetSalary = (decimal)r.GetDouble(11)
            });
        }
        return results;
    }

    public static Dictionary<string, double> GetEmployeeAverages(int lastN = 4)
    {
        var results = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);
        if (!IsConfigured) return results;
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Name, AVG(NetSalary) FROM (SELECT Name, NetSalary, WeekStart, ROW_NUMBER() OVER (PARTITION BY Name ORDER BY WeekStart DESC) as rn FROM SalaryRecord) WHERE rn <= {lastN} GROUP BY Name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var name = r.GetString(0);
            var avg = r.GetDouble(1);
            if (!string.IsNullOrEmpty(name) && avg > 0)
                results[name] = avg;
        }
        return results;
    }
    public static int GetRecordCount()
    {
        if (!IsConfigured) return 0;
        using var conn = Open();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WeeklySummary";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

public class WeeklySummaryData
{
    public string WeekStart { get; set; } = "";
    public string WeekEnd { get; set; } = "";
    public int TotalEmployees { get; set; }
    public double TotalPayout { get; set; }
    public double TotalOtHours { get; set; }
    public double TotalDedHours { get; set; }
}

public class CategoryBreakdown
{
    public string WeekStart { get; set; } = "";
    public string Category { get; set; } = "";
    public double Total { get; set; }
}

public class WorkerOtData
{
    public string Name { get; set; } = "";
    public string WeekStart { get; set; } = "";
    public double OtHours { get; set; }
}
