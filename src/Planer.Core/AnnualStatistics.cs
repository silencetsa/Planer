namespace Planer.Core;

public sealed record AnnualTaskCount(TaskKind Kind, int Completed, int Added, decimal Percent);
public sealed record AnnualMoodCount(Mood Mood, int Count);
public sealed record AnnualHabitRecord(Guid Id, string Name, string Color, int Marks, int LongestStreak,
    DateOnly? StreakStart, DateOnly? StreakEnd);
public sealed record AnnualHabitStatistics(int InYear, int Created, int Active, int Finished,
    IReadOnlyList<AnnualHabitRecord> TopThree, AnnualHabitRecord? LongestStreak);
public sealed record AnnualStatistics(int Year, int CompletedTasks, int AddedTasks, IReadOnlyList<AnnualTaskCount> TaskTypes,
    AnnualHabitStatistics Habits, IReadOnlyList<AnnualMoodCount> Moods);
