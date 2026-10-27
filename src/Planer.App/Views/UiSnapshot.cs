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
internal static partial class UiSnapshot
{
    public static async Task SaveScenarioAsync(FrameworkElement view, string path, string scenario, PlannerViewModel vm)
    {
        await PrepareAsync(view);
        if (scenario.StartsWith("Annual", StringComparison.Ordinal)) { await SaveAnnualScenarioAsync(view, path, scenario, vm); return; }
        if (scenario.StartsWith("Confirmation", StringComparison.Ordinal)) { await SaveConfirmationScenarioAsync(view, path, scenario, vm); return; }
        if (scenario.StartsWith("Finance", StringComparison.Ordinal)) { await SaveFinanceScenarioAsync(view, path, scenario, vm); return; }
        if (scenario == "StartupHidden") { await SaveAsync(view, path); return; }
        if (scenario == "ReminderActivation")
        {
            var application = (App)Application.Current; string key = ActivationBridge.KeyForDatabase(App.DatabasePath);
            vm.Navigate(PlannerScreen.Month, vm.Today.AddMonths(-1));
            if (!await ActivationBridge.Forward(key, DesktopActivation.Today)) throw new InvalidOperationException("Не обработан переход уведомления в текущий день");
            var first = App.FloatingWindow ?? throw new InvalidOperationException("Не открыто окно сегодняшнего дня");
            if (!await ActivationBridge.Forward(key, DesktopActivation.Today) || !ReferenceEquals(first, App.FloatingWindow))
                throw new InvalidOperationException("Повторное уведомление создало второе окно дня");
            if (!await ActivationBridge.Forward(key, DesktopActivation.Week) || vm.Screen != PlannerScreen.Week || vm.SelectedDate != vm.Today)
                throw new InvalidOperationException("Уведомление переноса не открыло текущую неделю");
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--notification-check"); start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(Path.GetDirectoryName(App.DatabasePath)!);
            using var child = System.Diagnostics.Process.Start(start)!;
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));
            if (child.ExitCode != 0) throw new InvalidOperationException("Фоновый запуск не передал проверку работающему экземпляру");
            var toastClass = Guid.NewGuid(); application.RegisterToastActivation(toastClass);
            async Task NativeActivate(PlannerDestination destination, Func<bool> reached)
            {
                await Task.Run(() =>
                {
                    object native = Activator.CreateInstance(Type.GetTypeFromCLSID(toastClass)!)!;
                    try { System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(((IToastActivationCallback)native)
                        .Activate(ShellIdentity.AppUserModelId, WindowsNotifications.ActivationArgument(destination), 0, 0)); }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(native); }
                });
                for (int i = 0; i < 30 && !reached(); i++) await Task.Delay(50);
                if (!reached()) throw new InvalidOperationException("Не обработана нативная активация уведомления: " + destination);
            }
            vm.Navigate(PlannerScreen.Month, vm.Today.AddMonths(-1));
            first.AppWindow.Hide();
            await NativeActivate(PlannerDestination.Today, () => ReferenceEquals(first, App.FloatingWindow) && first.AppWindow.IsVisible);
            await NativeActivate(PlannerDestination.Week, () => vm.Screen == PlannerScreen.Week && vm.SelectedDate == vm.Today);
            await NativeActivate(PlannerDestination.Habits, () => vm.Screen == PlannerScreen.Habits);
            vm.Navigate(PlannerScreen.Week, vm.Today);
            first.Close(); await SaveAsync(view, path); return;
        }
        if (scenario == "CompletedTaskColors")
        {
            foreach (TaskKind kind in Enum.GetValues<TaskKind>())
            {
                string description = "Выполненная задача: " + kind;
                var request = new TaskEditRequest(null, description, kind is TaskKind.UrgentImportant or TaskKind.NotUrgentImportant,
                    kind is TaskKind.UrgentImportant or TaskKind.UrgentUnimportant, new(vm.Today));
                vm.SaveTask(request, description, request.Important, request.Urgent, Repeat.None);
                vm.ToggleTaskCommand.Execute(vm.TasksOn(vm.Today).Single(t => t.Description == description).Id);
            }
            await PrepareAsync(view);
            foreach (var card in Descendants<DragTaskButton>(view).Where(b => b.SourceTask?.Completed == true))
            {
                var color = ((SolidColorBrush)card.Background).Color;
                double lightness = (new[] { color.R, color.G, color.B }.Max() + new[] { color.R, color.G, color.B }.Min()) / 510d;
                if (Math.Abs(lightness - .96) > .002) throw new InvalidOperationException("Фон выполненной задачи должен иметь HSL Lightness 96%");
                var text = Descendants<TextBlock>(card).First();
                var ink = ((SolidColorBrush)text.Foreground).Color;
                if (card.SourceTask!.Kind == TaskKind.UrgentUnimportant)
                {
                    if (ink != Ui.Brush(Palette.TaskForegrounds[(int)TaskKind.UrgentUnimportant]).Color || Math.Abs(text.Opacity - .48) > .001)
                        throw new InvalidOperationException("Оформление жёлтого текста не должно меняться");
                }
                else
                {
                    double inkLightness = (new[] { ink.R, ink.G, ink.B }.Max() + new[] { ink.R, ink.G, ink.B }.Min()) / 510d;
                    if (Math.Abs(inkLightness - .85) > .002 || text.Opacity != 1)
                        throw new InvalidOperationException("Текст выполненной задачи должен иметь HSL Lightness 85% без дополнительной прозрачности");
                }
            }
            await SaveAsync(view, path); return;
        }
        if (scenario is "LayoutStress" or "TodayLayoutStress")
        {
            await CheckLayoutStressAsync(view, path, vm, scenario == "TodayLayoutStress"); return;
        }
        if (scenario == "LayoutRecovery") { await CheckLayoutRecoveryAsync(view, path, vm); return; }
        if (scenario is "HabitsActions" or "PaymentsActions" or "PaymentsActionsCompleted")
        {
            if (scenario == "PaymentsActionsCompleted")
            {
                vm.ShowCompletedPayments = true; vm.Refresh(); await PrepareAsync(view);
            }
            var create = Descendants<Button>(view).Single(b => b.Name == "CreateManagementButton");
            var position = create.TransformToVisual(view).TransformPoint(new Windows.Foundation.Point());
            if (position.X < view.ActualWidth / 2 || position.Y > 100 || view.ActualWidth - position.X - create.ActualWidth < 10)
                throw new InvalidOperationException("Создать должна находиться справа в заголовке, с отступом от края");
            if (scenario is "PaymentsActions" or "PaymentsActionsCompleted")
            {
                var completed = Descendants<Button>(view).Single(b => b.Name == "CompletedPaymentsButton");
                var caption = Descendants<TextBlock>(completed).First();
                if (completed.Background is not SolidColorBrush { Color: { R: 255, G: 238, B: 227 } }
                    || caption.Foreground is not SolidColorBrush { Color: { R: 255, G: 124, B: 5 } })
                    throw new InvalidOperationException("Кнопка завершённых платежей не использует оранжевую палитру");
                if (caption.FontSize != 14 || caption.FontWeight.Weight != 400 || completed.Padding != new Thickness(14, 7, 14, 7)
                    || completed.ActualHeight >= create.ActualHeight)
                    throw new InvalidOperationException("Кнопка завершённых платежей должна использовать компактный стиль Сохранить/Отмена");
                var bottom = completed.TransformToVisual(view).TransformPoint(new Windows.Foundation.Point());
                if (bottom.X > 50 || bottom.Y < view.ActualHeight / 2)
                    throw new InvalidOperationException("Кнопка завершённых платежей должна оставаться внизу слева");
            }
            await SaveAsync(view, path); return;
        }
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
            ShellIdentity.VerifyWindowIcon(App.Window);
            var tray = App.Tray ?? throw new InvalidOperationException("Иконка области уведомлений не создана");
            if (!tray.IsRegistered) throw new InvalidOperationException("Shell не вернула координаты иконки");
            tray.Invoke(TrayAction.OpenPlanner);
            if (!App.Window.AppWindow.IsVisible) throw new InvalidOperationException("Меню не открыло главный планер");
            tray.Invoke(TrayAction.OpenToday); await Task.Delay(100); var first = App.FloatingWindow;
            tray.Invoke(TrayAction.OpenToday); await Task.Delay(100);
            if (first is null || !ReferenceEquals(first, App.FloatingWindow)
                || first.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter { IsAlwaysOnTop: true })
                throw new InvalidOperationException("Меню не сохранило единственное окно текущего дня поверх окон");
            ShellIdentity.VerifyWindowIcon(first);
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
                if (Descendants<TextBlock>(paymentButton).Last().Foreground is not SolidColorBrush { Color: { R: 255, G: 255, B: 255 } })
                    throw new InvalidOperationException("Сумма выполненного платежа должна быть белой");
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
            vm.SelectYearHabit(scenario == "YearHabits" ? vm.YearHabits.FirstOrDefault()?.Id : null); vm.Refresh(); await SaveAsync(view, path); return;
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
            case "SettingsDialog": vm.SettingsCommand.Execute(null); break;
            case "PaymentDialog": vm.CreatePaymentCommand.Execute(null); break;
            case "FinishHabitDialog": vm.FinishHabitCommand.Execute(vm.Habits.First(h => h.Ended is null).Id); break;
            default: throw new ArgumentException("Неизвестный сценарий снимка: " + scenario);
        }
        FrameworkElement? popupRoot = null;
        ContentDialog? openedDialog = null;
        for (int i = 0; i < 100 && popupRoot is null; i++)
        {
            await Task.Delay(30);
            openedDialog = Ui.OpenDialogForRoot(view.XamlRoot);
            if (openedDialog?.Content is not DependencyObject expectedContent) continue;
            popupRoot = VisualTreeHelper.GetOpenPopupsForXamlRoot(view.XamlRoot).Select(p => p.Child).OfType<FrameworkElement>()
                .FirstOrDefault(root => Descendants<DependencyObject>(root).Any(child => ReferenceEquals(child, expectedContent)));
        }
        if (popupRoot is null || openedDialog is null) throw new InvalidOperationException($"Не загрузилось содержимое формы {scenario}");
        await PrepareAsync(popupRoot);
        var cancel = Descendants<Button>(popupRoot).FirstOrDefault(b => b.Content is string s && s == "Отмена");
        if (cancel is not null && (cancel.Background is not SolidColorBrush { Color: { R: 255, G: 238, B: 227 } }
            || cancel.Foreground is not SolidColorBrush { Color: { R: 255, G: 124, B: 5 } }))
            throw new InvalidOperationException("Отмена должна использовать оранжевую палитру");
        if (scenario is "DeleteTaskDialog" or "DeleteRecurringDialog"
            && !Descendants<Border>(popupRoot).Any(b => b.Name == "DeleteTaskPreview" && b.Background is SolidColorBrush))
            throw new InvalidOperationException("В подтверждении удаления отсутствует цветная карточка задачи");
        if (scenario == "TaskDialogOn") foreach (var toggle in Descendants<PrioritySwitch>(popupRoot)) toggle.IsChecked = true;
        if (scenario == "DeleteRecurringDialog" && (!Descendants<RadioButton>(popupRoot).Any(b => b.Name == "DeleteOnlyOccurrence" && b.IsChecked == true)
            || !Descendants<RadioButton>(popupRoot).Any(b => b.Name == "DeleteFutureChain")))
            throw new InvalidOperationException("Удаление повтора должно предлагать одну задачу или будущую цепочку");
        if (scenario == "TaskMultilineDialog" && NormalizeLines(Descendants<TextBox>((DependencyObject)openedDialog.Content).First().Text) != "Первая строка\nВторая строка")
            throw new InvalidOperationException("Редактор задачи потерял строки описания");
        var chrome = Descendants<Border>(popupRoot).FirstOrDefault(b => b.Name == "BackgroundElement");
        await SaveAsync(chrome ?? popupRoot, path);
    }
    private static string NormalizeLines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    private static async Task CheckLayoutStressAsync(FrameworkElement view, string path, PlannerViewModel vm, bool floating)
    {
        void Trace(string step) => File.AppendAllText(path + ".steps.log", $"{DateTime.UtcNow:O} {step}\n");
        Trace("begin");
        var application = (App)Application.Current; int failures = application.LayoutCycleCount;
        var window = floating ? App.FloatingWindow! : App.Window;
        var minimum = WindowSizing.Minimum(window, floating);
        if (window.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter presenter
            || presenter.PreferredMinimumWidth != minimum.Width || presenter.PreferredMinimumHeight != minimum.Height)
            throw new InvalidOperationException("Минимальные размеры окна не применены");
        // Wrap long descriptions and overflow all three sections, exercising the actual ScrollViewers.
        for (int i = 0; i < 9; i++)
        {
            Trace($"seed {i}");
            var request = new TaskEditRequest(null, $"Проверка {i}: длинное описание задачи с несколькими строками текста для изменения высоты карточки и прокрутки внутри секции дня.", true, false,
                new(vm.Today, i < 6 ? DayPart.Morning : i < 8 ? DayPart.Afternoon : DayPart.Evening));
            vm.SaveTask(request, request.Description, true, false, Repeat.None);
        }
        var screens = floating ? new[] { PlannerScreen.Week } : Enum.GetValues<PlannerScreen>();
        foreach (var screen in screens)
        {
            Trace($"screen {screen}");
            vm.Navigate(screen, vm.Today); await PrepareAsync(view);
            for (int step = 0; step < (screen == PlannerScreen.Week ? 24 : 4); step++)
            {
                Trace($"step {screen} {step}");
                var size = (step % 4) switch
                {
                    0 => minimum,
                    1 => new Windows.Graphics.SizeInt32(minimum.Width + 1, minimum.Height + 1),
                    2 => new Windows.Graphics.SizeInt32(floating ? 520 : 1920, floating ? 850 : 1080),
                    _ => new Windows.Graphics.SizeInt32(floating ? 340 : 1400, floating ? 610 : 850)
                };
                window.AppWindow.Resize(size); await Task.Delay(20); view.UpdateLayout();
                if (screen is PlannerScreen.Week or PlannerScreen.Month)
                {
                    var pluses = Descendants<Button>(view).Where(b => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(b).StartsWith($"Создать задачу: {vm.Today:dd.MM},", StringComparison.Ordinal)).ToArray();
                    for (int i = 0; i < pluses.Length; i++) pluses[i].Visibility = i == step % 3 ? Visibility.Visible : Visibility.Collapsed;
                    foreach (var task in Descendants<DragTaskButton>(view))
                        foreach (var text in Descendants<TextBlock>(task)) text.MaxLines = step % 2 == 0 ? 0 : 2;
                    foreach (var scroll in Descendants<ScrollViewer>(view)) scroll.ChangeView(null, step % 2 == 0 ? 40 : 0, null, true);
                }
                view.UpdateLayout();
                if (application.LayoutCycleCount != failures) throw new InvalidOperationException("Изменение размеров вызвало цикл раскладки");
            }
        }
        Trace("complete");
        vm.Navigate(PlannerScreen.Week, vm.Today); window.AppWindow.Resize(minimum); await PrepareAsync(view);
        if (floating && Descendants<Border>(view).Any(b => b.BorderThickness.Top > 0 && b.BorderBrush is SolidColorBrush { Color: { R: 250, G: 240, B: 61 } }))
            throw new InvalidOperationException("В отдельном окне дня осталась жёлтая рамка");
        if (application.LayoutCycleCount != failures) throw new InvalidOperationException("Минимальное окно вызвало цикл раскладки");
        await SaveAsync(view, path);
    }
    private static async Task CheckLayoutRecoveryAsync(FrameworkElement view, string path, PlannerViewModel vm)
    {
        void Trace(string step) => File.AppendAllText(path + ".steps.log", $"{DateTime.UtcNow:O} {step}\n");
        var application = (App)Application.Current;
        var goal = vm.Goal; var backlog = vm.Backlog.Select(t => t.Id).ToArray();
        if (application.RecoverLayoutCycle(new InvalidOperationException("Unrelated QA exception")))
            throw new InvalidOperationException("Обработка раскладки скрывает постороннюю ошибку");
        if (application.RecoverLayoutCycle(new System.Runtime.InteropServices.COMException("Windows.ApplicationModel.LimitedAccessFeatures"), "Windows.ApplicationModel.LimitedAccessFeatures"))
            throw new InvalidOperationException("Обработка раскладки скрывает постороннюю ошибку WinRT");
        Trace("open today"); vm.FloatCommand.Execute(null); await Task.Delay(100); Trace("today opened");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int recovered = application.LayoutRecoveryCount;
            Trace($"recover {attempt}");
            bool accepted = attempt == 1
                ? application.RecoverLayoutCycle(new System.Runtime.InteropServices.COMException("Projected QA exception"), "Layout cycle detected. Layout could not complete.")
                : application.RecoverLayoutCycle(new LayoutCycleException("Controlled recovery QA"));
            if (!accepted)
                throw new InvalidOperationException("LayoutCycleException не принята для восстановления");
            for (int wait = 0; wait < 250 && application.LayoutRecoveryCount == recovered; wait++) await Task.Delay(20);
            if (application.LayoutRecoveryCount != recovered + 1) throw new InvalidOperationException("Экран не восстановлен после завершения текущего прохода");
            await PrepareAsync(view);
        }
        var retry = Descendants<Button>(view).Single(b => b.Name == "RetryPlannerLayout");
        Trace("fallback");
        await SaveAsync(view, path + ".fallback.png");
        retry.Command.Execute(null);
        for (int wait = 0; wait < 100 && !Descendants<PlannerView>(view).Any(); wait++) await Task.Delay(20);
        await PrepareAsync(view);
        if (!ReferenceEquals(vm, App.ViewModel) || goal != vm.Goal || !backlog.SequenceEqual(vm.Backlog.Select(t => t.Id))
            || !Descendants<PlannerView>(view).Any() || App.FloatingWindow is null
            || !Descendants<PlannerView>(App.FloatingWindow.Content).Any())
            throw new InvalidOperationException("Восстановление изменило данные или потеряло одно из окон");
        App.FloatingWindow.Close(); await SaveAsync(view, path);
        Trace("complete");
    }
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
