using CommunityToolkit.Mvvm.Input;
using Planer.Core;

namespace Planer_App.ViewModels;

public sealed partial class PlannerViewModel
{
    private Guid? _yearHabitId;
    private IRelayCommand? _showYearMood;
    public IReadOnlyList<Habit> YearHabits => _engine.HabitsForYear(SelectedDate.Year);
    public Habit? SelectedYearHabit => YearHabits.FirstOrDefault(h => h.Id == _yearHabitId);
    public Guid? YearHabitId => SelectedYearHabit?.Id;
    public bool YearShowsMood => YearHabitId is null;
    public IRelayCommand ShowYearMoodCommand => _showYearMood ??= new RelayCommand(() => SelectYearHabit(null));
    public void SelectYearHabit(Guid? id)
    {
        if (id is not null && !YearHabits.Any(h => h.Id == id)) return;
        if (_yearHabitId == id) return;
        _yearHabitId = id; Refresh();
    }
    private void NormalizeYearHabit()
    {
        if (_yearHabitId is { } id && !YearHabits.Any(h => h.Id == id)) _yearHabitId = null;
    }
}
