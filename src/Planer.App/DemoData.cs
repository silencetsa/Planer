using Planer.Core;
namespace Planer_App;

/// <summary>Opt-in QA data. Never populates the user's default database.</summary>
internal static class DemoData
{
    public static PlannerState Create(DateOnly today, string? annualScenario = null)
    {
        var monday = Dates.Monday(today);
        var s = new PlannerState { LastRolloverWeek = monday };
        var walk = new Habit { Name = "Прогулка", Color = Palette.Elements[3], Created = monday };
        var reading = new Habit { Name = "Читать", Color = Palette.Elements[5], Created = monday };
        var water = new Habit { Name = "Вода", Color = Palette.Elements[1], Created = monday };
        s.Habits.AddRange([walk, reading, water]);
        s.HabitChecks.Add(new() { HabitId = walk.Id, Date = monday });
        s.HabitChecks.Add(new() { HabitId = reading.Id, Date = today });
        s.Moods[monday] = Mood.Happy; s.Moods[today] = Mood.Good;
        s.Tasks.AddRange([
            new() { Description = "Подготовить документы", Important = true, Urgent = true, Date = monday, Part = DayPart.Morning, Completed = true },
            new() { Description = "Покормить кота", Important = true, Date = today, Part = DayPart.Evening },
            new() { Description = "Заехать на почту", Urgent = true, Date = today, Part = DayPart.Morning },
            new() { Description = "Разобрать фотографии из поездки", Date = monday.AddDays(3), Part = DayPart.Afternoon },
            new() { Description = "Прогуляться в парке", Important = true, Date = monday.AddDays(5), Part = DayPart.Morning },
            new() { Description = "Записаться на занятие", Important = true },
            new() { Description = "Ответить на письмо", Urgent = true },
            new() { Description = "Составить список покупок", Important = true },
            new() { Description = "Посмотреть интересный фильм", Important = false },
            new() { Description = "Подготовить большой список дел на следующий месяц и проверить детали каждого пункта", Important = true, Urgent = true }
        ]);
        for (int i = 0; i < s.Tasks.Count; i++) s.Tasks[i].Order = i;
        s.Payments.Add(new() { Revisions = [new() { Name = "Интернет", Amount = 805, Start = today, Repeat = Repeat.Monthly }] });
        s.BudgetIncome = 100000;
        string[] categoryNames = ["Продукты", "Дом", "Транспорт", "Отдых", "Сбережения"];
        decimal[] categoryLimits = [25000, 12000, 6000, 10000, 20000];
        for (int i = 0; i < categoryNames.Length; i++) s.BudgetCategories.Add(new() { Name = categoryNames[i], Limit = categoryLimits[i], Color = Palette.Elements[i], Order = i });
        s.Goals[Dates.WeekKey(today)] = "Найти время для важных дел";
        s.Goals[Dates.MonthKey(today)] = "Завершить начатые проекты";
        s.Goals[Dates.YearKey(today)] = "Больше внимания себе и близким";
        if (annualScenario?.StartsWith("Annual", StringComparison.Ordinal) == true) EnrichAnnualStatistics(s, today);
        if (annualScenario == "AnnualNoCompleted") foreach (var task in s.Tasks) task.Completed = false;
        if (annualScenario == "AnnualLongValues")
        {
            foreach (var habit in s.Habits) habit.Name = new string('Ш', 13);
            s.Moods.Clear(); var first = new DateOnly(today.Year, 1, 1);
            for (int i = 0; i < (DateTime.IsLeapYear(today.Year) ? 366 : 365); i++) s.Moods[first.AddDays(i)] = Mood.Happy;
        }
        if (annualScenario is "AnnualOneKind" or "AnnualSmallSlice")
        {
            s.Tasks.Clear();
            int[] counts = annualScenario == "AnnualOneKind" ? [0, 0, 5, 0] : [97, 1, 1, 1];
            foreach (var kind in Enum.GetValues<TaskKind>())
                for (int i = 0; i < counts[(int)kind]; i++) s.Tasks.Add(new() { Description = "Проверка доли " + kind, Date = today,
                    Important = kind is TaskKind.UrgentImportant or TaskKind.NotUrgentImportant,
                    Urgent = kind is TaskKind.UrgentImportant or TaskKind.UrgentUnimportant, Completed = true });
        }
        return s;
    }
    private static void EnrichAnnualStatistics(PlannerState state, DateOnly today)
    {
        var first = new DateOnly(today.Year, 1, 1); var activeStart = first.AddDays(14);
        if (activeStart > today) activeStart = today;
        foreach (var habit in state.Habits) habit.Created = activeStart;
        var historical = new Habit { Name = "Зарядка", Created = first.AddYears(-1).AddMonths(11), Ended = activeStart, Color = Palette.Elements[0] };
        state.Habits.Add(historical);
        for (int i = 0; i < activeStart.DayNumber - first.DayNumber; i++) state.HabitChecks.Add(new() { HabitId = historical.Id, Date = first.AddDays(i) });
        for (int i = 0; i < 120; i++) state.HabitChecks.Add(new() { HabitId = state.Habits[1].Id, Date = first.AddMonths(2).AddDays(i) });
        for (int i = 0; i < 90; i++) state.HabitChecks.Add(new() { HabitId = state.Habits[0].Id, Date = activeStart.AddDays(i * 2) });
        for (int i = 0; i < 45; i++) state.HabitChecks.Add(new() { HabitId = state.Habits[2].Id, Date = first.AddMonths(4).AddDays(i) });
        for (int i = 0; i < 81; i++) state.Moods[first.AddDays(i)] = (Mood)(i % 5);
        for (int i = 0; i < 12; i++) state.Tasks.Add(new() { Description = "План на месяц " + (i + 1), Date = new(today.Year, i + 1, 10),
            Important = i % 4 is 0 or 2, Urgent = i % 4 is 0 or 1, Completed = i % 3 != 0, Order = 100 + i });
    }
}
