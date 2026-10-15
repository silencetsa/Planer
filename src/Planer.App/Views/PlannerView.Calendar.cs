using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private Flyout PeriodCalendar()
    {
        var body = new StackPanel { Width = 330, Spacing = 10, Background = Ui.Brush(Ui.Paper) };
        var scaledBody = new Viewbox { Child = body, Width = 330 * _scaleHost.ScaleFactor };
        var flyout = new Flyout { Content = scaledBody };
        var style = new Style { TargetType = typeof(FlyoutPresenter) };
        style.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Brush(Ui.Paper)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(12)));
        flyout.FlyoutPresenterStyle = style;
        DateOnly month = new(_vm.SelectedDate.Year, _vm.SelectedDate.Month, 1);

        void Open(PlannerScreen screen, DateOnly date) { flyout.Hide(); _vm.Navigate(screen, date); }
        void Shift(int months)
        {
            try { month = month.AddMonths(months); Draw(); }
            catch (ArgumentOutOfRangeException) { }
        }
        UIElement Heading(string text, string name, int step, Action open)
        {
            var row = Ui.Grid(3, gap: 4); row.ColumnDefinitions[0].Width = GridLength.Auto; row.ColumnDefinitions[2].Width = GridLength.Auto;
            var previous = Ui.Button(Ui.PeriodArrow(false), new RelayCommand(() => Shift(-step)), tooltip: "Назад"); previous.Name = name + "Previous";
            var next = Ui.Button(Ui.PeriodArrow(true), new RelayCommand(() => Shift(step)), tooltip: "Вперёд"); next.Name = name + "Next";
            var title = Ui.Button(Ui.Text(text, name == "CalendarYear" ? 17 : 21, Ui.Accent, true), new RelayCommand(open)); title.Name = name;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            Ui.Add(row, previous); Ui.Add(row, title, 1); Ui.Add(row, next, 2); return row;
        }
        void Draw()
        {
            body.Children.Clear(); var displayed = month;
            body.Children.Add(Heading(month.Year.ToString(), "CalendarYear", 12, () => Open(PlannerScreen.Year, new(displayed.Year, 1, 1))));
            body.Children.Add(Heading(PlannerViewModel.Russian.TextInfo.ToTitleCase(month.ToString("MMMM", PlannerViewModel.Russian)),
                "CalendarMonth", 1, () => Open(PlannerScreen.Month, displayed)));
            var weekdays = Ui.Grid(7, gap: 3);
            for (int i = 0; i < 7; i++)
            {
                var text = Ui.Text(Weekdays[i], 11, Ui.Muted, true); text.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(weekdays, text, i);
            }
            body.Children.Add(weekdays);
            var days = Ui.Grid(7, 6, 3); var first = Dates.Monday(month);
            for (int i = 0; i < 42 && first.DayNumber + i <= DateOnly.MaxValue.DayNumber; i++)
            {
                var date = first.AddDays(i); var button = Ui.Button(Ui.Text(date.Day.ToString(), 14), new RelayCommand(() => Open(PlannerScreen.Week, date)), tooltip: date.ToString("d MMMM yyyy", PlannerViewModel.Russian));
                button.Tag = date; button.Height = 32; button.Padding = new(0); button.HorizontalAlignment = HorizontalAlignment.Stretch;
                if (date == _vm.Today) { button.Background = Ui.Brush("#FDFBD6"); button.BorderBrush = Ui.Brush("#FAF03D"); button.BorderThickness = new(1); }
                else if (date == _vm.SelectedDate) button.Background = Ui.Brush("#F3E3FA");
                if (date.Month != month.Month) button.Opacity = .45;
                Ui.Add(days, button, i % 7, i / 7);
            }
            body.Children.Add(days);
            var today = Ui.Button(Ui.Text("Сегодня", 13, Ui.Blue), new RelayCommand(() => Open(PlannerScreen.Week, _vm.Today)));
            today.HorizontalAlignment = HorizontalAlignment.Center; body.Children.Add(today);
        }
        flyout.Opening += (_, _) => { scaledBody.Width = 330 * _scaleHost.ScaleFactor; month = new(_vm.SelectedDate.Year, _vm.SelectedDate.Month, 1); Draw(); };
        return flyout;
    }
}
