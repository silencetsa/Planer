using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private static readonly string[] Weekdays = ["ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ", "ВС"];
    private UIElement MonthBoard()
    {
        var layout = Ui.Grid(2, gap: 14); layout.ColumnDefinitions[1].Width = new(240);
        var calendar = new Grid { RowSpacing = 8 }; calendar.RowDefinitions.Add(new() { Height = GridLength.Auto }); calendar.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var weekdays = Ui.Grid(7, gap: 7);
        for (int i = 0; i < 7; i++) { var text = Ui.Text(Weekdays[i], 11, Ui.Muted, true); text.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(weekdays, text, i); }
        Ui.Add(calendar, weekdays);
        var start = Dates.Monday(_vm.First);
        int weeks = (_vm.Last.DayNumber - start.DayNumber) / 7 + 1;
        var days = Ui.Grid(7, weeks, 7);
        for (int i = 0; i < weeks * 7; i++)
        {
            var date = start.AddDays(i); Ui.Add(days, MonthDay(date, date.Month == _vm.SelectedDate.Month), i % 7, i / 7);
        }
        Ui.Add(calendar, days, row: 1); Ui.Add(layout, calendar);
        var side = Ui.Grid(1, 2, 12); side.RowDefinitions[0].Height = new(1.3, GridUnitType.Star);
        Ui.Add(side, BacklogCard()); Ui.Add(side, MatrixCard(), row: 1); Ui.Add(layout, side, 1);
        return layout;
    }
    private UIElement MonthDay(DateOnly date, bool belongsToMonth)
    {
        var layout = new Grid { RowSpacing = 4 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var top = new Grid(); top.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var number = Ui.Button(Ui.Text(date.Day.ToString(), 12, Ui.Muted, true), _vm.OpenWeekCommand, date, $"Открыть неделю: {date:dd.MM.yyyy}"); number.HorizontalAlignment = HorizontalAlignment.Left; number.Padding = new(0); Ui.Add(top, number);
        if (_vm.MoodOn(date) is { } mood) Ui.Add(top, Ui.Dot(Palette.MoodColors[(int)mood], true, 11), 1);
        Ui.Add(layout, top);
        var tasks = new StackPanel { Spacing = 3 };
        foreach (var t in _vm.TasksOn(date).OrderBy(t => t.Part).ThenBy(t => t.Order)) tasks.Children.Add(TaskBlock(t, compact: true));
        Ui.Add(layout, Ui.Scroll(tasks), row: 1); Ui.Add(layout, ReadOnlyHabits(date, 8), row: 2);
        var border = Ui.Card(layout, 11); border.Padding = new(7); border.BorderThickness = new(date == _vm.Today ? 2 : 0); border.BorderBrush = Ui.Brush("#FAF03D");
        if (!belongsToMonth) border.Opacity = .43;
        EnableDrop(border, date);
        border.Tapped += (_, args) =>
        {
            if (DateTime.UtcNow <= _suppressClicksUntil || IsInteractiveSource(args.OriginalSource as DependencyObject, border)) return;
            args.Handled = true; _vm.OpenWeekCommand.Execute(date);
        };
        return border;
    }
    private static bool IsInteractiveSource(DependencyObject? source, DependencyObject boundary)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, boundary); node = VisualTreeHelper.GetParent(node))
            if (node is Button or Microsoft.UI.Xaml.Controls.Primitives.ScrollBar) return true;
        return false;
    }
    private UIElement ReadOnlyHabits(DateOnly date, double size = 7)
    {
        var indicators = _vm.HabitsOn(date); var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center, MinHeight = size + 2 };
        foreach (var h in indicators) { var dot = Ui.Dot(h.Color, h.Completed, size); ToolTipService.SetToolTip(dot, h.Name); row.Children.Add(dot); }
        return row;
    }
}
