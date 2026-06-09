#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LuksAttendance;

public static class PdfExporter
{
    static PdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void Export(string path,
        ObservableCollection<AttendanceRow> attendance,
        ObservableCollection<SalaryRow> salary,
        string duration = "")
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);

                page.Header().Row(row =>
                {
                    row.RelativeItem(2).AlignLeft().Height(40).Image("assets/logo_transparent.png");
                    row.RelativeItem(8).AlignCenter().PaddingTop(8)
                        .Text($"LUKS SALARY SHEET — {duration}").FontSize(16).Bold();
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); // Category
                        cols.RelativeColumn(3); // Name
                        cols.RelativeColumn(1); // Days
                        cols.RelativeColumn(1); // OT
                        cols.RelativeColumn(1); // Ded
                        cols.RelativeColumn(1); // Net
                        cols.RelativeColumn(1.5f); // Daily
                        cols.RelativeColumn(1); // Hourly
                        cols.RelativeColumn(1); // Extra
                        cols.RelativeColumn(1.5f); // Advance
                        cols.RelativeColumn(1.5f); // Arrears
                        cols.RelativeColumn(2); // Net Salary
                    });

                    table.Header(h =>
                    {
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Category").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Name").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Days").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("OT").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Ded").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Net Hrs").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Daily Rs").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Hr Rs").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Extra").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Advance").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Arrears").Bold().FontSize(8).FontColor(Colors.White);
                        h.Cell().Border(0.5f).Background("#1a237e").Padding(3).Text("Net Salary").Bold().FontSize(8).FontColor(Colors.White);
                    });

                    var sorted = salary.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
                    foreach (var r in sorted)
                    {
                        table.Cell().Border(0.5f).Padding(3).Text(r.Category).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Name).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Days.ToString()).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.OtHours.ToString("F1")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.DedHours.ToString("F1")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.NetHours.ToString("F1")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.DailyRate.ToString()).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.HourlyRate.ToString("F0")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.ExtraHrs.ToString("F1")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Advance.ToString("F0")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Arrears.ToString("F0")).FontSize(8);
                        table.Cell().Border(0.5f).Padding(3).Text(r.NetSalary.ToString("N0")).FontSize(8).Bold();
                    }

                    // Total
                    for (int i = 0; i < 11; i++)
                        table.Cell().Border(0.5f).Padding(3).Text(i == 1 ? "TOTAL" : "").Bold().FontSize(8);
                    table.Cell().Border(0.5f).Padding(3)
                        .Text(salary.Sum(s => s.NetSalary).ToString("N0")).Bold().FontSize(9);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                });
            });
        }).GeneratePdf(path);
    }

    public static void Print(ObservableCollection<SalaryRow> salary)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LUKS_Print.pdf");
        Export(tempPath, new ObservableCollection<AttendanceRow>(), salary, "");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true,
            Verb = "print"
        });
    }
}
