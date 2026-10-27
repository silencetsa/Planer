using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private Border YearStatisticsCard(AnnualStatistics statistics)
    {
        var card = new Grid { RowSpacing = 12, Name = "YearStatistics", Tag = statistics };
        card.RowDefinitions.Add(new() { Height = GridLength.Auto });
        card.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        card.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var title = Ui.Text("СТАТИСТИКА ГОДА", 15, Ui.Muted, true); title.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(card, title);
        var body = new StackPanel { Spacing = 17 };
        var tasks = new StackPanel { Spacing = 7 }; tasks.Children.Add(Ui.Text("Задачи", 17, Ui.Blue, true));
        tasks.Children.Add(StatisticsRow("Выполнено / добавлено", $"{statistics.CompletedTasks} / {statistics.AddedTasks}", 20));
        string[] typeNames = ["Срочные важные", "Срочные неважные", "Несрочные важные", "Несрочные неважные"];
        foreach (var type in statistics.TaskTypes)
        {
            var row = new Grid { ColumnSpacing = 6 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(row, Ui.Dot(Palette.TaskForegrounds[(int)type.Kind], true, 9));
            Ui.Add(row, Ui.Text(typeNames[(int)type.Kind], 12, Palette.TaskForegrounds[(int)type.Kind]), 1);
            var amount = Ui.Text($"{type.Completed}/{type.Added} · {type.Percent.ToString("0.#", ViewModels.PlannerViewModel.Russian)}%", 12);
            amount.TextAlignment = TextAlignment.Right; Ui.Add(row, amount, 2); tasks.Children.Add(row);
        }
        tasks.Children.Add(Ui.Text("По датам в планере; доли от всех задач года", 10, Ui.Muted)); body.Children.Add(tasks);

        var habits = new StackPanel { Spacing = 6 }; habits.Children.Add(Ui.Text("Привычки", 17, Ui.Blue, true));
        habits.Children.Add(StatisticsRow("Заведено в году", statistics.Habits.Created.ToString()));
        habits.Children.Add(StatisticsRow("Действовали в году", statistics.Habits.InYear.ToString()));
        habits.Children.Add(StatisticsRow("Активные / завершённые", $"{statistics.Habits.Active} / {statistics.Habits.Finished}"));
        var topLabel = Ui.Text("Топ-3 по отметкам", 12, Ui.Muted, true); topLabel.Margin = new(0, 4, 0, 0); habits.Children.Add(topLabel);
        for (int i = 0; i < statistics.Habits.TopThree.Count; i++)
        {
            var habit = statistics.Habits.TopThree[i]; var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(row, Ui.Text($"{i + 1}.", 12)); Ui.Add(row, Ui.Dot(habit.Color, true, 9), 1); Ui.Add(row, Ui.Text(habit.Name, 13), 2);
            Ui.Add(row, Ui.Text(habit.Marks.ToString(), 14, Ui.Ink, true), 3); habits.Children.Add(row);
        }
        if (statistics.Habits.TopThree.Count == 0) habits.Children.Add(Ui.Text("Пока нет отметок", 12));
        var streak = new StackPanel { Spacing = 3 };
        streak.Children.Add(Ui.Text("Самая длинная серия", 12, Ui.Muted, true));
        if (statistics.Habits.LongestStreak is { } record)
        {
            streak.Children.Add(StatisticsRow(record.Name, $"{record.LongestStreak} дн."));
            streak.Children.Add(Ui.Text($"{record.StreakStart:dd.MM} — {record.StreakEnd:dd.MM}", 11));
        }
        else streak.Children.Add(Ui.Text("Пока нет серии", 12));
        habits.Children.Add(new Border { Child = streak, Padding = new(9, 7, 9, 7), CornerRadius = new(5), Background = Ui.Brush(statistics.Habits.LongestStreak is { } best ? Palette.PaleHabitColor(best.Color) : Ui.Paper), Margin = new(0, 4, 0, 0) });
        body.Children.Add(habits);

        var moods = new StackPanel { Spacing = 8 }; moods.Children.Add(Ui.Text("Настроение", 17, Ui.Blue, true));
        var moodRow = Ui.Grid(5, gap: 3); moodRow.Name = "YearMoodStatistics";
        foreach (var item in statistics.Moods)
        {
            var value = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            value.Children.Add(Ui.Dot(Palette.MoodColors[(int)item.Mood], true, 12)); value.Children.Add(Ui.Text(item.Count.ToString(), 14, Ui.Ink, true));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(value, YearMoodNames[(int)item.Mood] + ": " + item.Count);
            Ui.Add(moodRow, value, (int)item.Mood);
        }
        moods.Children.Add(moodRow); body.Children.Add(moods); Ui.Add(card, Ui.Scroll(body), row: 1);

        var finance = new StackPanel { Spacing = 5, Name = "YearFinanceStatisticsPlaceholder" };
        var financeTitle = new Grid(); financeTitle.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); financeTitle.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(financeTitle, Ui.Text("Финансы", 17, Ui.Blue, true)); Ui.Add(financeTitle, Ui.Text("Факт / план", 11), 1); finance.Children.Add(financeTitle);
        foreach (string label in new[] { "Доход", "Расход", "Накопления" }) finance.Children.Add(StatisticsRow(label, "— / —", 13));
        Ui.Add(card, new Border { Child = finance, BorderThickness = new(0, 1, 0, 0), BorderBrush = Ui.Brush(Ui.Paper), Padding = new(0, 10, 0, 0) }, row: 2);
        return Ui.Card(card);
    }
    private static Grid StatisticsRow(string caption, string value, double size = 15)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(row, Ui.Text(caption, 12)); var amount = Ui.Text(value, size, Ui.Ink, true); amount.TextAlignment = TextAlignment.Right; Ui.Add(row, amount, 1); return row;
    }
}
