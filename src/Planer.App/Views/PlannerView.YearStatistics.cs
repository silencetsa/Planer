using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;
using Planer_App.ViewModels;
using Windows.Foundation;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private static readonly string[] AnnualTaskColors =
        [Palette.Elements[0], Palette.Elements[2], Palette.Elements[3], Palette.Elements[5]];

    private Border YearStatisticsCard(AnnualStatistics statistics)
    {
        var card = new Grid { Name = "YearStatistics", Tag = statistics };
        var body = new StackPanel { Name = "AnnualStatisticsFlow", Spacing = 20 };
        body.Children.Add(AnnualTaskStatistics(statistics));
        body.Children.Add(AnnualHabitStatistics(statistics.Habits));
        body.Children.Add(AnnualMoodStatistics(statistics.Moods));
        body.Children.Add(AnnualFinanceStatistics());
        var scroll = Ui.Scroll(body); scroll.Name = "YearStatisticsScroll";
        card.Children.Add(scroll);
        return Ui.Card(card);
    }

    private static StackPanel AnnualTaskStatistics(AnnualStatistics statistics)
    {
        var section = new StackPanel { Spacing = 6 };
        section.Children.Add(Ui.Text($"Выполнено задач {statistics.CompletedTasks} / {statistics.AddedTasks}", 17, Ui.Blue, true));
        section.Children.Add(CompletedTasksPie(statistics));
        var badges = new StackPanel { Spacing = 4 };
        string[] names = ["Срочные важные", "Срочные неважные", "Несрочные важные", "Несрочные неважные"];
        foreach (var type in statistics.TaskTypes)
        {
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            string ink = Palette.TaskForegrounds[(int)type.Kind];
            Ui.Add(row, Ui.Text(names[(int)type.Kind], 14, ink));
            Ui.Add(row, Ui.Text($"{type.Completed} / {type.Added}", 16, ink, true), 1);
            badges.Children.Add(new Border { Name = "AnnualTaskBadge", Tag = type, Child = row,
                Background = Ui.Brush(Palette.TaskBackgrounds[(int)type.Kind]), CornerRadius = new(4), Padding = new(6, 4, 6, 4) });
        }
        section.Children.Add(badges);
        return section;
    }

    private static Canvas CompletedTasksPie(AnnualStatistics statistics)
    {
        const double width = 276, height = 146, cx = width / 2, cy = height / 2, radius = 52;
        var chart = new Canvas { Name = "AnnualCompletedPie", Width = width, Height = height,
            HorizontalAlignment = HorizontalAlignment.Center, Tag = statistics.CompletedTasks };
        var circle = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = Ui.Brush("#E8E3E0") };
        Canvas.SetLeft(circle, cx - radius); Canvas.SetTop(circle, cy - radius); chart.Children.Add(circle);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chart, "Доли типов от выполненных задач");
        if (statistics.CompletedTasks == 0)
        {
            var zero = Ui.Text("0", 20, Ui.Ink, true); zero.Width = radius * 2; zero.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(zero, cx - radius); Canvas.SetTop(zero, cy - 13); chart.Children.Add(zero); return chart;
        }

        double start = -Math.PI / 2;
        var labels = new List<(AnnualTaskCount Type, double X, double Y, bool Right)>();
        foreach (var type in statistics.TaskTypes.Where(t => t.Completed > 0))
        {
            double sweep = (double)(type.CompletedPercent / 100) * Math.PI * 2;
            Shape sector;
            if (type.Completed == statistics.CompletedTasks)
            {
                sector = new Ellipse { Width = radius * 2, Height = radius * 2 };
                Canvas.SetLeft(sector, cx - radius); Canvas.SetTop(sector, cy - radius);
            }
            else
            {
                Point OnCircle(double angle) => new(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                var figure = new PathFigure { StartPoint = new(cx, cy), IsClosed = true, IsFilled = true };
                figure.Segments.Add(new LineSegment { Point = OnCircle(start) });
                figure.Segments.Add(new ArcSegment { Point = OnCircle(start + sweep), Size = new(radius, radius),
                    IsLargeArc = sweep > Math.PI, SweepDirection = SweepDirection.Clockwise });
                var geometry = new PathGeometry(); geometry.Figures.Add(figure);
                sector = new Microsoft.UI.Xaml.Shapes.Path { Data = geometry };
            }
            sector.Fill = Ui.Brush(AnnualTaskColors[(int)type.Kind]); sector.Tag = type; chart.Children.Add(sector);
            double middle = start + sweep / 2, x = Math.Cos(middle), y = Math.Sin(middle);
            labels.Add((type, cx + radius * x, cy + radius * y, x >= 0)); start += sweep;
        }
        // Separate labels on each side so even rare task types remain readable.
        foreach (bool right in new[] { false, true })
        {
            var side = labels.Where(l => l.Right == right).OrderBy(l => l.Y).ToArray();
            var positions = new double[side.Length];
            for (int i = 0; i < side.Length; i++) positions[i] = Math.Max(Math.Clamp(side[i].Y, 13, height - 13), i == 0 ? 13 : positions[i - 1] + 27);
            for (int i = side.Length - 1; i >= 0; i--) positions[i] = Math.Min(positions[i], i == side.Length - 1 ? height - 13 : positions[i + 1] - 27);
            for (int i = 0; i < side.Length; i++)
            {
                var item = side[i]; double endX = right ? cx + radius + 13 : cx - radius - 13;
                chart.Children.Add(new Line { X1 = item.X, Y1 = item.Y, X2 = endX, Y2 = positions[i],
                    Stroke = Ui.Brush(AnnualTaskColors[(int)item.Type.Kind]), StrokeThickness = 1.5 });
                var text = Ui.Text(item.Type.CompletedPercent.ToString("0.#", PlannerViewModel.Russian) + "%", 16, Ui.Ink, true);
                text.Name = "AnnualCompletedPercent"; text.Tag = item.Type; text.Width = 60; text.TextAlignment = right ? TextAlignment.Left : TextAlignment.Right;
                Canvas.SetLeft(text, right ? cx + radius + 17 : cx - radius - 72); Canvas.SetTop(text, positions[i] - 11);
                chart.Children.Add(text);
            }
        }
        return chart;
    }

    private static StackPanel AnnualHabitStatistics(AnnualHabitStatistics statistics)
    {
        var section = new StackPanel { Spacing = 5 };
        section.Children.Add(Ui.Text($"Заведено привычек {statistics.InYear}", 17, Ui.Blue, true));
        section.Children.Add(Ui.Text($"Активно {statistics.Active}", 16));
        section.Children.Add(Ui.Text($"Завершено {statistics.Finished}", 16));
        section.Children.Add(AnnualStatisticsSubtitle("Топ привычек"));
        int maximum = statistics.TopThree.FirstOrDefault()?.Marks ?? 1;
        foreach (var habit in statistics.TopThree)
        {
            var entry = new StackPanel { Name = "AnnualHabitBar", Tag = habit, Spacing = 3 };
            var label = new Grid { ColumnSpacing = 8 };
            label.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); label.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(label, Ui.Text(habit.Name, 16, light: true));
            Ui.Add(label, Ui.Text($"{habit.Marks} {AnnualPlural(habit.Marks, "раз", "раза", "раз")}", 16, Ui.Ink, true), 1);
            entry.Children.Add(label);
            double ratio = (double)habit.Marks / maximum;
            var bar = new Grid { Height = 12 };
            bar.ColumnDefinitions.Add(new() { Width = new(ratio, GridUnitType.Star) }); bar.ColumnDefinitions.Add(new() { Width = new(1 - ratio, GridUnitType.Star) });
            Ui.Add(bar, new Border { Background = Ui.Brush(habit.Color), CornerRadius = new(3) });
            entry.Children.Add(new Border { Background = Ui.Brush(Palette.PaleHabitColor(habit.Color)), CornerRadius = new(3), Child = bar });
            section.Children.Add(entry);
        }
        if (statistics.TopThree.Count == 0) section.Children.Add(Ui.Text("Пока нет отметок", 16));
        section.Children.Add(AnnualStatisticsSubtitle("Самая длинная серия"));
        if (statistics.LongestStreak is { } record)
        {
            var row = new Grid { Name = "AnnualLongestStreak", ColumnSpacing = 7 };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(row, Ui.OriginalIcon("YellowAccent.png", 21)); Ui.Add(row, Ui.Text(record.Name, 16, light: true), 1);
            Ui.Add(row, Ui.Text($"{record.LongestStreak} {AnnualPlural(record.LongestStreak, "день", "дня", "дней")}", 16, Ui.Ink, true), 2);
            section.Children.Add(row);
        }
        else section.Children.Add(Ui.Text("Пока нет серии", 16));
        return section;
    }

    private static TextBlock AnnualStatisticsSubtitle(string text)
    {
        var title = Ui.Text(text, 18, Ui.Ink, true); title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Margin = new(0, 6, 0, 2); return title;
    }
    private static string AnnualPlural(int value, string one, string few, string many) =>
        value % 100 is >= 11 and <= 14 ? many : value % 10 == 1 ? one : value % 10 is >= 2 and <= 4 ? few : many;

    private static StackPanel AnnualMoodStatistics(IReadOnlyList<AnnualMoodCount> statistics)
    {
        var section = new StackPanel { Spacing = 8 }; section.Children.Add(Ui.Text("Настроение", 17, Ui.Blue, true));
        var row = Ui.Grid(5, gap: 4); row.Name = "YearMoodStatistics";
        foreach (var item in statistics)
        {
            var value = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
            value.Children.Add(Ui.MoodIcon(item.Mood, true, 29));
            var count = Ui.Text(item.Count.ToString(), 21, Ui.Ink, true); count.HorizontalAlignment = HorizontalAlignment.Center; value.Children.Add(count);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(value, YearMoodNames[(int)item.Mood] + ": " + item.Count);
            Ui.Add(row, value, (int)item.Mood);
        }
        section.Children.Add(row); return section;
    }

    private static StackPanel AnnualFinanceStatistics()
    {
        var section = new StackPanel { Spacing = 5, Name = "YearFinanceStatisticsPlaceholder" };
        var title = new Grid(); title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(title, Ui.Text("Финансы", 17, Ui.Blue, true)); Ui.Add(title, Ui.Text("Факт / план", 16), 1); section.Children.Add(title);
        section.Children.Add(Ui.Text("Доходы", 16));
        section.Children.Add(AnnualFinanceRow("основные")); section.Children.Add(AnnualFinanceRow("дополнительные"));
        section.Children.Add(Ui.Text("Расходы", 16));
        section.Children.Add(AnnualFinanceRow("регулярные")); section.Children.Add(AnnualFinanceRow("остальные"));
        section.Children.Add(AnnualFinanceRow("Накоплено", 20, true)); return section;
    }
    private static Grid AnnualFinanceRow(string label, double size = 16, bool bold = false)
    {
        var row = new Grid { ColumnSpacing = 8, Margin = new(bold ? 0 : 12, 0, 0, 0) };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(row, Ui.Text(label, size, Ui.Ink, bold)); Ui.Add(row, Ui.Text("— / —", size, Ui.Ink, bold), 1); return row;
    }
}
