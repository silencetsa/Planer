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
        var body = new Grid { Name = "AnnualStatisticsFlow" };
        for (int i = 0; i < 7; i++) body.RowDefinitions.Add(new() {
            Height = i % 2 == 0 ? GridLength.Auto : new(1, GridUnitType.Star), MinHeight = i % 2 == 0 ? 0 : 12 });
        Ui.Add(body, AnnualTaskStatistics(statistics));
        Ui.Add(body, AnnualHabitStatistics(statistics.Habits), row: 2);
        Ui.Add(body, AnnualMoodStatistics(statistics.Moods), row: 4);
        Ui.Add(body, AnnualFinanceStatistics(), row: 6);
        var scroll = Ui.Scroll(body); scroll.Name = "YearStatisticsScroll";
        scroll.VerticalContentAlignment = VerticalAlignment.Stretch;
        card.Children.Add(scroll);
        return Ui.Card(card);
    }

    private static StackPanel AnnualTaskStatistics(AnnualStatistics statistics)
    {
        var section = new StackPanel();
        // Shared numeric columns align every slash, including the heading.
        var rows = new Grid { RowSpacing = 4, ColumnSpacing = 6, Name = "AnnualTaskRows" };
        rows.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        for (int i = 0; i < 3; i++) rows.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        for (int i = 0; i < 6; i++) rows.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Ui.Add(rows, Ui.Text("Выполнено задач", 17, Ui.Blue, true));
        AddCounts(statistics.CompletedTasks, statistics.AddedTasks, 0, 17, true);
        Ui.Add(rows, CompletedTasksPie(statistics), row: 1, span: 4);
        string[] names = ["Срочные важные", "Срочные неважные", "Несрочные важные", "Несрочные неважные"];
        foreach (var type in statistics.TaskTypes)
        {
            string ink = Palette.TaskForegrounds[(int)type.Kind];
            int row = (int)type.Kind + 2;
            Ui.Add(rows, new Border { Name = "AnnualTaskBadge", Tag = type, Child = Ui.Text(names[(int)type.Kind], 13, ink),
                Background = Ui.Brush(Palette.TaskBackgrounds[(int)type.Kind]), CornerRadius = new(4), Padding = new(6, 3, 6, 3) }, row: row);
            AddCounts(type.Completed, type.Added, row, 13, false);
        }
        section.Children.Add(rows);
        return section;

        void AddCounts(int completed, int added, int row, double size, bool bold)
        {
            var done = Ui.Text(completed.ToString(), size, Ui.Ink, bold); done.TextAlignment = TextAlignment.Right;
            var separator = Ui.Text("/", size, Ui.Ink, bold); separator.Name = "AnnualTaskSeparator"; separator.TextAlignment = TextAlignment.Center;
            var total = Ui.Text(added.ToString(), size, Ui.Ink, bold);
            Ui.Add(rows, done, 1, row); Ui.Add(rows, separator, 2, row); Ui.Add(rows, total, 3, row);
        }
    }

    private static Canvas CompletedTasksPie(AnnualStatistics statistics)
    {
        const double width = 276, height = 114, cx = width / 2, cy = height / 2, radius = 46;
        var chart = new Canvas { Name = "AnnualCompletedPie", Width = width, Height = height,
            HorizontalAlignment = HorizontalAlignment.Center, Tag = statistics.CompletedTasks };
        var circle = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = Ui.Brush("#E8E3E0") };
        Canvas.SetLeft(circle, cx - radius); Canvas.SetTop(circle, cy - radius); chart.Children.Add(circle);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chart, "Доли типов от выполненных задач");
        if (statistics.CompletedTasks == 0)
        {
            var zero = Ui.Text("0", 13); zero.Width = radius * 2; zero.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(zero, cx - radius); Canvas.SetTop(zero, cy - 8); chart.Children.Add(zero); return chart;
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
            for (int i = 0; i < side.Length; i++) positions[i] = Math.Max(Math.Clamp(side[i].Y, 9, height - 9), i == 0 ? 9 : positions[i - 1] + 20);
            for (int i = side.Length - 1; i >= 0; i--) positions[i] = Math.Min(positions[i], i == side.Length - 1 ? height - 9 : positions[i + 1] - 20);
            for (int i = 0; i < side.Length; i++)
            {
                var item = side[i]; double endX = right ? cx + radius + 13 : cx - radius - 13;
                chart.Children.Add(new Line { X1 = item.X, Y1 = item.Y, X2 = endX, Y2 = positions[i],
                    Stroke = Ui.Brush(AnnualTaskColors[(int)item.Type.Kind]), StrokeThickness = 1.5 });
                var text = Ui.Text(item.Type.CompletedPercent.ToString("0.#", PlannerViewModel.Russian) + "%", 13);
                text.Name = "AnnualCompletedPercent"; text.Tag = item.Type; text.Width = 60; text.TextAlignment = right ? TextAlignment.Left : TextAlignment.Right;
                Canvas.SetLeft(text, right ? cx + radius + 17 : cx - radius - 72); Canvas.SetTop(text, positions[i] - 8);
                chart.Children.Add(text);
            }
        }
        return chart;
    }

    private static StackPanel AnnualHabitStatistics(AnnualHabitStatistics statistics)
    {
        var section = new StackPanel { Spacing = 3 };
        section.Children.Add(AnnualStatisticsRow("Заведено привычек", statistics.InYear.ToString(), 17, true, Ui.Blue));
        section.Children.Add(AnnualStatisticsRow("Активно", statistics.Active.ToString()));
        section.Children.Add(AnnualStatisticsRow("Завершено", statistics.Finished.ToString()));
        section.Children.Add(AnnualStatisticsSubtitle("Топ привычек"));
        for (int i = 0; i < statistics.TopThree.Count; i++)
        {
            var habit = statistics.TopThree[i];
            var entry = new Grid { Name = "AnnualTopHabit", Tag = habit, ColumnSpacing = 5 };
            entry.ColumnDefinitions.Add(new() { Width = new(17) }); entry.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(entry, Ui.Text($"{i + 1}.", 13)); Ui.Add(entry, Ui.Text(habit.Name, 13), 1);
            Ui.Add(entry, Ui.Text($"{habit.Marks} {AnnualPlural(habit.Marks, "раз", "раза", "раз")}", 13), 2);
            section.Children.Add(entry);
        }
        if (statistics.TopThree.Count == 0) section.Children.Add(Ui.Text("Пока нет отметок", 13));
        section.Children.Add(AnnualStatisticsSubtitle("Самая длинная серия"));
        if (statistics.LongestStreak is { } record)
        {
            var row = new Grid { Name = "AnnualLongestStreak", ColumnSpacing = 7 };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(row, Ui.OriginalIcon("YellowAccent.png", 21)); Ui.Add(row, Ui.Text(record.Name, 13), 1);
            Ui.Add(row, Ui.Text($"{record.LongestStreak} {AnnualPlural(record.LongestStreak, "день", "дня", "дней")}", 13), 2);
            section.Children.Add(row);
        }
        else section.Children.Add(Ui.Text("Пока нет серии", 13));
        return section;
    }

    private static TextBlock AnnualStatisticsSubtitle(string text)
    {
        var title = Ui.Text(text, 15, Ui.Ink, true); title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Margin = new(0, 4, 0, 1); return title;
    }
    private static string AnnualPlural(int value, string one, string few, string many) =>
        value % 100 is >= 11 and <= 14 ? many : value % 10 == 1 ? one : value % 10 is >= 2 and <= 4 ? few : many;

    private static StackPanel AnnualMoodStatistics(IReadOnlyList<AnnualMoodCount> statistics)
    {
        var section = new StackPanel { Spacing = 5 }; section.Children.Add(Ui.Text("Настроение", 17, Ui.Blue, true));
        var row = Ui.Grid(5, gap: 3); row.Name = "YearMoodStatistics";
        foreach (var item in statistics)
        {
            var value = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
            value.Children.Add(Ui.MoodIcon(item.Mood, false, 25));
            value.Children.Add(Ui.Text(item.Count.ToString(), 15));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(value, YearMoodNames[(int)item.Mood] + ": " + item.Count);
            Ui.Add(row, value, (int)item.Mood);
        }
        section.Children.Add(row); return section;
    }

    private static StackPanel AnnualFinanceStatistics()
    {
        var section = new StackPanel { Spacing = 2, Name = "YearFinanceStatisticsPlaceholder" };
        var title = new Grid(); title.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(title, Ui.Text("Финансы", 17, Ui.Blue, true)); Ui.Add(title, Ui.Text("Факт / план", 13), 1); section.Children.Add(title);
        section.Children.Add(Ui.Text("Доходы", 13));
        section.Children.Add(AnnualFinanceRow("основные")); section.Children.Add(AnnualFinanceRow("дополнительные"));
        section.Children.Add(Ui.Text("Расходы", 13));
        section.Children.Add(AnnualFinanceRow("регулярные")); section.Children.Add(AnnualFinanceRow("остальные"));
        section.Children.Add(AnnualFinanceRow("Накоплено", 15, true)); return section;
    }
    private static Grid AnnualFinanceRow(string label, double size = 13, bool bold = false)
    {
        var row = AnnualStatisticsRow(label, "— / —", size, bold);
        row.Margin = new(bold ? 0 : 12, 0, 0, 0);
        if (bold) foreach (var text in row.Children.OfType<TextBlock>()) text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        return row;
    }
    private static Grid AnnualStatisticsRow(string label, string amount, double size = 13, bool bold = false, string labelColor = Ui.Ink)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(row, Ui.Text(label, size, labelColor, bold)); var value = Ui.Text(amount, size, Ui.Ink, bold); value.TextAlignment = TextAlignment.Right;
        Ui.Add(row, value, 1); return row;
    }
}
