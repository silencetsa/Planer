using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Planer.Core;
using Planer_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Planer_App.Views;

/// <summary>Native view composition. Domain changes go through view model commands.</summary>
public sealed partial class PlannerView : UserControl
{
    private readonly PlannerViewModel _vm;
    private readonly bool _floating;
    private readonly Grid _root = new();
    private readonly PlannerScaleHost _scaleHost;
    private DateTime _suppressClicksUntil;
    private bool _taskDragInProgress;
    private bool _subscribed;
    public PlannerView(PlannerViewModel vm, bool floating = false)
    {
        _vm = vm; _floating = floating; RequestedTheme = ElementTheme.Light; DataContext = vm; FontFamily = Ui.PlannerFont;
        Background = Ui.Brush(Ui.Paper); _scaleHost = new(_root, floating); Content = _scaleHost;
        _root.AllowDrop = true;
        _root.DragEnter += PlannerDragOver;
        _root.DragOver += PlannerDragOver;
        _root.Drop += PlannerDrop;
        _root.DragLeave += PlannerDragLeave;
        Loaded += (_, _) => { Subscribe(); Render(); }; Unloaded += (_, _) => Unsubscribe();
    }
    private void Subscribe()
    {
        if (_subscribed) return; _subscribed = true;
        _vm.PropertyChanged += Changed; _vm.TaskEditorRequested += EditTask; _vm.ErrorRequested += ShowError;
        _vm.TaskDeleteRequested += DeleteTask; _vm.TaskMoveScopeRequested += ChooseTaskMoveScope;
        DragTaskButton.ActiveDragEnded += ResetDrag;
        _vm.HabitEditorRequested += CreateHabit; _vm.HabitFinishRequested += FinishHabit;
        _vm.PaymentEditorRequested += EditPayment;
        _vm.PaymentDeleteRequested += DeletePayment;
        _vm.PaymentFinishRequested += FinishPayment;
        _vm.BudgetCategoryEditorRequested += EditBudgetCategory;
        _vm.BudgetCategoryDeleteRequested += DeleteBudgetCategory;
        _vm.SettingsRequested += EditSettings;
    }
    private void Unsubscribe()
    {
        if (!_subscribed) return; _subscribed = false;
        _vm.PropertyChanged -= Changed; _vm.TaskEditorRequested -= EditTask; _vm.ErrorRequested -= ShowError;
        _vm.TaskDeleteRequested -= DeleteTask; _vm.TaskMoveScopeRequested -= ChooseTaskMoveScope;
        DragTaskButton.ActiveDragEnded -= ResetDrag; ResetDrag(this, EventArgs.Empty);
        _vm.HabitEditorRequested -= CreateHabit; _vm.HabitFinishRequested -= FinishHabit;
        _vm.PaymentEditorRequested -= EditPayment;
        _vm.PaymentDeleteRequested -= DeletePayment;
        _vm.PaymentFinishRequested -= FinishPayment;
        _vm.BudgetCategoryEditorRequested -= EditBudgetCategory;
        _vm.BudgetCategoryDeleteRequested -= DeleteBudgetCategory;
        _vm.SettingsRequested -= EditSettings;
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PlannerViewModel.Revision)) Render();
        else if (args.PropertyName == nameof(PlannerViewModel.Budget)) _refreshFinanceValues?.Invoke();
    }
    private void Render()
    {
        _dropSlots.Clear(); ResetDrag(this, EventArgs.Empty);
        _refreshFinanceValues = null;
        _root.Children.Clear(); _root.RowDefinitions.Clear(); _root.Padding = new(_floating ? 10 : 18);
        if (_floating) { _root.Children.Add(DayCard(_vm.Today, true)); return; }
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto }); _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Ui.Add(_root, Header());
        UIElement board = _vm.Screen switch { PlannerScreen.Week => WeekBoard(), PlannerScreen.Month => MonthBoard(), PlannerScreen.Year => YearBoard(), PlannerScreen.Habits => HabitsBoard(), PlannerScreen.Finances => FinancesBoard(), _ => PaymentsBoard() };
        if (board is FrameworkElement element) element.Margin = new(0, 14, 0, 0); Ui.Add(_root, board, row: 1);
    }
    private UIElement Header()
    {
        if (_vm.Screen is PlannerScreen.Habits or PlannerScreen.Payments or PlannerScreen.Finances) return ManagementHeader();
        var header = Ui.Grid(2); header.ColumnDefinitions[0].Width = new(420);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        var settings = Ui.Button(Ui.Icon("\uE713", 19), tooltip: "Настройки"); settings.Margin = new(0, 2, 12, 0);
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Привычки", Command = _vm.NavigateCommand, CommandParameter = PlannerScreen.Habits, Icon = new FontIcon { Glyph = "\uE73E" } });
        menu.Items.Add(new MenuFlyoutItem { Text = "Платежи", Command = _vm.NavigateCommand, CommandParameter = PlannerScreen.Payments, Icon = new FontIcon { Glyph = "\uE8C7" } });
        menu.Items.Add(new MenuFlyoutItem { Text = "Финансы", Command = _vm.NavigateCommand, CommandParameter = PlannerScreen.Finances, Icon = new FontIcon { Glyph = "\uE8D4" } });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem { Text = "Автозапуск и уведомления", Command = _vm.SettingsCommand, Icon = new FontIcon { Glyph = "\uE713" } });
        settings.Flyout = menu; left.Children.Add(settings);
        left.Children.Add(Ui.Button(Ui.PeriodArrow(false), _vm.PreviousCommand, tooltip: "Предыдущий период"));
        var period = Ui.PeriodButton(_vm.Period, _vm.ParentCommand, "Открыть календарь");
        period.Name = "PeriodButton"; period.ContextFlyout = PeriodCalendar(); left.Children.Add(period);
        left.Children.Add(Ui.Button(Ui.PeriodArrow(true), _vm.NextCommand, tooltip: "Следующий период"));
        var today = Ui.Button(Ui.Text("Сегодня", 11, Ui.Muted), _vm.TodayCommand, tooltip: "Текущая неделя"); today.Margin = new(5, 0, 0, 0); left.Children.Add(today);
        Ui.Add(header, left);
        var goals = new StackPanel { Spacing = 6 };
        var goalRow = new Grid { ColumnSpacing = 12 }; goalRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); goalRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        string goalName = _vm.Screen switch { PlannerScreen.Month => "Цель месяца", PlannerScreen.Year => "Цель года", _ => "Цель недели" };
        var goalTitle = Ui.Text(goalName, Ui.GoalTitleSize, Ui.Accent, true); goalTitle.TextLineBounds = TextLineBounds.Tight; Ui.Add(goalRow, goalTitle);
        var editGoal = new TextBox { Text = _vm.Goal, PlaceholderText = goalName, BorderThickness = new(0), Background = Ui.Brush("#FFFFFF"), CornerRadius = new(5), FontSize = 15, Foreground = Ui.Brush(Ui.Ink), MinHeight = 36, Padding = new(10, 9, 10, 2), VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        editGoal.TextChanged += (_, _) => _vm.Goal = editGoal.Text; Ui.Add(goalRow, editGoal, 1); goals.Children.Add(goalRow);
        var parent = new Grid { ColumnSpacing = 10, Margin = new(100, 0, 0, 0) }; parent.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); parent.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        string parentLabel = _vm.Screen == PlannerScreen.Week ? "Цель месяца" : "Цель года";
        var parentTitle = Ui.Text(parentLabel, 15, Ui.Accent, true); parentTitle.TextLineBounds = TextLineBounds.Tight; Ui.Add(parent, parentTitle);
        var parentText = Ui.Text(string.IsNullOrEmpty(_vm.ParentGoal) ? (_vm.Screen == PlannerScreen.Week ? "Задаётся в планере месяца" : "Задаётся в планере года") : _vm.ParentGoal, 13, Palette.TaskForegrounds[1]);
        Ui.Add(parent, new Border { Background = Ui.Brush("#FDFBD6"), CornerRadius = new(3), Padding = new(9, 4, 9, 4), Child = parentText, VerticalAlignment = VerticalAlignment.Center }, 1);
        if (_vm.Screen != PlannerScreen.Year) goals.Children.Add(parent); Ui.Add(header, goals, 1); return header;
    }
    private UIElement WeekBoard()
    {
        var board = Ui.Grid(5, 2, 10);
        for (int i = 0; i < 5; i++) Ui.Add(board, DayCard(_vm.First.AddDays(i)), i);
        Ui.Add(board, DayCard(_vm.First.AddDays(5)), 0, 1); Ui.Add(board, DayCard(_vm.First.AddDays(6)), 1, 1);
        Ui.Add(board, BacklogCard(), 2, 1, 2); Ui.Add(board, MatrixCard(), 4, 1); return board;
    }
    private UIElement DayCard(DateOnly date, bool floating = false)
    {
        var inner = new Grid { RowSpacing = 4 }; inner.RowDefinitions.Add(new() { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); inner.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var top = new Grid(); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); top.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var label = Ui.Text(date.ToString("ddd", PlannerViewModel.Russian).ToUpperInvariant(), 15, Ui.Muted, true); label.Margin = new(0, 0, 4, 0); Ui.Add(top, label);
        var moods = Ui.Grid(5, gap: 0);
        string[] names = ["Радость", "Хорошо", "Злость", "Грусть", "Нейтрально"];
        foreach (var mood in Enum.GetValues<Mood>())
        {
            var button = Ui.Button(Ui.MoodIcon(mood, _vm.MoodOn(date) == mood), _vm.MoodCommand, new MoodToggle(date, mood), names[(int)mood]);
            button.Padding = new(0); button.Height = 29; button.HorizontalAlignment = HorizontalAlignment.Stretch; Ui.Add(moods, button, (int)mood);
        }
        Ui.Add(top, moods, 1); Ui.Add(inner, top);
        var card = Ui.Card(inner);
        var sections = new DaySectionsPanel(Enum.GetValues<DayPart>().Select(part =>
            _vm.TasksOn(date, part).Count > 0 || (part == DayPart.Morning && _vm.PaymentsOn(date).Count > 0)).ToArray());
        foreach (var part in Enum.GetValues<DayPart>()) DaySection(date, part, sections, card);
        Ui.Add(inner, sections, row: 1); Ui.Add(inner, HabitDots(date), row: 2);
        card.BorderThickness = new(date == _vm.Today && !floating ? 4 : 0); card.BorderBrush = Ui.Brush("#FAF03D");
        if (date == _vm.Today && !floating) card.CornerRadius = new(17, 0, 17, 17);
        if (date != _vm.Today || floating) return card;
        var outer = new Grid(); outer.Children.Add(card);
        var pin = Ui.Button(Ui.OriginalIcon("WhiteAccent.png", 21), _vm.FloatCommand, tooltip: "Текущий день поверх всех окон");
        pin.Background = Ui.Brush("#FAF03D"); pin.Width = 26; pin.Height = 24; pin.Padding = new(2, 0, 0, 2); pin.HorizontalAlignment = HorizontalAlignment.Right;
        pin.VerticalAlignment = VerticalAlignment.Top; pin.CornerRadius = new(0, 0, 0, 9); outer.Children.Add(pin); return outer;
    }
    private void DaySection(DateOnly date, DayPart part, DaySectionsPanel owner, FrameworkElement dayCard)
    {
        var layout = new Grid { ColumnSpacing = 6 };
        layout.ColumnDefinitions.Add(new() { Width = new(17) }); layout.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var icon = Ui.PartIcon(part); icon.HorizontalAlignment = HorizontalAlignment.Center; icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new(0, 2, 0, 0); Ui.Add(layout, icon);
        var content = new StackPanel { Spacing = 4 };
        // Static bills precede the editable task list; its insertion affordance cannot move them.
        if (part == DayPart.Morning) foreach (var payment in _vm.PaymentsOn(date)) content.Children.Add(PaymentBlock(payment));
        foreach (var task in _vm.TasksOn(date, part)) content.Children.Add(TaskBlock(task));
        var plus = Ui.Button(Ui.Icon("\uE710", 13), _vm.CreateTaskCommand, new TaskLocation(date, part), $"Создать задачу: {date:dd.MM}, {PartName(part)}");
        plus.Height = 28; plus.Padding = new(0); plus.HorizontalAlignment = HorizontalAlignment.Stretch;
        plus.Visibility = Visibility.Collapsed; plus.Background = Ui.Brush(Ui.Paper);
        var insertion = new Border { Child = plus }; content.Children.Add(insertion);
        plus.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => owner.SetInsertionSection((int)part, plus.Visibility == Visibility.Visible));
        var scroll = Ui.Scroll(content);
        // A changing scrollbar width must not change text wrapping and feed back into section heights.
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        var taskArea = new Border { Child = scroll, BorderBrush = Ui.Brush("#F2F2F2"), BorderThickness = new(0, 0, 0, part == DayPart.Evening ? 0 : 1) };
        Ui.Add(layout, taskArea, 1);
        var border = new Border { Child = layout, Background = Ui.Brush("#00FFFFFF"), MinHeight = 28, Padding = new(0, 5, 0, 3) };
        bool pointerInside = false;
        border.PointerEntered += (_, _) => { pointerInside = true; plus.Visibility = Visibility.Visible; };
        border.PointerExited += (_, _) => { pointerInside = false; if (plus.FocusState == FocusState.Unfocused) plus.Visibility = Visibility.Collapsed; };
        border.GotFocus += (_, _) => plus.Visibility = Visibility.Visible;
        border.LostFocus += (_, _) => { if (!pointerInside) plus.Visibility = Visibility.Collapsed; };
        owner.AddSection(border, content, 23, part == DayPart.Evening ? 8 : 9);
        EnableDrop(border, date, part, () => insertion, dayCard);
    }
    private static string PartName(DayPart part) => part switch { DayPart.Morning => "утро", DayPart.Afternoon => "день", _ => "вечер" };
    private static string TaskBackground(PlannerTask task) => task.Completed
        ? Ui.WithLightness(Palette.TaskBackgrounds[(int)task.Kind], .96) : Palette.TaskBackgrounds[(int)task.Kind];
    private static (Grid Contents, TextBlock Text) TaskContents(PlannerTask task, bool compact = false)
    {
        string foreground = Palette.TaskForegrounds[(int)task.Kind];
        bool completedYellow = task.Completed && task.Kind == TaskKind.UrgentUnimportant;
        if (task.Completed && !completedYellow) foreground = Ui.WithLightness(foreground, .85);
        var text = Ui.Text(task.Description, compact ? 12.5 : 14, foreground);
        text.MaxLines = compact ? 1 : 2; text.TextTrimming = TextTrimming.CharacterEllipsis; text.Margin = new(0, 0, task.Completed ? 14 : 0, 0);
        if (completedYellow) text.Opacity = .48;
        var contents = new Grid(); contents.Children.Add(text);
        if (task.Completed)
        {
            var check = Ui.OriginalIcon("Completed.png", 16); check.HorizontalAlignment = HorizontalAlignment.Right; check.VerticalAlignment = VerticalAlignment.Top; contents.Children.Add(check);
        }
        return (contents, text);
    }
    private UIElement TaskBlock(PlannerTask task, bool compact = false)
    {
        var (contents, text) = TaskContents(task, compact);
        var button = new DragTaskButton { Content = contents, SourceTask = task }; button.Padding = new(7, compact ? 3 : 5, 7, compact ? 3 : 5);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, task.Description);
        button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Background = Ui.Brush(TaskBackground(task)); button.CornerRadius = new(4);
        button.Resources["ButtonBackgroundPointerOver"] = button.Background;
        button.Resources["ButtonBackgroundPressed"] = button.Background;
        button.Click += (_, _) => { if (!button.IsClickSuppressed && !_taskDragInProgress && DateTime.UtcNow > _suppressClicksUntil) _vm.ToggleTaskCommand.Execute(task.Id); };
        button.PointerEntered += (_, _) => { text.MaxLines = 0; text.TextTrimming = TextTrimming.None; };
        button.PointerExited += (_, _) => { text.MaxLines = compact ? 1 : 2; text.TextTrimming = TextTrimming.CharacterEllipsis; };
        button.DragStarting += (_, args) => { _taskDragInProgress = true; args.Data.SetText(task.Id.ToString()); args.Data.RequestedOperation = DataPackageOperation.Move; };
        button.DragGestureFinished += (_, _) => { _taskDragInProgress = false; _suppressClicksUntil = DateTime.UtcNow.AddMilliseconds(250); };
        button.DragFailed += (_, error) => _vm.Run(() => throw error);
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Редактировать", Icon = new FontIcon { Glyph = "\uE70F" }, Command = _vm.EditTaskCommand, CommandParameter = task.Id });
        menu.Items.Add(new MenuFlyoutItem { Text = "Удалить", Icon = new FontIcon { Glyph = "\uE74D" }, Command = _vm.DeleteTaskCommand, CommandParameter = task.Id });
        button.ContextFlyout = menu; return button;
    }
    private UIElement PaymentBlock(PaymentOccurrence payment, bool compact = false)
    {
        var layout = new Grid { ColumnSpacing = 5 }; layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); layout.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(layout, Ui.OriginalIcon(payment.Completed ? "Completed.png" : "YellowAccent.png", 18));
        var title = Ui.Text(payment.Name, compact ? 12.5 : 14, "#FFFFFF"); title.MaxLines = 1; title.TextTrimming = TextTrimming.CharacterEllipsis; Ui.Add(layout, title, 1);
        Ui.Add(layout, Ui.Text(FormatAmount(payment.Amount), compact ? 14.5 : 16, payment.Completed ? "#FFFFFF" : "#FAF03D", true), 2);
        var button = Ui.Button(layout, _vm.PaymentCommand, new PaymentToggle(payment.PaymentId, payment.Date), $"{payment.Name}: {payment.Amount:N2} ₽ — отметить оплату");
        button.Tag = payment;
        button.Background = Ui.Brush(payment.Completed ? "#E4CAED" : Ui.Accent); button.Padding = new(5, 4, 5, 4); button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.CornerRadius = new(4);
        button.Resources["ButtonBackgroundPointerOver"] = button.Background; button.Resources["ButtonBackgroundPressed"] = button.Background;
        return button;
    }
    private UIElement HabitDots(DateOnly date, bool compact = false)
    {
        var habits = _vm.HabitsOn(date); var row = Ui.Grid(Math.Max(1, habits.Count), gap: 4);
        for (int i = 0; i < habits.Count; i++)
        {
            var h = habits[i]; var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            if (!compact) { var name = Ui.Text(h.Name, 11, Ui.Muted, light: true); name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxLines = 1; stack.Children.Add(name); }
            stack.Children.Add(Ui.Dot(h.Color, h.Completed, compact ? 7 : 19));
            var button = Ui.Button(stack, _vm.HabitCommand, new HabitToggle(h.HabitId, date), h.Name); button.Padding = new(compact ? 1 : 2); button.HorizontalAlignment = HorizontalAlignment.Stretch; Ui.Add(row, button, i);
        }
        row.MinHeight = compact ? 12 : 43; return row;
    }
    private UIElement BacklogCard()
    {
        var layout = new Grid { RowSpacing = 12 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); layout.RowDefinitions.Add(new() { Height = new(23) });
        var title = Ui.Text("НУЖНО РАСПРЕДЕЛИТЬ", 15, Ui.Muted, true); title.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(layout, title);
        int columns = _vm.Screen == PlannerScreen.Month ? 1 : 2;
        var tasks = Ui.Grid(columns, gap: 8); tasks.RowDefinitions.Clear(); int rowCount = _vm.Backlog.Count / columns + 1;
        for (int i = 0; i < rowCount; i++) tasks.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < _vm.Backlog.Count; i++) Ui.Add(tasks, TaskBlock(_vm.Backlog[i]), i % columns, i / columns);
        var insertion = new Border { Height = 0, VerticalAlignment = VerticalAlignment.Top }; Ui.Add(tasks, insertion, _vm.Backlog.Count % columns, _vm.Backlog.Count / columns);
        tasks.VerticalAlignment = VerticalAlignment.Top; Ui.Add(layout, Ui.Scroll(tasks), row: 1);
        var plus = Ui.Button(Ui.Icon("\uE710", 14), _vm.CreateTaskCommand, new TaskLocation(null), "Добавить нераспределённую задачу");
        plus.HorizontalAlignment = HorizontalAlignment.Stretch; plus.Padding = new(0); plus.Background = Ui.Brush("#F5F5F5"); plus.Opacity = 0; Ui.Add(layout, plus, row: 2);
        var card = Ui.Card(layout); card.PointerEntered += (_, _) => plus.Opacity = 1; card.PointerExited += (_, _) => plus.Opacity = 0;
        card.GotFocus += (_, _) => plus.Opacity = 1; card.LostFocus += (_, _) => plus.Opacity = 0; EnableDrop(card, null, anchor: () => insertion, widthTarget: insertion); return card;
    }
    private UIElement MatrixCard()
    {
        var layout = new Grid { RowSpacing = 12 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var title = Ui.Button(Ui.Text(_vm.NotesVisible ? "ЗАМЕТКИ" : "МАТРИЦА", 15, Ui.Muted, true), _vm.ToggleNotesCommand, tooltip: "Переключить матрицу и заметки");
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Padding = new(0); Ui.Add(layout, title);
        if (_vm.NotesVisible)
        {
            var notes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Text = _vm.Notes, BorderThickness = new(0), Background = Ui.Brush("#FFFFFF"), PlaceholderText = _vm.Screen == PlannerScreen.Month ? "Заметки этого месяца…" : "Заметки этой недели…",
                VerticalAlignment = VerticalAlignment.Stretch, FontSize = 12, FontStyle = Windows.UI.Text.FontStyle.Italic, Padding = new(0), Foreground = Ui.Brush(Ui.Ink) };
            notes.TextChanged += (_, _) => _vm.Notes = notes.Text; Ui.Add(layout, notes, row: 1);
        }
        else
        {
            const double scale = 1.1;
            var stack = new StackPanel { Spacing = 20 * scale, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var cross = Ui.Grid(3, 3, 0); cross.Width = 150 * scale; cross.Height = 150 * scale; cross.ColumnDefinitions[1].Width = new(scale); cross.RowDefinitions[1].Height = new(scale);
            var vertical = new Border { Background = Ui.Brush("#F1F1F1") }; Grid.SetRowSpan(vertical, 3); Ui.Add(cross, vertical, 1);
            Ui.Add(cross, new Border { Background = Ui.Brush("#F1F1F1") }, 0, 1, 3);
            var counts = _vm.Matrix; int[] positions = [0, 2, 1, 3];
            string[] descriptions = ["Срочное важное", "Срочное неважное", "Несрочное важное", "Несрочное неважное"];
            for (int i = 0; i < 4; i++)
            {
                var number = Ui.Text(counts.Scheduled[i].ToString(), 21 * scale, Palette.TaskForegrounds[i]); number.HorizontalAlignment = HorizontalAlignment.Center;
                var pill = new Border { Background = Ui.Brush(Palette.TaskBackgrounds[i]), CornerRadius = new(7 * scale), Width = 43 * scale, Height = 43 * scale, Child = number }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pill, descriptions[i] + ": " + counts.Scheduled[i]);
                Ui.Add(cross, pill, (positions[i] % 2) * 2, (positions[i] / 2) * 2);
            }
            stack.Children.Add(cross);
            var backlog = new StackPanel { Spacing = 6 * scale }; var caption = Ui.Text("Без времени", 12 * scale, Ui.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center; backlog.Children.Add(caption);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 * scale, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 4; i++)
            {
                var number = Ui.Text(counts.Unscheduled[i].ToString(), 11 * scale, Palette.TaskForegrounds[i]); number.HorizontalAlignment = HorizontalAlignment.Center;
                row.Children.Add(new Border { Background = Ui.Brush(Palette.TaskBackgrounds[i]), CornerRadius = new(3 * scale), Width = 23 * scale, Height = 23 * scale, Child = number });
            }
            backlog.Children.Add(row); stack.Children.Add(backlog); Ui.Add(layout, stack, row: 1);
        }
        return Ui.Card(layout);
    }
    private bool OwnsDialogs => XamlRoot is not null && _floating == App.FloatingOwnsDialogs;
    private async void EditTask(object? sender, TaskEditRequest request)
    {
        if (!OwnsDialogs) return;
        var description = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Text = request.Description, Header = "Описание задачи", PlaceholderText = "Что нужно сделать?", MinHeight = 96, MaxHeight = 250, MaxLength = 10000 };
        var important = new PrioritySwitch(request.Important, "Важное");
        var urgent = new PrioritySwitch(request.Urgent, "Срочное");
        var repeat = new ComboBox { Header = "Повтор", ItemsSource = TaskRepeatNames, SelectedIndex = (int)request.Repeat, HorizontalAlignment = HorizontalAlignment.Stretch };
        var validation = Ui.Text("", 12, "#BD3026"); var contents = new StackPanel { Spacing = 16, MinWidth = 340 }; contents.Children.Add(description);
        contents.Children.Add(Ui.PriorityRow("Неважное", important, "Важное"));
        contents.Children.Add(Ui.PriorityRow("Несрочное", urgent, "Срочное")); contents.Children.Add(repeat); contents.Children.Add(validation);
        var dialog = Ui.Dialog(XamlRoot, request.Id is null ? "Новая задача" : "Редактировать задачу", contents, scale: _scaleHost.ScaleFactor);
        dialog.PrimaryButtonClick += (_, args) => { if (string.IsNullOrWhiteSpace(description.Text)) { validation.Text = "Введите описание задачи"; args.Cancel = true; } };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.SaveTask(request, description.Text, important.IsChecked == true, urgent.IsChecked == true, (Repeat)repeat.SelectedIndex);
    }
    private async void ShowError(object? sender, string message) { if (OwnsDialogs) await ErrorDialog(message); }
    private async Task ErrorDialog(string message)
    {
        if (XamlRoot is null) return;
        try { await Ui.Dialog(XamlRoot, "Не удалось выполнить действие", Ui.Text(message, 14, Ui.Muted), primary: "", close: "Понятно", scale: _scaleHost.ScaleFactor).ShowAsync(); }
        catch (InvalidOperationException) { }
    }
}
