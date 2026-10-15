using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Controls;
using Planer_App.ViewModels;
using Planer.Core;

namespace Planer_App.Views;

/// <summary>Opt-in local rendering for QA; requires an explicit alternate data directory.</summary>
internal static class UiSnapshot
{
    public static async Task SaveScenarioAsync(FrameworkElement view, string path, string scenario, PlannerViewModel vm)
    {
        await PrepareAsync(view);
        if (scenario == "RecurringMonth")
        {
            var request = new TaskEditRequest(null, "Еженедельная проверка планов", true, false, new(null));
            vm.SaveTask(request, request.Description, true, false, Repeat.Weekly);
            var pending = vm.Backlog.First(t => t.Description == request.Description);
            vm.MoveTaskCommand.Execute(new TaskMove(pending.Id, vm.Today, DayPart.Afternoon));
            vm.Navigate(PlannerScreen.Month, vm.Today.AddMonths(1));
            if (vm.Matrix.Scheduled.Sum() < 4 || vm.Backlog.Any(t => t.Id == pending.Id)) throw new InvalidOperationException("Будущие повторы не появились в месяце");
            await SaveAsync(view, path); return;
        }
        if (scenario == "TrayLifecycle")
        {
            var tray = App.Tray ?? throw new InvalidOperationException("Иконка области уведомлений не создана");
            if (!tray.IsRegistered) throw new InvalidOperationException("Shell не вернула координаты иконки");
            tray.Invoke(TrayAction.OpenPlanner);
            if (!App.Window.AppWindow.IsVisible) throw new InvalidOperationException("Меню не открыло главный планер");
            tray.Invoke(TrayAction.OpenToday); await Task.Delay(100); var first = App.FloatingWindow;
            tray.Invoke(TrayAction.OpenToday); await Task.Delay(100);
            if (first is null || !ReferenceEquals(first, App.FloatingWindow)
                || first.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter { IsAlwaysOnTop: true })
                throw new InvalidOperationException("Меню не сохранило единственное окно текущего дня поверх окон");
            first.Close(); tray.Dispose(); await Task.Delay(100);
            if (tray.IsRegistered) throw new InvalidOperationException("Иконка осталась в Shell после удаления");
            await SaveAsync(view, path); return;
        }
        if (scenario.StartsWith("Calendar", StringComparison.Ordinal))
        {
            var period = Descendants<Button>(view).First(b => b.Name == "PeriodButton"); var calendar = (Flyout)period.ContextFlyout;
            calendar.ShowAt(period); await Task.Delay(100); var calendarBody = (FrameworkElement)calendar.Content; await PrepareAsync(calendarBody);
            if (scenario == "Calendar") { await SaveAsync(calendarBody, path); return; }
            var selected = vm.SelectedDate;
            if (scenario == "CalendarNavigate")
            {
                Descendants<Button>(calendarBody).First(b => b.Name == "CalendarYearNext").Command.Execute(null);
                await PrepareAsync(calendarBody);
                Descendants<Button>(calendarBody).First(b => b.Name == "CalendarMonthNext").Command.Execute(null);
                await PrepareAsync(calendarBody);
                await SaveAsync(calendarBody, path); return;
            }
            var link = scenario switch
            {
                "CalendarOpenYear" => Descendants<Button>(calendarBody).First(b => b.Name == "CalendarYear"),
                "CalendarOpenMonth" => Descendants<Button>(calendarBody).First(b => b.Name == "CalendarMonth"),
                "CalendarOpenWeek" => Descendants<Button>(calendarBody).First(b => b.Tag is DateOnly d && d.Month == selected.Month && d.Day == 20),
                _ => throw new ArgumentException("Неизвестный сценарий календаря")
            };
            link.Command.Execute(null);
            var expected = scenario == "CalendarOpenYear" ? PlannerScreen.Year : scenario == "CalendarOpenMonth" ? PlannerScreen.Month : PlannerScreen.Week;
            if (vm.Screen != expected || (expected == PlannerScreen.Week && vm.SelectedDate.Day != 20)) throw new InvalidOperationException("Неверный переход календаря");
            await SaveAsync(view, path); return;
        }
        if (scenario == "Notes")
        {
            vm.Notes = "Оставить время для прогулки и отдыха.\nВажные мысли этой недели."; vm.ToggleNotesCommand.Execute(null); await PrepareAsync(view);
            var notes = Descendants<TextBox>(view).First(b => b.AcceptsReturn);
            if (NormalizeLines(notes.Text) != NormalizeLines(vm.Notes)) throw new InvalidOperationException("Заметки потеряли строки при открытии");
            notes.Text += "\rЕщё одна мысль.";
            await PrepareAsync(view);
            if (!NormalizeLines(vm.Notes).Contains("Важные мысли этой недели.\nЕщё одна мысль.")) throw new InvalidOperationException("Правка заметок потеряла предыдущие строки");
            await SaveAsync(view, path); return;
        }
        if (scenario is "PaymentNormal" or "PaymentHover" or "PaymentDone")
        {
            var paymentButton = Descendants<Button>(view).First(b => b.Tag is PaymentOccurrence);
            if (scenario == "PaymentDone")
            {
                var payment = (PaymentOccurrence)paymentButton.Tag;
                vm.PaymentCommand.Execute(new PaymentToggle(payment.PaymentId, payment.Date)); await PrepareAsync(view);
                paymentButton = Descendants<Button>(view).First(b => b.Tag is PaymentOccurrence p && p.PaymentId == payment.PaymentId && p.Date == payment.Date);
            }
            else if (scenario == "PaymentHover") VisualStateManager.GoToState(paymentButton, "PointerOver", false);
            await SaveAsync(paymentButton, path); return;
        }
        if (scenario == "DayPlus")
        {
            var name = $"Создать задачу: {vm.Today:dd.MM}, утро";
            var plus = Descendants<Button>(view).First(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(b) == name);
            var payment = Descendants<Button>(view).FirstOrDefault(b => b.Tag is PaymentOccurrence p && p.Date == vm.Today);
            var before = payment?.TransformToVisual(view).TransformPoint(new Windows.Foundation.Point());
            plus.Visibility = Visibility.Visible; await PrepareAsync(view);
            var after = payment?.TransformToVisual(view).TransformPoint(new Windows.Foundation.Point());
            if (before is { } b && after is { } a && (Math.Abs(b.X - a.X) > .1 || Math.Abs(b.Y - a.Y) > .1))
                throw new InvalidOperationException("Платёж сместился при появлении плюса");
            await SaveAsync(view, path); return;
        }
        if (scenario == "YearHabits" || scenario == "YearMood")
        {
            vm.YearMode = scenario == "YearHabits" ? 2 : 1; vm.Refresh(); await SaveAsync(view, path); return;
        }
        switch (scenario)
        {
            case "TaskDialog": case "TaskDialogOn": vm.CreateTaskCommand.Execute(new TaskLocation(vm.Today, DayPart.Evening)); break;
            case "TaskMultilineDialog":
                var multiline = new TaskEditRequest(null, "Первая строка\nВторая строка", true, false, new(vm.Today));
                vm.SaveTask(multiline, multiline.Description, true, false, Repeat.None);
                vm.EditTaskCommand.Execute(vm.TasksOn(vm.Today).First(t => t.Description == multiline.Description).Id); break;
            case "DeleteTaskDialog": vm.DeleteTaskCommand.Execute(vm.TasksOn(vm.Today).First().Id); break;
            case "DeleteRecurringDialog": case "MoveRecurringDialog":
                var request = new TaskEditRequest(null, "Повторяющаяся задача", true, false, new(vm.Today, DayPart.Morning));
                vm.SaveTask(request, request.Description, request.Important, request.Urgent, Repeat.Weekly);
                var recurring = vm.TasksOn(vm.Today).First(t => t.Description == request.Description);
                if (scenario == "DeleteRecurringDialog") vm.DeleteTaskCommand.Execute(recurring.Id);
                else vm.MoveTaskCommand.Execute(new TaskMove(recurring.Id, vm.Today.AddDays(1), DayPart.Afternoon));
                break;
            case "HabitDialog": vm.CreateHabitCommand.Execute(null); break;
            case "PaymentDialog": vm.CreatePaymentCommand.Execute(null); break;
            case "FinishHabitDialog": vm.FinishHabitCommand.Execute(vm.Habits.First(h => h.Ended is null).Id); break;
            default: throw new ArgumentException("Неизвестный сценарий снимка: " + scenario);
        }
        FrameworkElement? popupRoot = null;
        for (int i = 0; i < 30 && popupRoot is null; i++)
        {
            await Task.Delay(30);
            popupRoot = VisualTreeHelper.GetOpenPopupsForXamlRoot(view.XamlRoot).Select(p => p.Child).OfType<FrameworkElement>().FirstOrDefault();
        }
        if (popupRoot is null) throw new InvalidOperationException("Модальное окно не открылось");
        if (scenario == "TaskDialogOn") foreach (var toggle in Descendants<PrioritySwitch>(popupRoot)) toggle.IsChecked = true;
        if (scenario == "TaskMultilineDialog" && NormalizeLines(Descendants<TextBox>(popupRoot).First().Text) != "Первая строка\nВторая строка")
            throw new InvalidOperationException("Редактор задачи потерял строки описания");
        var chrome = Descendants<Border>(popupRoot).FirstOrDefault(b => b.Name == "BackgroundElement");
        await SaveAsync(chrome ?? popupRoot, path);
    }
    private static string NormalizeLines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    public static async Task SaveTaskPreviewAsync(FrameworkElement view, string path)
    {
        await PrepareAsync(view);
        var card = FindTaskCard(view) ?? throw new InvalidOperationException("Нет карточки задачи для проверки образа переноса");
        using var bitmap = await card.RenderDragBitmapAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
    }
    private static DragTaskButton? FindTaskCard(DependencyObject node)
    {
        if (node is DragTaskButton card) return card;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindTaskCard(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }
    private static IEnumerable<Ui.OriginalImageLoad> OriginalImages(DependencyObject node)
    {
        if (node is Microsoft.UI.Xaml.Controls.Image { Tag: Ui.OriginalImageLoad image }) yield return image;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in OriginalImages(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static async Task PrepareAsync(FrameworkElement view)
    {
        await Task.Delay(150); view.UpdateLayout();
        var images = OriginalImages(view).ToArray();
        try { await Task.WhenAll(images.Select(i => i.Ready)).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Не завершилась загрузка иконок: " + string.Join(", ", images.Where(i => !i.Ready.IsCompleted).Select(i => i.Name).Distinct()));
        }
        // ImageOpened confirms decoding; allow the native compositor to present the pixels.
        await Task.Delay(150); view.UpdateLayout();
    }
    public static async Task SaveAsync(FrameworkElement view, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await PrepareAsync(view);
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(view);
        var buffer = await bitmap.GetPixelsAsync(); using var reader = DataReader.FromBuffer(buffer);
        byte[] pixels = new byte[buffer.Length]; reader.ReadBytes(pixels);
        File.WriteAllBytes(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }
}
