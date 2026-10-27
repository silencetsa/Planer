using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

internal sealed record YearDayPresentation(DateOnly Date, Guid? HabitId, bool Active, bool Marked, string Color);

public sealed partial class PlannerView
{
    private static readonly string[] YearMoodNames = ["Радость", "Хорошее", "Злость", "Грусть", "Нейтральное"];
    private UIElement YearBoard()
    {
        var layout = new Grid { RowSpacing = 10, ColumnSpacing = 12 };
        layout.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new() { Width = new(310) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var filter = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var mood = Ui.ActionButton("Настроение", _vm.YearShowsMood ? "#F3E3FA" : "#FFFFFF", _vm.YearShowsMood ? Ui.Accent : Ui.Muted);
        mood.Name = "YearMoodButton"; mood.Command = _vm.ShowYearMoodCommand; filter.Children.Add(mood);
        filter.Children.Add(Ui.Text("Привычка", 13, Ui.Muted));
        var selector = new ComboBox { Name = "YearHabitSelector", Width = 260, FontFamily = Ui.PlannerFont, FontSize = 14,
            CharacterSpacing = Ui.SmallTextSpacing, Foreground = Ui.Brush(Ui.Muted), Background = Ui.Brush("#FFFFFF"), BorderThickness = new(0), CornerRadius = new(5),
            PlaceholderText = _vm.YearHabits.Count == 0 ? "В этом году привычек нет" : "Выбрать привычку", IsEnabled = _vm.YearHabits.Count > 0 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(selector, "Привычка для календаря года");
        foreach (var habit in _vm.YearHabits)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(Ui.Dot(Palette.HabitColor(habit.Color), true, 11));
            var labels = new StackPanel { Spacing = 2 }; labels.Children.Add(Ui.Text(habit.Name, 14));
            if (habit.Ended is { } end && end <= new DateOnly(_vm.SelectedDate.Year, 12, 31)) labels.Children.Add(Ui.Text($"Завершена {end:dd.MM.yyyy}", 10));
            content.Children.Add(labels); var item = new ComboBoxItem { Content = content, Tag = habit.Id }; selector.Items.Add(item);
            if (_vm.YearHabitId == habit.Id) selector.SelectedItem = item;
        }
        selector.SelectionChanged += (_, _) =>
        {
            if (selector.SelectedItem is ComboBoxItem { Tag: Guid id }) { selector.IsDropDownOpen = false; _vm.SelectYearHabit(id); }
        };
        filter.Children.Add(selector);
        Ui.Add(layout, filter);
        var months = Ui.Grid(3, 4, 10); months.MinHeight = 606; months.Name = "YearMonthGrid";
        var selectedHabit = _vm.SelectedYearHabit;
        for (int month = 1; month <= 12; month++) Ui.Add(months, YearMonth(new(_vm.SelectedDate.Year, month, 1), selectedHabit), (month - 1) % 3, (month - 1) / 3);
        var calendar = Ui.Scroll(months); calendar.Name = "YearCalendar"; calendar.VerticalContentAlignment = VerticalAlignment.Stretch;
        Ui.Add(layout, calendar, row: 1);
        var statistics = YearStatisticsCard(_vm.YearStatistics); statistics.Name = "YearStatisticsCard"; Grid.SetRowSpan(statistics, 2); Ui.Add(layout, statistics, 1);
        return layout;
    }
    private UIElement YearMonth(DateOnly first, Habit? selectedHabit)
    {
        var layout = new Grid { RowSpacing = 4 };
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        string name = PlannerViewModel.Russian.TextInfo.ToTitleCase(first.ToString("MMMM", PlannerViewModel.Russian));
        var title = Ui.Text(name, 14, Ui.Accent, true); title.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(layout, title);
        var weekdays = Ui.Grid(7, gap: 0);
        for (int i = 0; i < 7; i++) { var dayName = Ui.Text(Weekdays[i], 8, Ui.Muted); dayName.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(weekdays, dayName, i); }
        Ui.Add(layout, weekdays, row: 1);
        var days = Ui.Grid(7, 6, 1); var start = Dates.Monday(first);
        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i); if (date.Month != first.Month) continue;
            var dateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var number = Ui.Text(date.Day.ToString(), 9, Ui.Muted); number.Width = 12; number.TextAlignment = TextAlignment.Right; dateRow.Children.Add(number);
            string color; bool active = true, marked; string description;
            if (selectedHabit is null)
            {
                var moodValue = _vm.MoodOn(date); marked = moodValue is not null;
                color = moodValue is { } m ? Palette.MoodColors[(int)m] : "#E8E3E0";
                description = moodValue is { } value ? YearMoodNames[(int)value] : "Настроение не отмечено";
            }
            else
            {
                var habit = _vm.HabitsOn(date).FirstOrDefault(h => h.HabitId == selectedHabit.Id); active = habit is not null; marked = habit?.Completed == true;
                color = habit is null ? "#E8E3E0" : marked ? habit.Color : Palette.PaleHabitColor(habit.Color);
                description = selectedHabit.Name + ": " + (!active ? "не была активна" : marked ? "отмечена" : "не отмечена");
            }
            dateRow.Children.Add(Ui.Dot(color, true, 12));
            var cell = new Border { Name = "YearDayCell", Tag = new YearDayPresentation(date, selectedHabit?.Id, active, marked, color), Child = dateRow,
                CornerRadius = new(3), BorderBrush = Ui.Brush("#FAF03D"), BorderThickness = new(date == _vm.Today ? 1 : 0), Background = Ui.Brush("#00FFFFFF"), Padding = new(1) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cell, date.ToString("d MMMM yyyy", PlannerViewModel.Russian) + ": " + description);
            Ui.Add(days, cell, i % 7, i / 7);
        }
        Ui.Add(layout, days, row: 2);
        var button = Ui.Button(layout, _vm.OpenMonthCommand, first, $"Открыть {name.ToLowerInvariant()} {first.Year}");
        button.Background = Ui.Brush("#FFFFFF"); button.CornerRadius = new(14); button.Padding = new(10); button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalAlignment = VerticalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
        return button;
    }
}
