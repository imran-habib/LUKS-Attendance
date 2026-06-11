#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace LuksAttendance;

public partial class AnalyticsTab : UserControl
{
    public AnalyticsTab()
    {
        InitializeComponent();
        Loaded += (_, _) => { try { LoadCharts(); } catch { } };
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadCharts();

    private void LoadCharts()
    {
        var summaries = DatabaseService.GetWeeklySummaries();
        int weeks = summaries.Count;
        TxtDbInfo.Text = $"Database: {weeks} weeks stored | {DatabaseService.DbPath}";

        if (weeks == 0)
        {
            TxtDbInfo.Text += " — No data yet. Calculate salary to start building history.";
            return;
        }

        BuildPayrollTrend(summaries);
        BuildCategoryPie();
        BuildOtTrend();
        BuildForecast(summaries);
    }

    private void BuildPayrollTrend(List<WeeklySummaryData> summaries)
    {
        var labels = summaries.Select(s => s.WeekStart.Length > 7 ? s.WeekStart[..7] : s.WeekStart).ToArray();
        var values = summaries.Select(s => s.TotalPayout).ToArray();

        PayrollTrendChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = values,
                Name = "Total Payout (Rs)",
                Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(50)),
                Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 3 },
                GeometrySize = 8
            }
        };

        PayrollTrendChart.XAxes = new Axis[]
        {
            new Axis { Labels = labels, LabelsRotation = 45, TextSize = 10 }
        };
        PayrollTrendChart.YAxes = new Axis[]
        {
            new Axis { Name = "Rs", TextSize = 10 }
        };
    }

    private void BuildCategoryPie()
    {
        var breakdown = DatabaseService.GetCategoryBreakdown();
        if (breakdown.Count == 0) return;

        // Use the latest week
        var latestWeek = breakdown.Last().WeekStart;
        var latest = breakdown.Where(b => b.WeekStart == latestWeek).ToList();

        CategoryPieChart.Series = latest.Select(b => new PieSeries<double>
        {
            Values = new[] { b.Total },
            Name = b.Category
        }).ToArray();
    }

    private void BuildOtTrend()
    {
        var otData = DatabaseService.GetWorkerOtTrend(5);
        if (otData.Count == 0) return;

        var weeks = otData.Select(d => d.WeekStart).Distinct().OrderBy(w => w).ToList();
        var names = otData.Select(d => d.Name).Distinct().ToList();
        var colors = new[] { SKColors.Orange, SKColors.Green, SKColors.Purple, SKColors.Red, SKColors.Teal };

        var series = new List<ISeries>();
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var workerData = otData.Where(d => d.Name == name).GroupBy(d => d.WeekStart).ToDictionary(g => g.Key, g => g.Last().OtHours);
            var values = weeks.Select(w => workerData.GetValueOrDefault(w, 0.0)).ToArray();

            series.Add(new LineSeries<double>
            {
                Values = values,
                Name = name,
                Stroke = new SolidColorPaint(colors[i % colors.Length]) { StrokeThickness = 2 },
                GeometrySize = 6,
                Fill = null
            });
        }

        OtTrendChart.Series = series;
        OtTrendChart.XAxes = new Axis[]
        {
            new Axis { Labels = weeks.Select(w => w.Length > 7 ? w[..7] : w).ToArray(), LabelsRotation = 45, TextSize = 10 }
        };
        OtTrendChart.YAxes = new Axis[]
        {
            new Axis { Name = "OT Hours", TextSize = 10 }
        };
    }

    private void BuildForecast(List<WeeklySummaryData> summaries)
    {
        ForecastPanel.Children.Clear();

        if (summaries.Count < 3)
        {
            ForecastPanel.Children.Add(new TextBlock
            {
                Text = "Need at least 3 weeks of data for forecasting.\nKeep using the software!",
                FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        // Simple linear regression on last 8 weeks (or all if < 8)
        var recent = summaries.TakeLast(Math.Min(8, summaries.Count)).ToList();
        double[] x = Enumerable.Range(0, recent.Count).Select(i => (double)i).ToArray();
        double[] y = recent.Select(s => s.TotalPayout).ToArray();

        double avgX = x.Average();
        double avgY = y.Average();
        double slope = x.Zip(y, (xi, yi) => (xi - avgX) * (yi - avgY)).Sum()
                     / x.Select(xi => (xi - avgX) * (xi - avgX)).Sum();
        double intercept = avgY - slope * avgX;

        // Moving average for smoothing
        double ma = recent.TakeLast(4).Average(s => s.TotalPayout);

        // Forecast next 4 weeks
        int n = recent.Count;
        ForecastPanel.Children.Add(new TextBlock
        {
            Text = "Projected Weekly Payroll:",
            FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10)
        });

        for (int i = 1; i <= 4; i++)
        {
            double predicted = slope * (n + i - 1) + intercept;
            // Blend regression with moving average
            double blended = (predicted + ma) / 2;
            ForecastPanel.Children.Add(new TextBlock
            {
                Text = $"Week +{i}:  Rs {blended:N0}",
                FontSize = 14, Margin = new Thickness(0, 4, 0, 4)
            });
        }

        ForecastPanel.Children.Add(new TextBlock
        {
            Text = $"\nTrend: {(slope > 0 ? "↗️ Increasing" : slope < -100 ? "↘️ Decreasing" : "→ Stable")} " +
                   $"(Rs {slope:+#;-#;0}/week)",
            FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 10, 0, 0)
        });

        // Average info
        ForecastPanel.Children.Add(new TextBlock
        {
            Text = $"Avg payout (last {recent.Count} weeks): Rs {avgY:N0}\n" +
                   $"Avg OT: {recent.Average(s => s.TotalOtHours):F1} hrs/week\n" +
                   $"Avg workers: {recent.Average(s => s.TotalEmployees):F0}/week",
            FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        });
    }
}
