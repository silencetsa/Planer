namespace Planer.Core;

public sealed partial class PlannerEngine
{
    public IReadOnlyList<Habit> HabitsForYear(int year)
    {
        var first = new DateOnly(year, 1, 1); var last = new DateOnly(year, 12, 31);
        return State.Habits.Where(h => h.Created.Year == year || h.Created <= last && (h.Ended is null || h.Ended > first))
            .OrderBy(h => h.Ended is { } end && end <= last).ThenByDescending(h => h.Created)
            .ThenByDescending(h => h.Order).ThenBy(h => h.Id).ToList();
    }

    /// <summary>Counts the selected dates without materializing repeats or changing persisted state.</summary>
    public AnnualStatistics StatisticsForYear(int year)
    {
        var first = new DateOnly(year, 1, 1); var last = new DateOnly(year, 12, 31);
        // Unseen repetitions count exactly like already materialized occurrences. Moved instances,
        // backlog entries and single deleted repeats must retain their current assigned date.
        var tasks = State.Tasks.Where(t => t.Date >= first && t.Date <= last).Concat(MissingOccurrences(first, last)).ToList();
        var types = Enum.GetValues<TaskKind>().Select(kind =>
        {
            int added = tasks.Count(t => t.Kind == kind);
            return new AnnualTaskCount(kind, tasks.Count(t => t.Kind == kind && t.Completed), added,
                tasks.Count == 0 ? 0 : added * 100m / tasks.Count);
        }).ToArray();

        var habits = HabitsForYear(year);
        var checks = State.HabitChecks.Where(c => c.Date >= first && c.Date <= last).ToLookup(c => c.HabitId, c => c.Date);
        var records = habits.Select(habit =>
        {
            var dates = checks[habit.Id].Where(habit.ExistsOn).Distinct().Order().ToArray();
            int longest = 0, current = 0; DateOnly? start = null, bestStart = null, bestEnd = null, previous = null;
            foreach (var date in dates)
            {
                if (previous is { } prev && date.DayNumber - prev.DayNumber == 1) current++;
                else { current = 1; start = date; }
                if (current > longest) { longest = current; bestStart = start; bestEnd = date; }
                previous = date;
            }
            return new AnnualHabitRecord(habit.Id, habit.Name, Palette.HabitColor(habit.Color), dates.Length, longest, bestStart, bestEnd);
        }).ToArray();
        // Name and ID make ties deterministic, including identical counts and same-day creation.
        var top = records.Where(r => r.Marks > 0).OrderByDescending(r => r.Marks).ThenByDescending(r => r.LongestStreak)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id).Take(3).ToArray();
        var record = records.Where(r => r.LongestStreak > 0).OrderByDescending(r => r.LongestStreak)
            .ThenBy(r => r.StreakStart).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id).FirstOrDefault();
        int finished = habits.Count(h => h.Ended is { } end && end <= last);
        var habitStatistics = new AnnualHabitStatistics(habits.Count, habits.Count(h => h.Created.Year == year),
            habits.Count - finished, finished, top, record);
        var moods = Enum.GetValues<Mood>().Select(mood => new AnnualMoodCount(mood,
            State.Moods.Count(pair => pair.Key >= first && pair.Key <= last && pair.Value == mood))).ToArray();
        return new(year, tasks.Count(t => t.Completed), tasks.Count, types, habitStatistics, moods);
    }
}
