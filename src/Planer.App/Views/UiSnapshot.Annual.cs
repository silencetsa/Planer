using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
        string[] outlineIcons = ["GreenPanel.png", "YellowPanelSoft.png", "RedPanel.png", "BluePanel.png", "PurplePanelWarm.png"];
        foreach (var group in moodRow.Children.Cast<StackPanel>())
        {
            int index = Grid.GetColumn(group);
            if (group.Children[0] is not Image icon || icon.Tag is not Ui.OriginalImageLoad image || image.Name != outlineIcons[index]
                || group.Children[1] is not TextBlock count || count.FontSize != 15 || count.FontWeight != Microsoft.UI.Text.FontWeights.Normal
                || count.TransformToVisual(group).TransformPoint(new()).X < icon.ActualWidth
                || group.ActualWidth > moodRow.ColumnDefinitions[index].ActualWidth + 1)
                throw new InvalidOperationException("Настроения должны показывать контурные смайлики и числа справа без перекрытия");
        }
        var finance = Descendants<StackPanel>(panel).Single(s => s.Name == "YearFinanceStatisticsPlaceholder");
        if (Descendants<TextBlock>(finance).Count(t => t.Text == "— / —") != 5) throw new InvalidOperationException("Не зарезервированы пять строк факт/план финансов");
        CheckAnnualCharts(panel, actual);
        var flow = Descendants<Grid>(panel).Single(p => p.Name == "AnnualStatisticsFlow");
        double? previousGap = null;
        for (int i = 1; i < flow.Children.Count; i++)
        {
            var previous = (FrameworkElement)flow.Children[i - 1]; var next = (FrameworkElement)flow.Children[i];
            double gap = next.TransformToVisual(flow).TransformPoint(new()).Y - previous.TransformToVisual(flow).TransformPoint(new(0, previous.ActualHeight)).Y;
            if (gap < 11 || previousGap is { } previousValue && Math.Abs(gap - previousValue) > 1)
                throw new InvalidOperationException("Промежутки между блоками статистики должны быть равными без перекрытия");
            previousGap = gap;
        }
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
        var statisticsScroll = Descendants<ScrollViewer>(panel).Single(s => s.Name == "YearStatisticsScroll");
        if (scenario != "AnnualMinimum")
        {
            double financeBottom = finance.TransformToVisual(statisticsScroll).TransformPoint(new(0, finance.ActualHeight)).Y;
            if (statisticsScroll.ScrollableHeight > 1 || Math.Abs(financeBottom - statisticsScroll.ActualHeight) > 1)
                throw new InvalidOperationException($"Статистика должна помещаться без прокрутки в стандартном окне: контент {flow.ActualHeight}, область {statisticsScroll.ActualHeight}, прокрутка {statisticsScroll.ScrollableHeight}");
        }
        if (scenario == "AnnualMinimum")
        {
            calendar.ChangeView(null, double.MaxValue, null, true); await PrepareAsync(view);
            var december = Descendants<Button>(calendar).Single(b => b.CommandParameter is DateOnly date && date.Month == 12);
            var bottom = december.TransformToVisual(view).TransformPoint(new(0, december.ActualHeight));
            if (bottom.Y > view.ActualHeight + 1) throw new InvalidOperationException("Декабрь недоступен в минимальном окне");
            statisticsScroll.ChangeView(null, double.MaxValue, null, true); await PrepareAsync(view);
            double financeBottom = finance.TransformToVisual(statisticsScroll).TransformPoint(new(0, finance.ActualHeight)).Y;
            if (financeBottom > statisticsScroll.ActualHeight + 1 || financeBottom < 1)
                throw new InvalidOperationException("Итоги финансов недоступны при прокрутке статистики");
            await SaveAsync(view, System.IO.Path.ChangeExtension(path, ".bottom.png"));
            calendar.ChangeView(null, 0, null, true); statisticsScroll.ChangeView(null, 0, null, true); await PrepareAsync(view);
        }
        if (scenario == "AnnualEmpty" && (actual.AddedTasks != 0 || actual.Habits.TopThree.Count != 0)) throw new InvalidOperationException("Пустой год получил выдуманную статистику");
        await SaveAsync(view, path);
    }
    private static void CheckAnnualCharts(Grid panel, AnnualStatistics statistics)
    {
        var pie = Descendants<Canvas>(panel).Single(c => c.Name == "AnnualCompletedPie");
        var sectors = pie.Children.OfType<Shape>().Where(s => s.Tag is AnnualTaskCount).ToArray();
        var labels = pie.Children.OfType<TextBlock>().Where(t => t.Tag is AnnualTaskCount).ToArray();
        if (sectors.Length != statistics.TaskTypes.Count(t => t.Completed > 0) || sectors.Length != labels.Length)
            throw new InvalidOperationException("Диаграмма должна учитывать только выполненные задачи и показывать все ненулевые доли");
        int[] colors = [0, 2, 3, 5];
        foreach (var sector in sectors)
        {
            var type = (AnnualTaskCount)sector.Tag;
            decimal percent = type.Completed * 100m / statistics.CompletedTasks;
            if (type.CompletedPercent != percent || ((SolidColorBrush)sector.Fill).Color != Ui.Brush(Palette.Elements[colors[(int)type.Kind]]).Color)
                throw new InvalidOperationException("Сектор использует неверную базу процента или цвет элементов");
            var label = labels.Single(t => ((AnnualTaskCount)t.Tag).Kind == type.Kind);
            if (label.Text != percent.ToString("0.#", PlannerViewModel.Russian) + "%" || label.FontSize != 13 || label.FontWeight != Microsoft.UI.Text.FontWeights.Normal || Canvas.GetLeft(label) < 0
                || Canvas.GetLeft(label) + label.ActualWidth > pie.ActualWidth + 1 || Canvas.GetTop(label) < 0 || Canvas.GetTop(label) + label.ActualHeight > pie.ActualHeight + 1)
                throw new InvalidOperationException("Подпись доли выполненных задач обрезана или устарела");
            if (type.Completed == statistics.CompletedTasks && sector is not Ellipse) throw new InvalidOperationException("Диаграмма 100% не замкнута в круг");
        }
        for (int i = 0; i < labels.Length; i++)
            for (int j = i + 1; j < labels.Length; j++)
                if (Math.Abs(Canvas.GetLeft(labels[i]) - Canvas.GetLeft(labels[j])) < 60 && Math.Abs(Canvas.GetTop(labels[i]) - Canvas.GetTop(labels[j])) < 18)
                    throw new InvalidOperationException("Подписи редких типов задач перекрываются");
        var top = Descendants<Grid>(panel).Where(p => p.Name == "AnnualTopHabit").ToArray();
        if (top.Length != statistics.Habits.TopThree.Count || Descendants<StackPanel>(panel).Any(p => p.Name == "AnnualHabitBar"))
            throw new InvalidOperationException("Топ привычек должен быть нумерованным списком");
        for (int i = 0; i < top.Length; i++)
        {
            var entry = top[i];
            var habit = (AnnualHabitRecord)entry.Tag;
            if (((TextBlock)entry.Children[0]).Text != $"{i + 1}." || ((TextBlock)entry.Children[1]).Text != habit.Name
                || entry.Children.Cast<TextBlock>().Any(t => t.FontSize != 13 || t.FontWeight != Microsoft.UI.Text.FontWeights.Normal))
                throw new InvalidOperationException("Неверные подписи или размеры нумерованного топа привычек");
        }
        var taskRows = Descendants<Grid>(panel).Single(g => g.Name == "AnnualTaskRows");
        var separators = taskRows.Children.OfType<TextBlock>().Where(t => t.Name == "AnnualTaskSeparator").ToArray();
        if (separators.Length != 5 || separators.Max(t => t.TransformToVisual(taskRows).TransformPoint(new()).X)
            - separators.Min(t => t.TransformToVisual(taskRows).TransformPoint(new()).X) > .5)
            throw new InvalidOperationException("Разделители выполнено/добавлено не выровнены по вертикали");
        foreach (var badge in taskRows.Children.OfType<Border>())
        {
            int row = Grid.GetRow(badge);
            double right = badge.TransformToVisual(taskRows).TransformPoint(new(badge.ActualWidth, 0)).X;
            foreach (var count in taskRows.Children.OfType<TextBlock>().Where(t => Grid.GetRow(t) == row && Grid.GetColumn(t) > 0))
                if (((SolidColorBrush)count.Foreground).Color != Ui.Brush(Ui.Ink).Color || count.FontSize != 13 || count.FontWeight != Microsoft.UI.Text.FontWeights.Normal
                    || count.TransformToVisual(taskRows).TransformPoint(new()).X < right + 2)
                    throw new InvalidOperationException("Серые числа должны находиться справа вне цветной плашки задачи");
        }
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
