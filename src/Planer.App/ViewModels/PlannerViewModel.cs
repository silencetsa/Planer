using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Planer.Core;

namespace Planer_App.ViewModels;

public enum PlannerScreen { Week, Month, Year, Habits, Payments }
public sealed record TaskLocation(DateOnly? Date, DayPart Part = DayPart.Evening);
public sealed record TaskEditRequest(Guid? Id, string Description, bool Important, bool Urgent, TaskLocation Location);
public sealed record TaskMove(Guid Id, DateOnly? Date, DayPart? Part = null);
public sealed record HabitToggle(Guid Id, DateOnly Date);
public sealed record PaymentToggle(Guid Id, DateOnly Date);
public sealed record MoodToggle(DateOnly Date, Mood Mood);

/// <summary>Presentation state and commands; views do not mutate domain entities.</summary>
public sealed class PlannerViewModel : ObservableObject, IDisposable
{
    private readonly PlannerEngine _engine;
    private DateOnly _date;
    private PlannerScreen _screen;
    private DateOnly _observedToday;
    private bool _weekNotes, _monthNotes;
    public static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    public DateOnly Today => _engine.Today;
    public DateOnly SelectedDate { get => _date; private set => SetProperty(ref _date, value); }
    public PlannerScreen Screen { get => _screen; private set => SetProperty(ref _screen, value); }
    public long Revision { get; private set; }
    public bool NotesVisible => Screen == PlannerScreen.Month ? _monthNotes : _weekNotes;
    public DateOnly First => Screen == PlannerScreen.Month ? new(SelectedDate.Year, SelectedDate.Month, 1) : Dates.Monday(SelectedDate);
    public DateOnly Last => Screen == PlannerScreen.Month ? First.AddMonths(1).AddDays(-1) : First.AddDays(6);
    public string Period => Screen switch
    {
        PlannerScreen.Week => $"{First.ToString("d MMM", Russian)} — {Last.ToString("d MMM", Russian)}",
        PlannerScreen.Month => SelectedDate.ToString("MMMM yyyy", Russian),
        PlannerScreen.Year => $"{SelectedDate.Year}",
        PlannerScreen.Habits => "Привычки",
        _ => "Платежи"
    };
    public string PeriodKey => Screen switch
    {
        PlannerScreen.Month => Dates.MonthKey(SelectedDate),
        PlannerScreen.Year => Dates.YearKey(SelectedDate),
        _ => Dates.WeekKey(SelectedDate)
    };
    public string Goal { get => _engine.Goal(PeriodKey); set => Run(() => _engine.SetGoal(PeriodKey, value)); }
    public string ParentGoal => Screen == PlannerScreen.Week ? _engine.Goal(Dates.MonthKey(SelectedDate)) : _engine.Goal(Dates.YearKey(SelectedDate));
    public string Notes { get => _engine.Note(PeriodKey); set => Run(() => _engine.SetNote(PeriodKey, value)); }
    public IReadOnlyList<PlannerTask> Backlog => _engine.Backlog.ToList();
    public IReadOnlyList<Habit> Habits => _engine.State.Habits.OrderBy(h => h.Ended is not null).ThenByDescending(h => h.Created).ThenByDescending(h => h.Order).ThenByDescending(h => h.Id).ToList();
    public IReadOnlyList<Payment> Payments => _engine.State.Payments.Where(p => p.DeletedFrom is null).ToList();
    public MatrixCounts Matrix => _engine.Matrix(First, Last);
    public int YearMode { get; set; }
    public bool ShowCompletedPayments { get; set; }
    public event EventHandler<TaskEditRequest>? TaskEditorRequested;
    public event EventHandler<string>? ErrorRequested;
    public event EventHandler? FloatRequested;
    public event EventHandler? HabitEditorRequested;
    public event EventHandler<Payment?>? PaymentEditorRequested;
    public event EventHandler<Guid>? HabitFinishRequested;
    public event EventHandler<Guid>? PaymentDeleteRequested;
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IRelayCommand<PlannerScreen> NavigateCommand { get; }
    public IRelayCommand<DateOnly> OpenWeekCommand { get; }
    public IRelayCommand<DateOnly> OpenMonthCommand { get; }
    public IRelayCommand ParentCommand { get; }
    public IRelayCommand ToggleNotesCommand { get; }
    public IRelayCommand<TaskLocation> CreateTaskCommand { get; }
    public IRelayCommand<Guid> EditTaskCommand { get; }
    public IRelayCommand<Guid> ToggleTaskCommand { get; }
    public IRelayCommand<Guid> DeleteTaskCommand { get; }
    public IRelayCommand<TaskMove> MoveTaskCommand { get; }
    public IRelayCommand<MoodToggle> MoodCommand { get; }
    public IRelayCommand<HabitToggle> HabitCommand { get; }
    public IRelayCommand<PaymentToggle> PaymentCommand { get; }
    public IRelayCommand FloatCommand { get; }
    public IRelayCommand CreateHabitCommand { get; }
    public IRelayCommand<Guid> FinishHabitCommand { get; }
    public IRelayCommand CreatePaymentCommand { get; }
    public IRelayCommand<Guid> EditPaymentCommand { get; }
    public IRelayCommand<Guid> FinishPaymentCommand { get; }
    public IRelayCommand<Guid> DeletePaymentCommand { get; }

    public PlannerViewModel(PlannerEngine engine)
    {
        _engine = engine; _date = Today; _observedToday = Today; _engine.Changed += EngineChanged;
        PreviousCommand = new RelayCommand(() => Shift(-1)); NextCommand = new RelayCommand(() => Shift(1));
        TodayCommand = new RelayCommand(() => Navigate(PlannerScreen.Week, Today));
        NavigateCommand = new RelayCommand<PlannerScreen>(screen => Navigate(screen, SelectedDate));
        OpenWeekCommand = new RelayCommand<DateOnly>(date => Navigate(PlannerScreen.Week, date));
        OpenMonthCommand = new RelayCommand<DateOnly>(date => Navigate(PlannerScreen.Month, date));
        ParentCommand = new RelayCommand(() => Navigate(Screen == PlannerScreen.Week ? PlannerScreen.Month : PlannerScreen.Year, SelectedDate));
        ToggleNotesCommand = new RelayCommand(() => { if (Screen == PlannerScreen.Month) _monthNotes = !_monthNotes; else _weekNotes = !_weekNotes; Refresh(); });
        CreateTaskCommand = new RelayCommand<TaskLocation>(location => { if (location is not null) TaskEditorRequested?.Invoke(this, new(null, "", false, false, location)); });
        EditTaskCommand = new RelayCommand<Guid>(id => { var t = _engine.State.Tasks.FirstOrDefault(t => t.Id == id); if (t is not null) TaskEditorRequested?.Invoke(this, new(id, t.Description, t.Important, t.Urgent, new(t.Date, t.Part))); });
        ToggleTaskCommand = new RelayCommand<Guid>(id => Run(() => _engine.ToggleTask(id)));
        DeleteTaskCommand = new RelayCommand<Guid>(id => Run(() => _engine.DeleteTask(id)));
        MoveTaskCommand = new RelayCommand<TaskMove>(move => { if (move is not null && _engine.State.Tasks.Any(t => t.Id == move.Id)) Run(() => _engine.MoveTask(move.Id, move.Date, move.Part)); });
        MoodCommand = new RelayCommand<MoodToggle>(toggle => { if (toggle is not null) Run(() => _engine.ToggleMood(toggle.Date, toggle.Mood)); });
        HabitCommand = new RelayCommand<HabitToggle>(toggle => { if (toggle is not null) Run(() => _engine.ToggleHabit(toggle.Id, toggle.Date)); });
        PaymentCommand = new RelayCommand<PaymentToggle>(toggle => { if (toggle is not null) Run(() => _engine.TogglePayment(toggle.Id, toggle.Date)); });
        FloatCommand = new RelayCommand(() => FloatRequested?.Invoke(this, EventArgs.Empty));
        CreateHabitCommand = new RelayCommand(() => HabitEditorRequested?.Invoke(this, EventArgs.Empty));
        FinishHabitCommand = new RelayCommand<Guid>(id => HabitFinishRequested?.Invoke(this, id));
        CreatePaymentCommand = new RelayCommand(() => PaymentEditorRequested?.Invoke(this, null));
        EditPaymentCommand = new RelayCommand<Guid>(id => PaymentEditorRequested?.Invoke(this, Payments.FirstOrDefault(p => p.Id == id)));
        FinishPaymentCommand = new RelayCommand<Guid>(id => Run(() => _engine.FinishPayment(id)));
        DeletePaymentCommand = new RelayCommand<Guid>(id => PaymentDeleteRequested?.Invoke(this, id));
    }
    public void Navigate(PlannerScreen screen, DateOnly date) { Screen = screen; SelectedDate = date; Refresh(); }
    private void Shift(int direction) => Run(() =>
    {
        SelectedDate = Screen switch { PlannerScreen.Month => SelectedDate.AddMonths(direction), PlannerScreen.Year => SelectedDate.AddYears(direction), _ => SelectedDate.AddDays(direction * 7) };
        Refresh();
    });
    public void Tick()
    {
        if (_observedToday == Today) return; _observedToday = Today;
        if (!_engine.ReconcileWeek()) Refresh();
    }
    public void Refresh() { Revision++; OnPropertyChanged(nameof(Revision)); }
    private void EngineChanged(object? sender, EventArgs args) => Refresh();
    public void Run(Action action) { try { action(); } catch (Exception ex) { ErrorRequested?.Invoke(this, ex.Message); } }
    public void SaveTask(TaskEditRequest request, string description, bool important, bool urgent) => Run(() =>
    {
        if (request.Id is { } id) _engine.EditTask(id, description, important, urgent);
        else _engine.AddTask(description, important, urgent, request.Location.Date, request.Location.Part);
    });
    public void AddHabit(string name, string color) => Run(() => _engine.AddHabit(name, color));
    public void FinishHabit(Guid id) => Run(() => _engine.FinishHabit(id));
    public void SavePayment(Guid? id, string name, decimal amount, DateOnly start, Repeat repeat, DateOnly? end) => Run(() =>
    {
        if (id is { } paymentId) _engine.EditPayment(paymentId, name, amount, start, repeat, end);
        else _engine.AddPayment(name, amount, start, repeat, end);
    });
    public void DeletePayment(Guid id) => Run(() => _engine.DeletePayment(id));
    public IReadOnlyList<PlannerTask> TasksOn(DateOnly date, DayPart? part = null) => _engine.TasksOn(date, part).ToList();
    public IReadOnlyList<HabitIndicator> HabitsOn(DateOnly date) => _engine.HabitsOn(date).ToList();
    public IReadOnlyList<PaymentOccurrence> PaymentsOn(DateOnly date) => _engine.PaymentsOn(date).ToList();
    public Mood? MoodOn(DateOnly date) => _engine.MoodOn(date);
    public int HabitSuccess(Guid id) => _engine.State.HabitChecks.Count(c => c.HabitId == id);
    public PaymentRevision? PaymentRevision(Payment p) => p.RevisionOn(Today);
    public void Dispose() => _engine.Changed -= EngineChanged;
}
