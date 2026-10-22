using CommunityToolkit.Mvvm.Input;
using Planer.Core;

namespace Planer_App.ViewModels;

public sealed partial class PlannerViewModel
{
    private bool _savingBudgetIncome;
    public BudgetSummary Budget => _engine.Budget;
    public IReadOnlyList<BudgetCategory> BudgetCategories => _engine.State.BudgetCategories.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();
    public event EventHandler<BudgetCategory?>? BudgetCategoryEditorRequested;
    private IRelayCommand? _createBudgetCategory;
    private IRelayCommand<Guid>? _editBudgetCategory, _deleteBudgetCategory;
    public IRelayCommand CreateBudgetCategoryCommand => _createBudgetCategory ??= new RelayCommand(() => BudgetCategoryEditorRequested?.Invoke(this, null));
    public IRelayCommand<Guid> EditBudgetCategoryCommand => _editBudgetCategory ??= new RelayCommand<Guid>(id =>
    {
        if (BudgetCategories.FirstOrDefault(c => c.Id == id) is { } category) BudgetCategoryEditorRequested?.Invoke(this, category);
    });
    public IRelayCommand<Guid> DeleteBudgetCategoryCommand => _deleteBudgetCategory ??= new RelayCommand<Guid>(id => Run(() => _engine.DeleteBudgetCategory(id)));

    public string? SaveBudgetIncome(string text)
    {
        if (!BudgetMoney.TryParse(text, out decimal income)) return "Введите сумму от 0 до 999 999 999, не более двух знаков после запятой";
        if (income == Budget.Income) return null;
        try { _savingBudgetIncome = true; _engine.SetBudgetIncome(income); return null; }
        catch (Exception error) { return error.Message; }
        finally { _savingBudgetIncome = false; }
    }
    public string? SaveBudgetCategory(Guid? id, string name, string amount)
    {
        if (!BudgetMoney.TryParse(amount, out decimal limit)) return "Введите сумму от 0 до 999 999 999, не более двух знаков после запятой";
        try { _engine.SaveBudgetCategory(id, name, limit); return null; }
        catch (Exception error) { return error.Message; }
    }
}
