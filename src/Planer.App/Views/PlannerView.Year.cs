using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private UIElement YearBoard()
    {
        var layout = new Grid { RowSpacing = 10 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var filter = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        filter.Children.Add(Ui.Text("Показывать", 12, Ui.Muted));
        var mode = new ComboBox { ItemsSource = new[] { "Общий", "Настроение", "Привычки" }, SelectedIndex = _vm.YearMode, MinWidth = 170, FontSize = 12 };
        mode.SelectionChanged += (_, _) => { _vm.YearMode = mode.SelectedIndex; _vm.Refresh(); }; filter.Children.Add(mode);
        Ui.Add(layout, filter);
        var months = Ui.Grid(4, 3, 10);
        for (int month = 1; month <= 12; month++) Ui.Add(months, YearMonth(new(_vm.SelectedDate.Year, month, 1)), (month - 1) % 4, (month - 1) / 4);
        Ui.Add(layout, months, row: 1); return layout;
    }
    private UIElement YearMonth(DateOnly first)
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
            var indicators = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (_vm.YearMode != 2)
            {
                var mood = _vm.MoodOn(date); indicators.Children.Add(Ui.Dot(mood is { } m ? Palette.MoodColors[(int)m] : "#E8E3E0", mood is not null, 12));
            }
            if (_vm.YearMode != 1) indicators.Children.Add(ReadOnlyHabits(date, 5));
            var cell = new Border { Child = indicators, CornerRadius = new(3), BorderBrush = Ui.Brush("#FAF03D"), BorderThickness = new(date == _vm.Today ? 1 : 0), Background = Ui.Brush("#00FFFFFF"), Padding = new(1) };
            ToolTipService.SetToolTip(cell, date.ToString("dddd, d MMMM yyyy", PlannerViewModel.Russian));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cell, date.ToString("d MMMM yyyy", PlannerViewModel.Russian));
            Ui.Add(days, cell, i % 7, i / 7);
        }
        Ui.Add(layout, days, row: 2);
        var button = Ui.Button(layout, _vm.OpenMonthCommand, first, $"Открыть {name.ToLowerInvariant()} {first.Year}");
        button.Background = Ui.Brush("#FFFFFF"); button.CornerRadius = new(14); button.Padding = new(10); button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalAlignment = VerticalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
        return button;
    }
}
