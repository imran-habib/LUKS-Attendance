using System.Collections.ObjectModel;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        ObservableCollection<SalaryRow> salary)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.Header().Text("LUKS Salary Sheet").FontSize(18).Bold().AlignCenter();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3); // Name
                        cols.RelativeColumn(1); // Days
                        cols.RelativeColumn(1); // OT
                        cols.RelativeColumn(1); // Ded
                        cols.RelativeColumn(1); // Net
                        cols.RelativeColumn(1); // Daily
                        cols.RelativeColumn(1); // Hourly
                        cols.RelativeColumn(1); // Extra
                        cols.RelativeColumn(1); // Advance
                        cols.RelativeColumn(1); // Arrears
                        cols.RelativeColumn(2); // Net Salary
                    });

                    // Header
                    table.Header(h =>
                    {
                        h.Cell().Border(0.5f).Padding(3).Text("Name").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Days").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("OT").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Ded").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Net Hrs").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Daily Rs").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Hr Rs").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Extra").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Advance").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Arrears").Bold().FontSize(9);
                        h.Cell().Border(0.5f).Padding(3).Text("Net Salary").Bold().FontSize(9);
                    });

                    foreach (var r in salary)
                    {
                        table.Cell().Border(0.5f).Padding(3).Text(r.Name).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Days.ToString()).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.OtHours.ToString("F1")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.DedHours.ToString("F1")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.NetHours.ToString("F1")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.DailyRate.ToString()).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.HourlyRate.ToString("F0")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.ExtraHrs.ToString("F1")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Advance.ToString("F0")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.Arrears.ToString("F0")).FontSize(9);
                        table.Cell().Border(0.5f).Padding(3).Text(r.NetSalary.ToString("N0")).FontSize(9);
                    }

                    // Total
                    table.Cell().Border(0.5f).Padding(3).Text("TOTAL").Bold().FontSize(9);
                    for (int i = 0; i < 9; i++)
                        table.Cell().Border(0.5f).Padding(3).Text("").FontSize(9);
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

        // Generate temp PDF and open for printing
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LUKS_Print.pdf");
        Export(tempPath, new ObservableCollection<AttendanceRow>(), salary);
        
        // Open PDF with default viewer for printing
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true,
            Verb = "print"
        });
    }
}
