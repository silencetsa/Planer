using Planer.Core;
namespace Planer_App;

/// <summary>Opt-in QA data. Never populates the user's default database.</summary>
internal static class DemoData
{
    public static PlannerState Create(DateOnly today)
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
        s.Goals[Dates.WeekKey(today)] = "Найти время для важных дел";
        s.Goals[Dates.MonthKey(today)] = "Завершить начатые проекты";
        s.Goals[Dates.YearKey(today)] = "Больше внимания себе и близким";
        return s;
    }
}
