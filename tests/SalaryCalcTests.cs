#nullable enable
using System.Collections.Generic;
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

    // ═══ Grace Period — Strict Exclusive (Bug #1: intentional) ═══

    [Fact]
    public void Grace_Haseeb_WithinWindow_NoOT()
    {
        // 8h45 effective (diff=+45min, inside grace window)
        var row = Build("Haseeb", "08:00", "17:45");
        Assert.Equal("", row.OT);
        Assert.Equal("", row.Deduction);
    }

    [Fact]
    public void Grace_Haseeb_ExactBoundary_OTApplies()
    {
        // Grace is STRICT exclusive: at exactly +1h (9h effective), OT applies
        // IN=08:00, OUT=18:00: pres=10h, lunch→9h, round→9h, diff=+60 → NOT in grace
        var row = Build("Haseeb", "08:00", "18:00");
        Assert.Equal("01:00", row.OT);
    }

    [Fact]
    public void NonGrace_SamePunches_GetsOT()
    {
        // Same as Haseeb 8h45 but non-grace → 45min OT
        var row = Build("Irfan Maana", "08:00", "17:45");
        Assert.Equal("00:45", row.OT);
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

    // ═══ Helper ═══
    private static AttendanceRow Build(string name, string inTime, string outTime)
        => SalaryCalc.BuildAttendanceRow(new PunchRecord { Name = name, DayLabel = "01-Jan (Mo)", InTime = inTime, OutTime = outTime });
}
