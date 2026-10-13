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
    private DateTime _suppressClicksUntil;
    private bool _subscribed;
    public PlannerView(PlannerViewModel vm, bool floating = false)
    {
        _vm = vm; _floating = floating; RequestedTheme = ElementTheme.Light; DataContext = vm;
        Background = Ui.Brush(Ui.Paper); Content = _root;
        Loaded += (_, _) => { Subscribe(); Render(); }; Unloaded += (_, _) => Unsubscribe();
    }
    private void Subscribe()
    {
        if (_subscribed) return; _subscribed = true;
        _vm.PropertyChanged += Changed; _vm.TaskEditorRequested += EditTask; _vm.ErrorRequested += ShowError;
        _vm.HabitEditorRequested += CreateHabit; _vm.HabitFinishRequested += FinishHabit;
        _vm.PaymentEditorRequested += EditPayment; _vm.PaymentDeleteRequested += DeletePayment;
    }
    private void Unsubscribe()
    {
        if (!_subscribed) return; _subscribed = false;
        _vm.PropertyChanged -= Changed; _vm.TaskEditorRequested -= EditTask; _vm.ErrorRequested -= ShowError;
        _vm.HabitEditorRequested -= CreateHabit; _vm.HabitFinishRequested -= FinishHabit;
        _vm.PaymentEditorRequested -= EditPayment; _vm.PaymentDeleteRequested -= DeletePayment;
    }
    private void Changed(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(PlannerViewModel.Revision)) Render(); }
    private void Render()
    {
        _root.Children.Clear(); _root.RowDefinitions.Clear(); _root.Padding = new(_floating ? 10 : 18);
        if (_floating) { _root.Children.Add(DayCard(_vm.Today, true)); return; }
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto }); _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Ui.Add(_root, Header());
        UIElement board = _vm.Screen switch { PlannerScreen.Week => WeekBoard(), PlannerScreen.Month => MonthBoard(), PlannerScreen.Year => YearBoard(), PlannerScreen.Habits => HabitsBoard(), _ => PaymentsBoard() };
        if (board is FrameworkElement element) element.Margin = new(0, 14, 0, 0); Ui.Add(_root, board, row: 1);
    }
    private UIElement Header()
    {
        if (_vm.Screen is PlannerScreen.Habits or PlannerScreen.Payments) return ManagementHeader();
        var header = Ui.Grid(2); header.ColumnDefinitions[0].Width = new(420);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
        var settings = Ui.Button(Ui.Icon("\uE713", 19), tooltip: "Настройки"); settings.Margin = new(0, 2, 12, 0);
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Привычки", Command = _vm.NavigateCommand, CommandParameter = PlannerScreen.Habits, Icon = new FontIcon { Glyph = "\uE73E" } });
        menu.Items.Add(new MenuFlyoutItem { Text = "Платежи", Command = _vm.NavigateCommand, CommandParameter = PlannerScreen.Payments, Icon = new FontIcon { Glyph = "\uE8C7" } });
        settings.Flyout = menu; left.Children.Add(settings);
        left.Children.Add(Ui.Button(Ui.Icon("\uE76B", 18, "#E7D92B"), _vm.PreviousCommand, tooltip: "Предыдущий период"));
        var period = Ui.Button(Ui.Text(_vm.Period, 21, Ui.Accent, true), _vm.ParentCommand, tooltip: "Открыть календарь");
        period.Background = Ui.Brush("#F3E3FA"); period.Padding = new(12, 7, 12, 7); left.Children.Add(period);
        left.Children.Add(Ui.Button(Ui.Icon("\uE76C", 18, "#E7D92B"), _vm.NextCommand, tooltip: "Следующий период"));
        var today = Ui.Button(Ui.Text("Сегодня", 11, Ui.Muted), _vm.TodayCommand, tooltip: "Текущая неделя"); today.Margin = new(5, 0, 0, 0); left.Children.Add(today);
        Ui.Add(header, left);
        var goals = new StackPanel { Spacing = 6 };
        var goalRow = new Grid { ColumnSpacing = 12 }; goalRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); goalRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        string goalName = _vm.Screen switch { PlannerScreen.Month => "Цель месяца", PlannerScreen.Year => "Цель года", _ => "Цель недели" };
        Ui.Add(goalRow, Ui.Text(goalName, 17, Ui.Accent, true));
        var editGoal = new TextBox { Text = _vm.Goal, PlaceholderText = goalName, BorderThickness = new(0), Background = Ui.Brush("#FFFFFF"), CornerRadius = new(5), FontSize = 13, Foreground = Ui.Brush(Ui.Ink), MinHeight = 34 };
        editGoal.TextChanged += (_, _) => _vm.Goal = editGoal.Text; Ui.Add(goalRow, editGoal, 1); goals.Children.Add(goalRow);
        var parent = new Grid { ColumnSpacing = 10, Margin = new(100, 0, 0, 0) }; parent.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); parent.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        string parentLabel = _vm.Screen == PlannerScreen.Week ? "Цель месяца" : "Цель года";
        Ui.Add(parent, Ui.Text(parentLabel, 11, Ui.Accent, true));
        var parentText = Ui.Text(string.IsNullOrEmpty(_vm.ParentGoal) ? (_vm.Screen == PlannerScreen.Week ? "Задаётся в планере месяца" : "Задаётся в планере года") : _vm.ParentGoal, 11, "#AA8800");
        Ui.Add(parent, new Border { Background = Ui.Brush("#FDFBD6"), CornerRadius = new(3), Padding = new(9, 4, 9, 4), Child = parentText }, 1);
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
        var label = Ui.Text($"{date.ToString("ddd", PlannerViewModel.Russian).ToUpperInvariant()} {date.Day}", 11, Ui.Muted, true); label.Margin = new(0, 0, 5, 0); Ui.Add(top, label);
        var moods = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        string[] names = ["Радость", "Хорошо", "Злость", "Грусть", "Нейтрально"];
        foreach (var mood in Enum.GetValues<Mood>())
        {
            var button = Ui.Button(Ui.MoodIcon(mood, _vm.MoodOn(date) == mood), _vm.MoodCommand, new MoodToggle(date, mood), names[(int)mood]);
            button.Padding = new(0); button.Width = 24; button.Height = 26; moods.Children.Add(button);
        }
        Ui.Add(top, moods, 1); Ui.Add(inner, top);
        var sections = Ui.Grid(1, 3, 2); foreach (var part in Enum.GetValues<DayPart>()) Ui.Add(sections, DaySection(date, part), row: (int)part);
        Ui.Add(inner, sections, row: 1); Ui.Add(inner, HabitDots(date), row: 2);
        var card = Ui.Card(inner); card.BorderThickness = new(date == _vm.Today ? 3 : 0); card.BorderBrush = Ui.Brush("#FAF03D");
        if (date != _vm.Today || floating) return card;
        var outer = new Grid(); outer.Children.Add(card);
        var pin = Ui.Button(Ui.Text("↗", 15, "#FFFFFF", true), _vm.FloatCommand, tooltip: "Текущий день поверх всех окон");
        pin.Background = Ui.Brush("#E9E12A"); pin.Width = 24; pin.Height = 23; pin.Padding = new(0); pin.HorizontalAlignment = HorizontalAlignment.Right;
        pin.VerticalAlignment = VerticalAlignment.Top; pin.CornerRadius = new(0, 15, 0, 8); outer.Children.Add(pin); inner.Margin = new(0, 7, 0, 0); return outer;
    }
    private UIElement DaySection(DateOnly date, DayPart part)
    {
        var layout = new Grid();
        var content = new StackPanel { Spacing = 4 }; content.Children.Add(Ui.PartIcon(part));
        foreach (var task in _vm.TasksOn(date, part)) content.Children.Add(TaskBlock(task));
        if (part == DayPart.Morning) foreach (var payment in _vm.PaymentsOn(date)) content.Children.Add(PaymentBlock(payment));
        Ui.Add(layout, Ui.Scroll(content));
        var plus = Ui.Button(Ui.Icon("\uE710", 13), _vm.CreateTaskCommand, new TaskLocation(date, part), $"Создать задачу: {date:dd.MM}, {PartName(part)}");
        plus.Height = 20; plus.Width = 24; plus.Padding = new(0); plus.HorizontalAlignment = HorizontalAlignment.Right; plus.VerticalAlignment = VerticalAlignment.Top;
        plus.Opacity = 0; plus.Background = Ui.Brush("#F5F5F5"); Ui.Add(layout, plus);
        var border = new Border { Child = layout, Background = Ui.Brush("#00FFFFFF"), BorderBrush = Ui.Brush("#F2F2F2"), BorderThickness = new(0, 0, 0, part == DayPart.Evening ? 0 : 1), Padding = new(0, 5, 0, 3) };
        border.PointerEntered += (_, _) => plus.Opacity = 1; border.PointerExited += (_, _) => plus.Opacity = 0;
        border.GotFocus += (_, _) => plus.Opacity = 1; border.LostFocus += (_, _) => plus.Opacity = 0;
        EnableDrop(border, date, part); return border;
    }
    private static string PartName(DayPart part) => part switch { DayPart.Morning => "утро", DayPart.Afternoon => "день", _ => "вечер" };
    private UIElement TaskBlock(PlannerTask task, bool compact = false)
    {
        var text = Ui.Text(task.Description, compact ? 10.5 : 12, Palette.TaskForegrounds[(int)task.Kind]);
        text.MaxLines = compact ? 1 : 2; text.TextTrimming = TextTrimming.CharacterEllipsis; text.Margin = new(0, 0, task.Completed ? 14 : 0, 0); if (task.Completed) text.Opacity = .48;
        var contents = new Grid(); contents.Children.Add(text);
        if (task.Completed) contents.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Originals/Completed.png")), Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
        var button = Ui.Button(contents, tooltip: task.Description); button.Padding = new(7, compact ? 3 : 5, 7, compact ? 3 : 5);
        button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Background = Ui.Brush(Palette.TaskBackgrounds[(int)task.Kind]); button.CornerRadius = new(4); button.CanDrag = true;
        button.Click += (_, _) => { if (DateTime.UtcNow > _suppressClicksUntil) _vm.ToggleTaskCommand.Execute(task.Id); };
        button.PointerEntered += (_, _) => { text.MaxLines = 0; text.TextTrimming = TextTrimming.None; };
        button.PointerExited += (_, _) => { text.MaxLines = compact ? 1 : 2; text.TextTrimming = TextTrimming.CharacterEllipsis; };
        button.DragStarting += (_, args) => { _suppressClicksUntil = DateTime.UtcNow.AddHours(1); args.Data.SetText(task.Id.ToString()); args.Data.RequestedOperation = DataPackageOperation.Move; };
        button.DropCompleted += (_, _) => _suppressClicksUntil = DateTime.UtcNow.AddMilliseconds(250);
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Редактировать", Icon = new FontIcon { Glyph = "\uE70F" }, Command = _vm.EditTaskCommand, CommandParameter = task.Id });
        menu.Items.Add(new MenuFlyoutItem { Text = "Удалить", Icon = new FontIcon { Glyph = "\uE74D" }, Command = _vm.DeleteTaskCommand, CommandParameter = task.Id });
        button.ContextFlyout = menu; return button;
    }
    private UIElement PaymentBlock(PaymentOccurrence payment, bool compact = false)
    {
        var layout = new Grid { ColumnSpacing = 5 }; layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); layout.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(layout, Ui.Text(payment.Completed ? "✓" : "✪", 14, payment.Completed ? "#FFFFFF" : "#FAF03D"));
        var title = Ui.Text(payment.Name, compact ? 10 : 11, "#FFFFFF", true); title.MaxLines = 1; title.TextTrimming = TextTrimming.CharacterEllipsis; Ui.Add(layout, title, 1);
        Ui.Add(layout, Ui.Text(FormatAmount(payment.Amount), compact ? 10 : 11, "#FAF03D", true), 2);
        var button = Ui.Button(layout, _vm.PaymentCommand, new PaymentToggle(payment.PaymentId, payment.Date), $"{payment.Name}: {payment.Amount:N2} ₽ — отметить оплату");
        button.Background = Ui.Brush(payment.Completed ? "#E4CAED" : Ui.Accent); button.Padding = new(5, 3, 5, 3); button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.CornerRadius = new(4); if (payment.Completed) layout.Opacity = .6; return button;
    }
    private UIElement HabitDots(DateOnly date, bool compact = false)
    {
        var habits = _vm.HabitsOn(date); var row = Ui.Grid(Math.Max(1, habits.Count), gap: 4);
        for (int i = 0; i < habits.Count; i++)
        {
            var h = habits[i]; var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            if (!compact) { var name = Ui.Text(h.Name, 9, Ui.Muted); name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxLines = 1; stack.Children.Add(name); }
            stack.Children.Add(Ui.Dot(h.Color, h.Completed, compact ? 7 : 14));
            var button = Ui.Button(stack, _vm.HabitCommand, new HabitToggle(h.HabitId, date), h.Name); button.Padding = new(compact ? 1 : 2); button.HorizontalAlignment = HorizontalAlignment.Stretch; Ui.Add(row, button, i);
        }
        row.MinHeight = compact ? 12 : 35; return row;
    }
    private UIElement BacklogCard()
    {
        var layout = new Grid { RowSpacing = 12 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); layout.RowDefinitions.Add(new() { Height = new(23) });
        var title = Ui.Text("НУЖНО РАСПРЕДЕЛИТЬ", 11, Ui.Muted, true); title.HorizontalAlignment = HorizontalAlignment.Center; Ui.Add(layout, title);
        int columns = _vm.Screen == PlannerScreen.Month ? 1 : 2;
        var tasks = Ui.Grid(columns, gap: 8); tasks.RowDefinitions.Clear(); int rowCount = (_vm.Backlog.Count + columns - 1) / columns;
        for (int i = 0; i < rowCount; i++) tasks.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < _vm.Backlog.Count; i++) Ui.Add(tasks, TaskBlock(_vm.Backlog[i]), i % columns, i / columns);
        tasks.VerticalAlignment = VerticalAlignment.Top; Ui.Add(layout, Ui.Scroll(tasks), row: 1);
        var plus = Ui.Button(Ui.Icon("\uE710", 14), _vm.CreateTaskCommand, new TaskLocation(null), "Добавить нераспределённую задачу");
        plus.HorizontalAlignment = HorizontalAlignment.Stretch; plus.Padding = new(0); plus.Background = Ui.Brush("#F5F5F5"); plus.Opacity = 0; Ui.Add(layout, plus, row: 2);
        var card = Ui.Card(layout); card.PointerEntered += (_, _) => plus.Opacity = 1; card.PointerExited += (_, _) => plus.Opacity = 0;
        card.GotFocus += (_, _) => plus.Opacity = 1; card.LostFocus += (_, _) => plus.Opacity = 0; EnableDrop(card, null); return card;
    }
    private UIElement MatrixCard()
    {
        var layout = new Grid { RowSpacing = 12 }; layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); layout.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var title = Ui.Button(Ui.Text(_vm.NotesVisible ? "ЗАМЕТКИ" : "МАТРИЦА", 11, Ui.Muted, true), _vm.ToggleNotesCommand, tooltip: "Переключить матрицу и заметки");
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Padding = new(0); Ui.Add(layout, title);
        if (_vm.NotesVisible)
        {
            var notes = new TextBox { Text = _vm.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new(0), Background = Ui.Brush("#FFFFFF"), PlaceholderText = _vm.Screen == PlannerScreen.Month ? "Заметки этого месяца…" : "Заметки этой недели…",
                VerticalAlignment = VerticalAlignment.Stretch, FontSize = 12, Padding = new(0), Foreground = Ui.Brush(Ui.Ink) };
            notes.TextChanged += (_, _) => _vm.Notes = notes.Text; Ui.Add(layout, notes, row: 1);
        }
        else
        {
            var stack = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var cross = Ui.Grid(3, 3, 0); cross.Width = 150; cross.Height = 150; cross.ColumnDefinitions[1].Width = new(1); cross.RowDefinitions[1].Height = new(1);
            var vertical = new Border { Background = Ui.Brush("#F1F1F1") }; Grid.SetRowSpan(vertical, 3); Ui.Add(cross, vertical, 1);
            Ui.Add(cross, new Border { Background = Ui.Brush("#F1F1F1") }, 0, 1, 3);
            var counts = _vm.Matrix; int[] positions = [0, 2, 1, 3];
            string[] descriptions = ["Срочное важное", "Срочное неважное", "Несрочное важное", "Несрочное неважное"];
            for (int i = 0; i < 4; i++)
            {
                var number = Ui.Text(counts.Scheduled[i].ToString(), 21, Palette.TaskForegrounds[i]); number.HorizontalAlignment = HorizontalAlignment.Center;
                var pill = new Border { Background = Ui.Brush(Palette.TaskBackgrounds[i]), CornerRadius = new(7), Width = 43, Height = 43, Child = number }; ToolTipService.SetToolTip(pill, descriptions[i]);
                Ui.Add(cross, pill, (positions[i] % 2) * 2, (positions[i] / 2) * 2);
            }
            stack.Children.Add(cross);
            var backlog = new StackPanel { Spacing = 6 }; var caption = Ui.Text("Без времени", 10, Ui.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center; backlog.Children.Add(caption);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            for (int i = 0; i < 4; i++)
            {
                var number = Ui.Text(counts.Unscheduled[i].ToString(), 11, Palette.TaskForegrounds[i]); number.HorizontalAlignment = HorizontalAlignment.Center;
                row.Children.Add(new Border { Background = Ui.Brush(Palette.TaskBackgrounds[i]), CornerRadius = new(3), Width = 23, Height = 23, Child = number });
            }
            backlog.Children.Add(row); stack.Children.Add(backlog); Ui.Add(layout, stack, row: 1);
        }
        return Ui.Card(layout);
    }
    private void EnableDrop(UIElement target, DateOnly? date, DayPart? part = null)
    {
        target.AllowDrop = true;
        target.DragOver += (_, args) => { if (args.DataView.Contains(StandardDataFormats.Text)) { args.AcceptedOperation = DataPackageOperation.Move; args.DragUIOverride.Caption = date is null ? "Нужно распределить" : $"{date:dd.MM}"; args.Handled = true; } };
        target.Drop += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (args.DataView.Contains(StandardDataFormats.Text) && Guid.TryParse(await args.DataView.GetTextAsync(), out var id))
                { _suppressClicksUntil = DateTime.UtcNow.AddMilliseconds(250); _vm.MoveTaskCommand.Execute(new TaskMove(id, date, part)); }
                args.Handled = true;
            }
            catch (Exception ex) { await ErrorDialog(ex.Message); }
            finally { deferral.Complete(); }
        };
    }
    private bool OwnsDialogs => XamlRoot is not null && _floating == App.FloatingOwnsDialogs;
    private async void EditTask(object? sender, TaskEditRequest request)
    {
        if (!OwnsDialogs) return;
        var description = new TextBox { Text = request.Description, Header = "Описание задачи", PlaceholderText = "Что нужно сделать?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96, MaxHeight = 250, MaxLength = 10000 };
        var important = new ToggleSwitch { Header = "Важность", OnContent = "Важное", OffContent = "Неважное", IsOn = request.Important };
        var urgent = new ToggleSwitch { Header = "Срочность", OnContent = "Срочное", OffContent = "Несрочное", IsOn = request.Urgent };
        var validation = Ui.Text("", 12, "#BD3026"); var contents = new StackPanel { Spacing = 16, MinWidth = 340 }; contents.Children.Add(description);
        var toggles = Ui.Grid(2); Ui.Add(toggles, important); Ui.Add(toggles, urgent, 1); contents.Children.Add(toggles); contents.Children.Add(validation);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ElementTheme.Light, Title = request.Id is null ? "Новая задача" : "Редактировать задачу", Content = contents,
            PrimaryButtonText = "Сохранить", CloseButtonText = "Отмена", DefaultButton = ContentDialogButton.Primary };
        dialog.PrimaryButtonClick += (_, args) => { if (string.IsNullOrWhiteSpace(description.Text)) { validation.Text = "Введите описание задачи"; args.Cancel = true; } };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.SaveTask(request, description.Text, important.IsOn, urgent.IsOn);
    }
    private async void ShowError(object? sender, string message) { if (OwnsDialogs) await ErrorDialog(message); }
    private async Task ErrorDialog(string message)
    {
        if (XamlRoot is null) return;
        try { await new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ElementTheme.Light, Title = "Не удалось выполнить действие", Content = message, CloseButtonText = "Понятно" }.ShowAsync(); }
        catch (InvalidOperationException) { }
    }
}
