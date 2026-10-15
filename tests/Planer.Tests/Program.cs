using System.Text.Json;
using Planer.Core;
using Planer.Data;

int passed = 0, failed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"FAIL {name}: {ex}"); failed++; }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; actual {actual}");
}
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
(PlannerEngine Engine, TestClock Clock, MemoryStore Store) Setup(DateOnly? date = null)
{
    var clock = new TestClock { Today = date ?? new(2026, 10, 13) };
    var store = new MemoryStore();
    return (new PlannerEngine(store, clock), clock, store);
}

Test("Week starts Monday, including Sunday and year boundaries", () =>
{
    Equal(new DateOnly(2026, 10, 12), Dates.Monday(new(2026, 10, 18)));
    Equal(new DateOnly(2025, 12, 29), Dates.Monday(new(2026, 1, 1)));
});
Test("Rollover moves overdue open tasks, preserves completed and future tasks", () =>
{
    var (e, clock, _) = Setup(new(2026, 10, 11));
    var open = e.AddTask("open", true, true, clock.Today);
    var done = e.AddTask("done", false, true, clock.Today); e.ToggleTask(done.Id);
    var future = e.AddTask("future", true, false, new(2026, 10, 19));
    clock.Today = new(2026, 10, 12); e.ReconcileWeek();
    Equal<DateOnly?>(null, open.Date); Equal<DateOnly?>(new(2026, 10, 11), done.Date); Equal<DateOnly?>(new(2026, 10, 19), future.Date);
});
Test("Manual backdating survives same-week refresh and restart", () =>
{
    var (e, clock, store) = Setup();
    var t = e.AddTask("remembered task", true, false, null);
    e.MoveTask(t.Id, new(2026, 10, 7)); e.ReconcileWeek();
    e = new PlannerEngine(store, clock);
    Equal<DateOnly?>(new(2026, 10, 7), e.State.Tasks.Single().Date);
    clock.Today = new(2026, 10, 19); e.ReconcileWeek(); Equal<DateOnly?>(null, e.State.Tasks.Single().Date);
});
Test("Rollover after multiple closed weeks and persisted completion", () =>
{
    var (e, clock, store) = Setup(new(2026, 9, 1));
    var t = e.AddTask("old", false, false, clock.Today);
    clock.Today = new(2026, 10, 13); e = new PlannerEngine(store, clock);
    Equal(1, e.Backlog.Count()); e.MoveTask(t.Id, new(2026, 9, 1)); e.ToggleTask(t.Id);
    clock.Today = new(2026, 11, 1); e = new PlannerEngine(store, clock); Equal(0, e.Backlog.Count());
});
Test("Monthly drag keeps part, backlog defaults to evening", () =>
{
    var (e, _, _) = Setup();
    var t = e.AddTask("task", false, false, new(2026, 10, 13), DayPart.Morning);
    e.MoveTask(t.Id, new(2026, 10, 14)); Equal(DayPart.Morning, t.Part);
    e.MoveTask(t.Id, null); e.MoveTask(t.Id, new(2026, 10, 15)); Equal(DayPart.Evening, t.Part);
});
Test("Matrix distinguishes current period and global backlog, includes done", () =>
{
    var (e, _, _) = Setup();
    var t = e.AddTask("done", true, true, new(2026, 10, 13)); e.ToggleTask(t.Id);
    e.AddTask("later", true, true, new(2026, 11, 1)); e.AddTask("backlog", false, false, null);
    var m = e.Matrix(new(2026, 10, 12), new(2026, 10, 18)); Equal(1, m.Scheduled[0]); Equal(1, m.Unscheduled[3]);
});
Test("Mood is exclusive, replaceable, and removable", () =>
{
    var (e, clock, _) = Setup();
    e.ToggleMood(clock.Today, Mood.Happy); e.ToggleMood(clock.Today, Mood.Sad); Equal<Mood?>(Mood.Sad, e.MoodOn(clock.Today));
    e.ToggleMood(clock.Today, Mood.Sad); Equal<Mood?>(null, e.MoodOn(clock.Today));
});
Test("At most 3 active habits; finishing frees a slot without reactivation", () =>
{
    var (e, clock, _) = Setup();
    var first = e.AddHabit("first", Palette.Elements[0]); e.AddHabit("second", Palette.Elements[1]); e.AddHabit("third", Palette.Elements[2]);
    Throws<InvalidOperationException>(() => e.AddHabit("fourth", Palette.Elements[3]));
    e.FinishHabit(first.Id); e.AddHabit("new", Palette.Elements[3]); Equal(3, e.HabitsOn(clock.Today).Count());
    Throws<InvalidOperationException>(() => e.FinishHabit(first.Id));
});
Test("Habit history excludes finish day, preserves earlier success and active days", () =>
{
    var (e, clock, _) = Setup(new(2026, 10, 12));
    var h = e.AddHabit("walk", Palette.Elements[3]); e.ToggleHabit(h.Id, clock.Today);
    clock.Today = new(2026, 10, 13); e.ToggleHabit(h.Id, clock.Today); e.ToggleHabit(h.Id, new(2026, 10, 14)); e.FinishHabit(h.Id);
    Equal(0, e.HabitsOn(clock.Today).Count()); Equal(true, e.HabitsOn(new(2026, 10, 12)).Single().Completed);
    Equal(1, e.State.HabitChecks.Count); Equal(1, h.ActiveDays(clock.Today));
});
Test("Monthly and yearly payments clamp to last day and restore anchor", () =>
{
    var (e, _, _) = Setup();
    var p = e.AddPayment("monthly", 100, new(2026, 1, 31), Repeat.Monthly, null);
    Equal(true, p.OccursOn(new(2026, 2, 28))); Equal(true, p.OccursOn(new(2026, 3, 31))); Equal(false, p.OccursOn(new(2026, 3, 28)));
    var leap = e.AddPayment("yearly", 100, new(2024, 2, 29), Repeat.Yearly, null);
    Equal(true, leap.OccursOn(new(2025, 2, 28))); Equal(true, leap.OccursOn(new(2028, 2, 29)));
});
Test("Payment end is inclusive; one occurrence completion does not finish series", () =>
{
    var (e, _, _) = Setup();
    var p = e.AddPayment("bill", 805, new(2026, 10, 6), Repeat.Weekly, new(2026, 10, 13));
    Equal(true, p.OccursOn(new(2026, 10, 13))); Equal(false, p.OccursOn(new(2026, 10, 20)));
    e.TogglePayment(p.Id, new(2026, 10, 6)); Equal(false, e.PaymentsOn(new(2026, 10, 13)).Single().Completed);
});
Test("Payment edit changes today and future, preserves historical amount and check", () =>
{
    var (e, _, _) = Setup();
    var p = e.AddPayment("old", 100, new(2026, 10, 6), Repeat.Weekly, null); e.TogglePayment(p.Id, new(2026, 10, 6));
    e.EditPayment(p.Id, "new", 200, new(2026, 10, 6), Repeat.Weekly, null);
    Equal(100m, e.PaymentsOn(new(2026, 10, 6)).Single().Amount); Equal(true, e.PaymentsOn(new(2026, 10, 6)).Single().Completed);
    Equal(200m, e.PaymentsOn(new(2026, 10, 13)).Single().Amount);
    e.DeletePayment(p.Id); Equal(1, e.PaymentsOn(new(2026, 10, 6)).Count()); Equal(0, e.PaymentsOn(new(2026, 10, 13)).Count());
});
Test("Finishing a series today includes today's payment", () =>
{
    var (e, clock, _) = Setup();
    var p = e.AddPayment("bill", 100, new(2026, 10, 6), Repeat.Weekly, null); e.FinishPayment(p.Id);
    Equal(true, p.OccursOn(clock.Today)); Equal(false, p.IsActive(clock.Today)); Equal(false, p.OccursOn(clock.Today.AddDays(7)));
});
Test("Finishing an unstarted series keeps it in completed payments with today's end date", () =>
{
    var (e, clock, store) = Setup();
    var p = e.AddPayment("future", 100, clock.Today.AddDays(10), Repeat.Monthly, null);
    e.FinishPayment(p.Id); e = new PlannerEngine(store, clock); p = e.State.Payments.Single();
    Equal<DateOnly?>(null, p.DeletedFrom); Equal<DateOnly?>(clock.Today, p.RevisionOn(clock.Today)!.End);
    Equal(false, p.IsActive(clock.Today)); Equal(false, p.OccursOn(clock.Today.AddDays(10)));
});
Test("Stopped future payment can be renamed while retaining its end date", () =>
{
    var (e, clock, _) = Setup();
    var p = e.AddPayment("future", 100, clock.Today.AddDays(10), Repeat.Monthly, null);
    e.FinishPayment(p.Id); e.EditPayment(p.Id, "renamed", 200, clock.Today.AddDays(10), Repeat.Monthly, clock.Today);
    Equal("renamed", p.RevisionOn(clock.Today)!.Name); Equal(false, p.IsActive(clock.Today));
});
Test("Validation rejects invalid task, amount, dates and overlong habit", () =>
{
    var (e, clock, _) = Setup();
    Throws<ArgumentException>(() => e.AddTask(" ", false, false, null));
    Throws<ArgumentException>(() => e.AddHabit(new string('a', 14), Palette.Elements[0]));
    Throws<ArgumentException>(() => e.AddPayment("bill", -1, clock.Today, Repeat.None, null));
    Throws<ArgumentException>(() => e.AddPayment("bill", 1, clock.Today, Repeat.None, clock.Today.AddDays(-1)));
});
Test("SQLite restart roundtrip: tasks, habits, moods, payment history, notes and goals", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), "PlanerTests", Guid.NewGuid().ToString());
    var path = Path.Combine(testDir, "planner.db");
    var clock = new TestClock { Today = new(2026, 10, 13) };
    var e = new PlannerEngine(new SqlitePlannerStore(path), clock);
    var t = e.AddTask("persistent", true, false, clock.Today); e.ToggleTask(t.Id);
    var h = e.AddHabit("read", Palette.Elements[2]); e.ToggleHabit(h.Id, clock.Today);
    var p = e.AddPayment("bill", 123.45m, clock.Today, Repeat.Monthly, null); e.TogglePayment(p.Id, clock.Today);
    e.ToggleMood(clock.Today, Mood.Happy); e.SetGoal(Dates.WeekKey(clock.Today), "week goal");
    e.SetNote(Dates.WeekKey(clock.Today), "weekly note"); e.SetNote(Dates.MonthKey(clock.Today), "monthly note");
    e = new PlannerEngine(new SqlitePlannerStore(path), clock);
    Equal(true, e.State.Tasks.Single().Completed); Equal(true, e.HabitsOn(clock.Today).Single().Completed);
    Equal(true, e.PaymentsOn(clock.Today).Single().Completed); Equal<Mood?>(Mood.Happy, e.MoodOn(clock.Today));
    Equal("weekly note", e.Note(Dates.WeekKey(clock.Today))); Equal("monthly note", e.Note(Dates.MonthKey(clock.Today)));
    e.DeleteTask(t.Id); e = new PlannerEngine(new SqlitePlannerStore(path), clock); Equal(0, e.State.Tasks.Count);
    // Only this uniquely generated test directory is removed, never a user data directory.
    Directory.Delete(testDir, recursive: true);
});
Test("Failed persistence rolls in-memory state back", () =>
{
    var (e, _, store) = Setup(); store.FailSave = true;
    Throws<IOException>(() => e.AddTask("must not survive", false, false, null)); Equal(0, e.State.Tasks.Count);
});
Test("Legacy habit colors use the element palette without changing stored history", () =>
{
    var (e, clock, store) = Setup();
    var habit = new Habit { Name = "old habit", Color = "#0DBA30", Created = clock.Today.AddDays(-3), Ended = clock.Today };
    var state = store.Load(); state.Habits.Add(habit);
    state.HabitChecks.Add(new() { HabitId = habit.Id, Date = clock.Today.AddDays(-1) }); store.Save(state);
    e = new PlannerEngine(store, clock);
    var historical = e.HabitsOn(clock.Today.AddDays(-1)).Single();
    Equal("#66E37E", historical.Color); Equal(true, historical.Completed);
    Equal(0, e.HabitsOn(clock.Today).Count());
    e.SetNote(Dates.WeekKey(clock.Today), "unrelated save");
    var saved = store.Load(); Equal("#0DBA30", saved.Habits.Single().Color);
    Equal(habit.Created, saved.Habits.Single().Created); Equal(habit.Ended, saved.Habits.Single().Ended);
    Equal(clock.Today.AddDays(-1), saved.HabitChecks.Single().Date);
});

Test("Day layout lends spare space only from empty sections", () =>
{
    var h = DaySectionLayout.Allocate(300, [160, 25, 80], [true, false, true]);
    Equal(160d, h[0]); Equal(40d, h[1]); Equal(100d, h[2]);
});
Test("Day layout can borrow from two empty sections and preserves total height", () =>
{
    var h = DaySectionLayout.Allocate(300, [220, 25, 25], [true, false, false]);
    Equal(220d, h[0]); Equal(40d, h[1]); Equal(40d, h[2]); Equal(300d, h.Sum());
});
Test("Day layout shares limited spare space between two overflowing sections", () =>
{
    var h = DaySectionLayout.Allocate(300, [210, 25, 180], [true, false, true]);
    Equal(25d, h[1]); Equal(300d, h.Sum());
    if (h[0] <= 100 || h[2] <= 100 || h[0] >= 210 || h[2] >= 180) throw new Exception("Incorrect spare-space allocation");
});
Test("Day layout retains equal sections when all are occupied or already fit", () =>
{
    Equal(true, DaySectionLayout.Allocate(300, [200, 40, 20], [true, true, true]).All(h => h == 100));
    Equal(true, DaySectionLayout.Allocate(300, [80, 25, 80], [true, false, true]).All(h => h == 100));
});

Test("Recurring tasks use weekly and fortnightly weekdays without a future horizon", () =>
{
    var (e, clock, _) = Setup(new(2026, 10, 12));
    e.AddTask("weekly", true, false, clock.Today, DayPart.Morning, Repeat.Weekly);
    e.AddTask("fortnightly", false, true, clock.Today, DayPart.Afternoon, Repeat.Fortnightly);
    Equal(1, e.TasksOn(clock.Today.AddDays(7)).Count()); Equal(2, e.TasksOn(clock.Today.AddDays(14)).Count());
    var distant = new DateOnly(2090, 1, 1); e.EnsureTaskRange(distant, distant.AddDays(6));
    if (e.State.Tasks.Count > 8) throw new Exception("Opening a distant week materialized intervening decades");
});
Test("Monthly and yearly task anchors restore the 31st and leap day", () =>
{
    var (e, _, _) = Setup(new(2028, 1, 31));
    e.AddTask("month", false, false, new(2028, 1, 31), repeat: Repeat.Monthly);
    Equal(1, e.TasksOn(new(2028, 2, 29)).Count()); Equal(1, e.TasksOn(new(2028, 3, 31)).Count());
    Equal(0, e.TasksOn(new(2028, 3, 29)).Count());
    e.AddTask("year", true, false, new(2028, 2, 29), repeat: Repeat.Yearly);
    Equal("year", e.TasksOn(new(2029, 2, 28)).Single(t => t.Description == "year").Description);
    Equal(1, e.TasksOn(new(2032, 2, 29)).Count(t => t.Description == "year"));
});
Test("An unassigned recurring task starts only when assigned, defaulting to evening", () =>
{
    var (e, clock, _) = Setup(); var task = e.AddTask("pending", false, false, null, repeat: Repeat.Weekly);
    e.EnsureTaskRange(clock.Today, clock.Today.AddYears(5)); Equal(1, e.State.Tasks.Count); Equal(0, e.State.TaskSeries.Count);
    e.MoveTask(task.Id, clock.Today); Equal(DayPart.Evening, task.Part);
    Equal(1, e.TasksOn(clock.Today.AddDays(7)).Count()); Equal(0, e.Backlog.Count());
});
Test("Completion and text edits affect only one recurring occurrence", () =>
{
    var (e, clock, _) = Setup(); var t = e.AddTask("original", true, false, clock.Today, repeat: Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.ToggleTask(t.Id); e.EditTask(t.Id, "edited", false, true);
    Equal(false, next.Completed); Equal("original", next.Description); Equal("edited", t.Description);
    Equal("original", e.TasksOn(clock.Today.AddDays(14)).Single().Description);
});
Test("Moving one occurrence neither moves nor regenerates other occurrences after restart", () =>
{
    var (e, clock, store) = Setup(); var first = e.AddTask("move one", false, false, clock.Today, DayPart.Morning, Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.MoveTask(next.Id, clock.Today.AddDays(8));
    e = new PlannerEngine(store, clock);
    Equal(0, e.TasksOn(clock.Today.AddDays(7)).Count()); Equal(next.Id, e.TasksOn(clock.Today.AddDays(8)).Single().Id);
    Equal(DayPart.Morning, e.TasksOn(clock.Today.AddDays(8)).Single().Part);
    Equal(first.Id, e.TasksOn(clock.Today).Single().Id); Equal(1, e.TasksOn(clock.Today.AddDays(14)).Count());
});
Test("Moving future chain preserves earlier history and occurrence completion", () =>
{
    var (e, clock, _) = Setup(); var first = e.AddTask("chain", true, false, clock.Today, DayPart.Morning, Repeat.Weekly); e.ToggleTask(first.Id);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); var third = e.TasksOn(clock.Today.AddDays(14)).Single(); e.ToggleTask(third.Id);
    e.MoveTask(next.Id, clock.Today.AddDays(9), DayPart.Afternoon, TaskMoveScope.ThisAndFuture);
    Equal<DateOnly?>(clock.Today, first.Date); Equal(true, first.Completed); Equal(0, e.TasksOn(clock.Today.AddDays(7)).Count());
    var moved = e.TasksOn(clock.Today.AddDays(16)).Single(); Equal(third.Id, moved.Id); Equal(true, moved.Completed); Equal(DayPart.Afternoon, moved.Part);
    Equal(1, e.TasksOn(clock.Today.AddDays(23)).Count()); Equal(0, e.TasksOn(clock.Today.AddDays(21)).Count());
});
Test("Reanchoring monthly future tasks uses the new day without changing previous months", () =>
{
    var (e, _, _) = Setup(new(2026, 1, 31)); var first = e.AddTask("monthly", false, false, new(2026, 1, 31), repeat: Repeat.Monthly);
    var next = e.TasksOn(new(2026, 2, 28)).Single(); e.MoveTask(next.Id, new(2026, 2, 27), scope: TaskMoveScope.ThisAndFuture);
    Equal(first.Id, e.TasksOn(new(2026, 1, 31)).Single().Id); Equal(0, e.TasksOn(new(2026, 3, 31)).Count());
    Equal(1, e.TasksOn(new(2026, 3, 27)).Count());
});
Test("Deleting a recurring task removes current and all unseen future instances, preserving earlier ones", () =>
{
    var (e, clock, store) = Setup(); var first = e.AddTask("delete", false, false, clock.Today, repeat: Repeat.Weekly); e.ToggleTask(first.Id);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.TasksOn(clock.Today.AddDays(14)).ToList(); e.DeleteTask(next.Id);
    e = new PlannerEngine(store, clock); Equal(first.Id, e.TasksOn(clock.Today).Single().Id); Equal(true, e.TasksOn(clock.Today).Single().Completed);
    Equal(0, e.Matrix(clock.Today.AddDays(7), clock.Today.AddYears(2)).Scheduled.Sum());
});
Test("Deleting before a changed segment stops that segment too", () =>
{
    var (e, clock, _) = Setup(); var first = e.AddTask("split", false, false, clock.Today, repeat: Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.MoveTask(next.Id, clock.Today.AddDays(8), scope: TaskMoveScope.ThisAndFuture);
    e.DeleteTask(first.Id); Equal(0, e.Matrix(clock.Today, clock.Today.AddYears(1)).Scheduled.Sum());
});
Test("Rollover materializes missed recurring tasks once, keeping completed history and future dates", () =>
{
    var (e, clock, store) = Setup(new(2026, 10, 12)); var first = e.AddTask("roll", false, false, clock.Today, repeat: Repeat.Weekly); e.ToggleTask(first.Id);
    var future = e.TasksOn(new(2026, 11, 9)).Single(); clock.Today = new(2026, 11, 2); e = new PlannerEngine(store, clock);
    Equal(2, e.Backlog.Count()); Equal(1, e.TasksOn(new(2026, 10, 12)).Count()); Equal(future.Id, e.TasksOn(new(2026, 11, 9)).Single().Id);
    Equal(0, e.TasksOn(new(2026, 10, 19)).Count()); Equal(0, e.TasksOn(new(2026, 10, 26)).Count());
    e = new PlannerEngine(store, clock); Equal(2, e.Backlog.Count());
    var backlog = e.Backlog.First(); e.MoveTask(backlog.Id, new(2026, 10, 20)); e.ToggleTask(backlog.Id);
    e = new PlannerEngine(store, clock); Equal(1, e.Backlog.Count()); Equal(true, e.TasksOn(new(2026, 10, 20)).Single().Completed);
});
Test("Moving an entire chain to backlog pauses it until reassignment without an infinite queue", () =>
{
    var (e, clock, _) = Setup(); var first = e.AddTask("pause", false, false, clock.Today, repeat: Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.TasksOn(clock.Today.AddDays(14)).ToList();
    e.MoveTask(next.Id, null, scope: TaskMoveScope.ThisAndFuture); e.EnsureTaskRange(clock.Today, clock.Today.AddYears(2));
    Equal(1, e.Backlog.Count()); Equal(1, e.Matrix(clock.Today, clock.Today.AddYears(2)).Scheduled.Sum());
    e.MoveTask(next.Id, clock.Today.AddDays(10)); Equal(1, e.TasksOn(clock.Today.AddDays(17)).Count());
    Equal(first.Id, e.TasksOn(clock.Today).Single().Id);
});
Test("Changing recurrence truncates only the selected and subsequent schedule", () =>
{
    var (e, clock, _) = Setup(); var first = e.AddTask("change", false, false, clock.Today, repeat: Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.EditTask(next.Id, "monthly", true, false, Repeat.Monthly);
    Equal(first.Id, e.TasksOn(clock.Today).Single().Id); Equal(0, e.TasksOn(clock.Today.AddDays(14)).Count());
    Equal(1, e.TasksOn(clock.Today.AddDays(7).AddMonths(1)).Count());
    e.EditTask(next.Id, "single", false, false, Repeat.None); Equal(0, e.TasksOn(clock.Today.AddDays(7).AddMonths(1)).Count());
    Equal(next.Id, e.TasksOn(clock.Today.AddDays(7)).Single().Id);
});
Test("Failed series reschedule rolls back dates, segments and completion atomically", () =>
{
    var (e, clock, store) = Setup(); var first = e.AddTask("atomic", false, false, clock.Today, repeat: Repeat.Weekly);
    var next = e.TasksOn(clock.Today.AddDays(7)).Single(); e.ToggleTask(next.Id); store.FailSave = true;
    Throws<IOException>(() => e.MoveTask(first.Id, clock.Today.AddDays(1), scope: TaskMoveScope.ThisAndFuture));
    Equal(1, e.State.TaskSeries.Count); Equal<DateOnly?>(clock.Today, e.TaskById(first.Id)!.Date);
    Equal(true, e.TaskById(next.Id)!.Completed); Equal<DateOnly?>(clock.Today.AddDays(7), e.TaskById(next.Id)!.Date);
});
Test("SQLite stores series splits, moved exceptions and independent completion", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "PlanerTests", Guid.NewGuid().ToString()); var path = Path.Combine(dir, "planner.db");
    var clock = new TestClock { Today = new(2026, 10, 13) }; var e = new PlannerEngine(new SqlitePlannerStore(path), clock);
    var first = e.AddTask("sqlite repeat", true, true, clock.Today, repeat: Repeat.Weekly); e.ToggleTask(first.Id);
    var second = e.TasksOn(clock.Today.AddDays(7)).Single(); e.MoveTask(second.Id, clock.Today.AddDays(9), scope: TaskMoveScope.ThisAndFuture);
    var third = e.TasksOn(clock.Today.AddDays(16)).Single(); e.MoveTask(third.Id, null);
    e = new PlannerEngine(new SqlitePlannerStore(path), clock); Equal(2, e.State.TaskSeries.Count); Equal(1, e.Backlog.Count());
    Equal(true, e.TasksOn(clock.Today).Single().Completed); Equal(0, e.TasksOn(clock.Today.AddDays(16)).Count());
    Equal(1, e.TasksOn(clock.Today.AddDays(23)).Count()); Directory.Delete(dir, true);
});
Test("Old SQLite tasks migrate with no repeat and keep their IDs and dates", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "PlanerTests", Guid.NewGuid().ToString()); Directory.CreateDirectory(dir); var path = Path.Combine(dir, "planner.db");
    var id = Guid.NewGuid();
    using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
    {
        c.Open(); using var command = c.CreateCommand(); command.CommandText = "CREATE TABLE tasks(id TEXT PRIMARY KEY,data TEXT NOT NULL);PRAGMA user_version=1;INSERT INTO tasks VALUES($id,$data)";
        command.Parameters.AddWithValue("$id", id.ToString()); command.Parameters.AddWithValue("$data", $$"""{"Id":"{{id}}","Description":"legacy","Date":"2026-10-13","Part":0,"Completed":true} """); command.ExecuteNonQuery();
    }
    var e = new PlannerEngine(new SqlitePlannerStore(path), new TestClock { Today = new(2026, 10, 13) });
    var task = e.State.Tasks.Single(); Equal(id, task.Id); Equal(Repeat.None, task.Repeat); Equal(true, task.Completed); Equal(0, e.State.TaskSeries.Count);
    Equal<DateOnly?>(new(2026, 10, 13), task.Date); Directory.Delete(dir, true);
});
Test("Pale habit palette retains element identity including legacy colors", () =>
{
    Equal("#E0F8E5", Palette.PaleHabitColor("#0DBA30")); Equal("#DBEEFD", Palette.PaleHabitColor("#59ADF6"));
    Equal("#F5F5F5", Palette.PaleHabitColor("#99908C"));
});

Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class TestClock : IPlannerClock { public DateOnly Today { get; set; } }
sealed class MemoryStore : IPlannerStore
{
    private string _json = JsonSerializer.Serialize(new PlannerState());
    public bool FailSave { get; set; }
    public PlannerState Load() => JsonSerializer.Deserialize<PlannerState>(_json)!;
    public void Save(PlannerState state)
    {
        if (FailSave) throw new IOException("simulated failure");
        _json = JsonSerializer.Serialize(state);
    }
}
