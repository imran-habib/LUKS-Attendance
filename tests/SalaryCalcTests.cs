#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using LuksAttendance;

namespace LuksAttendance.Tests;

public class SalaryCalcTests
{
    // ═══ OT/Deduction Basics ═══

    [Fact]
    public void Standard8hDay_NoOtNoDeduction()
    {
        var row = Build("Test", "08:00", "17:00");
        Assert.Equal("08:00", row.Worked);
        Assert.Equal("", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void OvertimeDay_2hOT()
    {
        var row = Build("Test", "08:00", "19:00");
        Assert.Equal("02:00", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void ShortDay_2hDeduction()
    {
        var row = Build("Test", "08:00", "15:00");
        Assert.Equal("02:00", row.Deduction);
        Assert.Equal("", row.OT);
    }

    // ═══ 15-Minute Rounding (Bug #4: lunch first, then round) ═══

    [Fact]
    public void Rounding_9h07m_NoOT()
    {
        // presence=9h07m, lunch→8h07m, round→8h00m → 0 OT
        var row = Build("Test", "08:00", "17:07");
        Assert.Equal("", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void Rounding_9h08m_15minOT()
    {
        // presence=9h08m, lunch→8h08m, round→8h15m → 15min OT
        var row = Build("Test", "08:00", "17:08");
        Assert.Equal("00:15", row.OT);
    }

    // ═══ Lunch Deduction Boundary ═══

    [Fact]
    public void LunchDeducted_OutAt1300()
    {
        // presence=5h, lunch→4h, round→4h → 4h deduction
        var row = Build("Test", "08:00", "13:00");
        Assert.Equal("04:00", row.Worked);
    }

    [Fact]
    public void NoLunch_OutAt1259()
    {
        // presence=4h59m, no lunch, round→5h00m
        var row = Build("Test", "08:00", "12:59");
        Assert.Equal("05:00", row.Worked);
    }

    // ═══ Cross-Midnight (Bug #3: lunch only if ≥5h) ═══

    [Fact]
    public void CrossMidnight_ShortShift_NoLunch()
    {
        // IN=23:00, OUT=01:30+1 → presence=2h30m → no lunch (< 5h) → round→2h30m
        var row = Build("Test", "23:00", "01:30+1");
        Assert.Equal("02:30", row.Worked);
    }

    [Fact]
    public void CrossMidnight_LongShift_LunchDeducted()
    {
        // IN=08:10, OUT=01:06+1 → presence=16h56m → lunch (≥5h) → 15h56m → round→16h00m
        var row = Build("Test", "08:10", "01:06+1");
        Assert.Equal("16:00", row.Worked);
        Assert.Equal("08:00", row.OT);
    }

    // ═══ Grace Period — Now ONLY from isGrace flag, not hardcoded names ═══

    [Fact]
    public void Grace_FlagEnabled_WithinWindow_NoOT()
    {
        // 8h45 effective (diff=+45min, inside grace window), isGrace=true
        var row = SalaryCalc.BuildAttendanceRow(
            new PunchRecord { Name = "AnyWorker", DayLabel = "01-Jan (Mo)", InTime = "08:00", OutTime = "17:45" },
            isGrace: true);
        Assert.Equal("", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void Grace_FlagEnabled_ExactBoundary_OTApplies()
    {
        // Grace is STRICT exclusive: at exactly +1h (9h effective), OT applies
        var row = SalaryCalc.BuildAttendanceRow(
            new PunchRecord { Name = "AnyWorker", DayLabel = "01-Jan (Mo)", InTime = "08:00", OutTime = "18:00" },
            isGrace: true);
        Assert.Equal("01:00", row.OT);
    }

    [Fact]
    public void Grace_FlagDisabled_SamePunches_GetsOT()
    {
        // Same punch times but isGrace=false → 45min OT
        var row = SalaryCalc.BuildAttendanceRow(
            new PunchRecord { Name = "AnyWorker", DayLabel = "01-Jan (Mo)", InTime = "08:00", OutTime = "17:45" },
            isGrace: false);
        Assert.Equal("00:45", row.OT);
    }

    [Fact]
    public void Grace_HardcodedName_NoLongerAutoGrace()
    {
        // "Haseeb" was previously hardcoded — now without isGrace=true, gets OT
        var row = Build("Haseeb", "08:00", "17:45");
        Assert.Equal("00:45", row.OT); // NOT suppressed
    }

    // ═══ Bug #14: +1 display hidden ═══

    [Fact]
    public void CrossMidnight_DisplayHidesPlus1()
    {
        var row = Build("Test", "20:00", "02:00+1");
        Assert.Contains("next day", row.OutTime);
        Assert.DoesNotContain("+1", row.OutTime);
    }

    // ═══ Salary Formula ═══

    [Fact]
    public void SalaryFormula_WeeklyWorker()
    {
        var rows = new List<AttendanceRow>
        {
            new() { Name = "test", Day = "d1", Worked = "08:00" },
            new() { Name = "test", Day = "d2", Worked = "09:00", OT = "01:00" },
            new() { Name = "test", Day = "d3", Worked = "08:00" },
        };
        var sal = SalaryCalc.Calculate("test worker", rows, 1600, 500, 200, "Polishing");
        Assert.Equal(3, sal.Days);
        Assert.Equal(1.0, sal.OtHours);
        // Net = 3*1600 + 1*200 - 500 + 200 = 4800 + 200 - 500 + 200 = 4700
        Assert.Equal(4700m, sal.NetSalary);
    }

    [Fact]
    public void SalaryFormula_MonthlyWorker()
    {
        var rows = new List<AttendanceRow>
        {
            new() { Name = "helper", Day = "d1", Worked = "present" },
            new() { Name = "helper", Day = "d2", Worked = "present" },
            new() { Name = "helper", Day = "d3", Worked = "present" },
        };
        var sal = SalaryCalc.Calculate("helper", rows, 1000, 0, 0, "Helper", isMonthly: true);
        Assert.Equal(3, sal.Days);
        Assert.Equal(3000m, sal.NetSalary);
        Assert.Equal(0.0, sal.OtHours);
    }

    // ═══ Edge Cases ═══

    [Fact]
    public void EmptyPunchRecord_ReturnsEmptyRow()
    {
        var row = SalaryCalc.BuildAttendanceRow(new PunchRecord { Name = "Test", DayLabel = "01-Jan (Mo)", InTime = "", OutTime = "" });
        Assert.Equal("", row.Worked);
        Assert.Equal("", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void ZeroDaysWorked_ZeroSalary()
    {
        var rows = new List<AttendanceRow>
        {
            new() { Name = "test", Day = "d1", Worked = "" }, // no worked time
        };
        var sal = SalaryCalc.Calculate("test", rows, 1500, 0, 0, "Worker");
        Assert.Equal(0, sal.Days);
        Assert.Equal(0m, sal.NetSalary);
    }

    // ═══ TimeHelper Validation ═══

    [Fact]
    public void TimeHelper_ValidTimes()
    {
        Assert.True(TimeHelper.IsValidTime("08:00"));
        Assert.True(TimeHelper.IsValidTime("23:59"));
        Assert.True(TimeHelper.IsValidTime("00:00"));
    }

    [Fact]
    public void TimeHelper_InvalidTimes()
    {
        Assert.False(TimeHelper.IsValidTime(""));
        Assert.False(TimeHelper.IsValidTime("25:00"));
        Assert.False(TimeHelper.IsValidTime("12:60"));
        Assert.False(TimeHelper.IsValidTime("8:00")); // too short
        Assert.False(TimeHelper.IsValidTime("abc"));
        Assert.False(TimeHelper.IsValidTime(null!));
    }

    // ═══ PunchParser Tests ═══

    [Fact]
    public void PunchParser_TwoPunches_CreatesRecord()
    {
        var data = MakeAttendanceData(new Dictionary<string, string>
        {
            { "02-Jan (Tu)", "08:00 17:00" }
        }, "01-Jan (Mo)", "02-Jan (Tu)");

        var (records, issues) = PunchParser.Parse(data);
        Assert.Single(records);
        Assert.Equal("08:00", records[0].InTime);
        Assert.Equal("17:00", records[0].OutTime);
        Assert.Empty(issues);
    }

    [Fact]
    public void PunchParser_SinglePunch_CreatesIssue()
    {
        var data = MakeAttendanceData(new Dictionary<string, string>
        {
            { "02-Jan (Tu)", "08:00" }
        }, "01-Jan (Mo)", "02-Jan (Tu)");

        var (records, issues) = PunchParser.Parse(data);
        Assert.Empty(records);
        Assert.Single(issues);
        Assert.Equal("Last Day OUT", issues[0].Type);
    }

    [Fact]
    public void PunchParser_MultiplePunches_UsesFirstAndLast()
    {
        var data = MakeAttendanceData(new Dictionary<string, string>
        {
            { "02-Jan (Tu)", "08:00 12:30 13:30 17:45" }
        }, "01-Jan (Mo)", "02-Jan (Tu)");

        var (records, issues) = PunchParser.Parse(data);
        Assert.Single(records);
        Assert.Equal("08:00", records[0].InTime);
        Assert.Equal("17:45", records[0].OutTime);
    }

    [Fact]
    public void PunchParser_MidnightExit_PairWithPreviousDay()
    {
        var data = new AttendanceData
        {
            Duration = "2026/01/01 ~ 01/02",
            Days = new List<DayInfo>
            {
                new() { Col = 3, DayNum = 1, DayName = "We", DateLabel = "01-Jan (We)" },
                new() { Col = 4, DayNum = 2, DayName = "Th", DateLabel = "02-Jan (Th)" }
            },
            Employees = new List<EmployeeData>
            {
                new()
                {
                    No = "1", Name = "Worker1",
                    Punches = new Dictionary<string, string>
                    {
                        { "01-Jan (We)", "20:00" },
                        { "02-Jan (Th)", "01:30 08:00 17:00" }
                    }
                }
            }
        };

        var (records, issues) = PunchParser.Parse(data);
        // Day 1: single punch + midnight exit from day 2 = cross-midnight record
        var day1Rec = records.FirstOrDefault(r => r.DayLabel == "01-Jan (We)");
        Assert.NotNull(day1Rec);
        Assert.Equal("20:00", day1Rec.InTime);
        Assert.EndsWith("+1", day1Rec.OutTime);
    }

    [Fact]
    public void PunchParser_NoPunches_SkipsDay()
    {
        var data = MakeAttendanceData(new Dictionary<string, string>(), "01-Jan (Mo)", "02-Jan (Tu)");
        var (records, issues) = PunchParser.Parse(data);
        Assert.Empty(records);
        Assert.Empty(issues);
    }

    // ═══ OT-Exempt Tests ═══

    [Fact]
    public void OtExempt_NetHoursZero_SalaryUnaffected()
    {
        // Worker with 2h OT but category is OT-exempt → NetHours=0, pay = days × rate only
        var rows = new List<AttendanceRow>
        {
            new() { Name = "test", Day = "d1", Worked = "10:00", OT = "02:00" },
            new() { Name = "test", Day = "d2", Worked = "08:00" },
        };
        var sal = SalaryCalc.Calculate("test carpenter", rows, 1500, 0, 0, "Carpenter", isOtExempt: true);
        Assert.Equal(2, sal.Days);
        Assert.Equal(2.0, sal.OtHours);      // OT hours still tracked
        Assert.Equal(0.0, sal.NetHours);      // But NetHours = 0 (exempt)
        Assert.Equal(3000m, sal.NetSalary);   // 2 × 1500 = 3000 (no OT pay)
    }

    [Fact]
    public void OtExempt_DeductionIgnored()
    {
        // Worker with 2h deduction but OT-exempt → no deduction from pay
        var rows = new List<AttendanceRow>
        {
            new() { Name = "test", Day = "d1", Worked = "06:00", Deduction = "02:00" },
        };
        var sal = SalaryCalc.Calculate("test carpenter", rows, 1500, 0, 0, "Carpenter", isOtExempt: true);
        Assert.Equal(1, sal.Days);
        Assert.Equal(2.0, sal.DedHours);      // Deduction hours still tracked
        Assert.Equal(0.0, sal.NetHours);      // But NetHours = 0
        Assert.Equal(1500m, sal.NetSalary);   // 1 × 1500 (no deduction from pay)
    }

    [Fact]
    public void NonExempt_OtStillApplied()
    {
        // Normal worker (not exempt) → OT affects pay
        var rows = new List<AttendanceRow>
        {
            new() { Name = "test", Day = "d1", Worked = "10:00", OT = "02:00" },
        };
        var sal = SalaryCalc.Calculate("test worker", rows, 1600, 0, 0, "Worker", isOtExempt: false);
        Assert.Equal(1, sal.Days);
        Assert.Equal(2.0, sal.NetHours);      // NetHours = OT - Ded = 2
        // 1×1600 + 2×200 = 2000
        Assert.Equal(2000m, sal.NetSalary);
    }

    // ═══ Helper ═══

    private static AttendanceRow Build(string name, string inTime, string outTime)
        => SalaryCalc.BuildAttendanceRow(new PunchRecord { Name = name, DayLabel = "01-Jan (Mo)", InTime = inTime, OutTime = outTime });

    private static AttendanceData MakeAttendanceData(Dictionary<string, string> punches, params string[] dayLabels)
    {
        var days = new List<DayInfo>();
        for (int i = 0; i < dayLabels.Length; i++)
            days.Add(new DayInfo { Col = i + 3, DayNum = i + 1, DayName = dayLabels[i].Split('(')[1].TrimEnd(')'), DateLabel = dayLabels[i] });

        return new AttendanceData
        {
            Duration = "2026/01/01 ~ 01/07",
            Days = days,
            Employees = new List<EmployeeData>
            {
                new() { No = "1", Name = "TestWorker", Punches = punches }
            }
        };
    }
}
