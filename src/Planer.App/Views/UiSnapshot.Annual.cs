using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

internal static partial class UiSnapshot
{
    private static async Task SaveAnnualScenarioAsync(FrameworkElement view, string path, string scenario, PlannerViewModel vm)
    {
        if (scenario == "AnnualMinimum") App.Window.AppWindow.Resize(WindowSizing.Minimum(App.Window, floating: false));
        if (scenario == "AnnualPreviousYear") vm.Navigate(PlannerScreen.Year, new(vm.Today.Year - 1, 6, 1));
        if (scenario == "AnnualLeapYear") vm.Navigate(PlannerScreen.Year, new(2024, 6, 1));
        if (scenario == "AnnualHabit") vm.SelectYearHabit(vm.YearHabits.First(h => h.Ended is null).Id);
        if (scenario == "AnnualEndedHabit") vm.SelectYearHabit(vm.YearHabits.First(h => h.Ended is not null).Id);
        if (scenario == "AnnualInteractions") { await CheckAnnualInteractionsAsync(view, vm); }
        await PrepareAsync(view);
        var panel = Descendants<Grid>(view).Single(g => g.Name == "YearStatistics");
        var expected = vm.YearStatistics;
        if (panel.Tag is not AnnualStatistics actual || JsonSerializer.Serialize(actual) != JsonSerializer.Serialize(expected))
            throw new InvalidOperationException("Годовая статистика показывает устаревшие данные");
        var moodRow = Descendants<Grid>(panel).Single(g => g.Name == "YearMoodStatistics");
        if (moodRow.Children.Count != 5 || Descendants<TextBlock>(moodRow).Any(text => !int.TryParse(text.Text, out _)))
            throw new InvalidOperationException("Статистика настроения должна содержать только пять цветных индикаторов и числа");
        var finance = Descendants<StackPanel>(panel).Single(s => s.Name == "YearFinanceStatisticsPlaceholder");
        if (Descendants<TextBlock>(finance).Count(t => t.Text == "— / —") != 3) throw new InvalidOperationException("Не зарезервированы три строки факт/план финансов");
        var cells = Descendants<Border>(view).Where(b => b.Tag is YearDayPresentation).ToArray();
        if (cells.Length != (DateTime.IsLeapYear(vm.SelectedDate.Year) ? 366 : 365)) throw new InvalidOperationException("Годовой календарь потерял дни");
        foreach (var cell in cells)
        {
            var data = (YearDayPresentation)cell.Tag;
            if (data.HabitId != vm.YearHabitId) throw new InvalidOperationException("Календарь смешал настроение и разные привычки");
            if (data.HabitId is { } habitId)
            {
                var indicator = vm.HabitsOn(data.Date).FirstOrDefault(h => h.HabitId == habitId);
                string color = indicator is null ? "#E8E3E0" : indicator.Completed ? indicator.Color : Palette.PaleHabitColor(indicator.Color);
                if (data.Color != color || data.Active != (indicator is not null)) throw new InvalidOperationException("Неверный цвет или период активности выбранной привычки");
            }
        }
        var sidebar = Descendants<Border>(view).Single(b => b.Name == "YearStatisticsCard");
        var calendar = Descendants<ScrollViewer>(view).Single(s => s.Name == "YearCalendar");
        double sidebarLeft = sidebar.TransformToVisual(view).TransformPoint(new()).X;
        double calendarRight = calendar.TransformToVisual(view).TransformPoint(new(calendar.ActualWidth, 0)).X;
        if (calendarRight > sidebarLeft - 2) throw new InvalidOperationException("Годовой календарь перекрывает статистику");
        if (scenario == "AnnualMinimum")
        {
            calendar.ChangeView(null, double.MaxValue, null, true); await PrepareAsync(view);
            var december = Descendants<Button>(calendar).Single(b => b.CommandParameter is DateOnly date && date.Month == 12);
            var bottom = december.TransformToVisual(view).TransformPoint(new(0, december.ActualHeight));
            if (bottom.Y > view.ActualHeight + 1) throw new InvalidOperationException("Декабрь недоступен в минимальном окне");
            await SaveAsync(view, System.IO.Path.ChangeExtension(path, ".bottom.png"));
            calendar.ChangeView(null, 0, null, true); await PrepareAsync(view);
        }
        if (scenario == "AnnualEmpty" && (actual.AddedTasks != 0 || actual.Habits.TopThree.Count != 0)) throw new InvalidOperationException("Пустой год получил выдуманную статистику");
        await SaveAsync(view, path);
    }
    private static async Task CheckAnnualInteractionsAsync(FrameworkElement view, PlannerViewModel vm)
    {
        string statistics = JsonSerializer.Serialize(vm.YearStatistics);
        var selector = Descendants<ComboBox>(view).Single(c => c.Name == "YearHabitSelector");
        if (selector.Items.Count != vm.YearHabits.Count || selector.Items.Cast<ComboBoxItem>().Any(i => i.Tag is not Guid))
            throw new InvalidOperationException("Список выбора привычки не соответствует выбранному году");
        var ended = vm.YearHabits.First(h => h.Ended is not null);
        selector.SelectedItem = selector.Items.Cast<ComboBoxItem>().Single(i => (Guid)i.Tag == ended.Id); await PrepareAsync(view);
        if (vm.YearHabitId != ended.Id || vm.YearShowsMood) throw new InvalidOperationException("Селектор не переключил календарь на завершённую привычку");
        if (JsonSerializer.Serialize(vm.YearStatistics) != statistics) throw new InvalidOperationException("Фильтр календаря изменил итоговую статистику года");
        var mood = Descendants<Button>(view).Single(b => b.Name == "YearMoodButton");
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(mood)
            ?? new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(mood);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        await PrepareAsync(view);
        if (!vm.YearShowsMood || vm.YearHabitId is not null) throw new InvalidOperationException("Кнопка настроения не сбросила выбор привычки");
        vm.SelectYearHabit(ended.Id); vm.Navigate(PlannerScreen.Year, new(vm.Today.Year + 1, 6, 1)); await PrepareAsync(view);
        if (!vm.YearShowsMood || vm.YearHabits.Any(h => h.Id == ended.Id)) throw new InvalidOperationException("Привычка появилась в году после завершения");
        vm.Navigate(PlannerScreen.Year, vm.Today); await PrepareAsync(view);
        var firstMonth = Descendants<Button>(view).First(b => b.CommandParameter is DateOnly date && date.Month == 1);
        firstMonth.Command.Execute(firstMonth.CommandParameter); await PrepareAsync(view);
        if (vm.Screen != PlannerScreen.Month || vm.SelectedDate.Month != 1) throw new InvalidOperationException("Годовой календарь не открывает месяц");
        vm.Navigate(PlannerScreen.Year, vm.Today); await PrepareAsync(view);
    }
}
