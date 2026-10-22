using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

internal static partial class UiSnapshot
{
    private static async Task SaveFinanceScenarioAsync(FrameworkElement view, string path, string scenario, PlannerViewModel vm)
    {
        if (vm.Screen != PlannerScreen.Finances) throw new InvalidOperationException("Не открыт раздел финансов");
        if (scenario == "FinanceEmpty")
        {
            if (vm.Budget != new BudgetSummary(0, 0, 0)) throw new InvalidOperationException("Пустая база получила демонстрационный бюджет");
        }
        if (scenario is "FinanceMaximum" or "FinanceMinimum")
        {
            for (int i = vm.BudgetCategories.Count; i < 7; i++)
                if (vm.SaveBudgetCategory(null, "Плановая категория с длинным названием " + i, "10,25") is { } error) throw new InvalidOperationException(error);
            if (scenario == "FinanceMinimum") App.Window.AppWindow.Resize(WindowSizing.Minimum(App.Window, floating: false));
            vm.Refresh();
        }
        if (scenario == "FinanceOverbudget")
        {
            if (vm.SaveBudgetIncome("500") is { } error) throw new InvalidOperationException(error);
            vm.Refresh();
            if (vm.Budget.Remaining >= 0 || vm.BudgetCategories.Count == 0) throw new InvalidOperationException("Потеряны лимиты при снижении дохода");
        }
        if (scenario == "FinanceFull")
        {
            foreach (var category in vm.BudgetCategories) vm.DeleteBudgetCategoryCommand.Execute(category.Id);
            if (vm.SaveBudgetIncome(vm.Budget.MandatoryPayments.ToString(PlannerViewModel.Russian)) is { } error) throw new InvalidOperationException(error);
            vm.Refresh();
        }
        if (scenario == "FinanceIncome")
        {
            var income = Descendants<TextBox>(view).Single(t => t.Name == "BudgetIncome");
            income.Text = "200000,50";
            if (vm.SaveBudgetIncome(income.Text) is { } error) throw new InvalidOperationException(error);
            if (!ReferenceEquals(income, Descendants<TextBox>(view).Single(t => t.Name == "BudgetIncome")) || vm.Budget.Income != 200000.50m)
                throw new InvalidOperationException("Сохранение дохода пересоздало поле или потеряло сумму");
        }
        await PrepareAsync(view);
        if (scenario == "FinancePlus")
        {
            var plus = Descendants<Button>(view).Single(b => b.Name == "AddBudgetCategory");
            var before = Descendants<Grid>(view).Where(g => g.Name == "BudgetCategoryRow").Select(g => g.TransformToVisual(view).TransformPoint(new())).ToArray();
            plus.Opacity = 1; await PrepareAsync(view);
            var after = Descendants<Grid>(view).Where(g => g.Name == "BudgetCategoryRow").Select(g => g.TransformToVisual(view).TransformPoint(new())).ToArray();
            if (!before.SequenceEqual(after)) throw new InvalidOperationException("Кнопка добавления сдвигает категории");
        }
        var pie = Descendants<Grid>(view).Single(g => g.Name == "BudgetPie");
        if (pie.Tag is not BudgetSummary rendered || rendered != vm.Budget) throw new InvalidOperationException("Диаграмма использует устаревший бюджет");
        if (vm.Budget.Income == 0 || vm.Budget.Remaining < 0)
        {
            if (pie.Children.OfType<Shape>().Any(s => s.Tag is string tag && tag == "BudgetSector")) throw new InvalidOperationException("Некорректная диаграмма при нулевом доходе/перерасходе");
        }
        if (scenario == "FinanceFull" && pie.Children.OfType<Ellipse>().Count(e => e.Tag is string tag && tag == "BudgetSector") != 1)
            throw new InvalidOperationException("Сектор на 100% не заполнил круг");
        if (scenario == "FinanceMinimum")
        {
            var scroll = Descendants<ScrollViewer>(view).Single(s => s.Content is Grid { Name: "FinancesBoard" });
            scroll.ChangeView(null, double.MaxValue, null, true); await PrepareAsync(view);
            var plus = Descendants<Button>(view).Single(b => b.Name == "AddBudgetCategory"); plus.Opacity = 1;
            var bottom = plus.TransformToVisual(view).TransformPoint(new(0, plus.ActualHeight));
            if (bottom.Y > view.ActualHeight + 1) throw new InvalidOperationException("Последняя категория и плюс недоступны в минимальном окне");
            await SaveAsync(view, System.IO.Path.ChangeExtension(path, ".bottom.png"));
            scroll.ChangeView(null, 0, null, true); await PrepareAsync(view);
        }
        if (scenario is not ("FinanceDialog" or "FinanceEditDialog" or "FinanceBudgetError" or "FinanceCrud")) { await SaveAsync(view, path); return; }

        int previousCount = vm.BudgetCategories.Count;
        if (scenario == "FinanceEditDialog") vm.EditBudgetCategoryCommand.Execute(vm.BudgetCategories.First().Id);
        else vm.CreateBudgetCategoryCommand.Execute(null);
        var (dialog, root) = await FindBudgetDialogAsync(view);
        var name = Descendants<TextBox>(root).Single(t => t.Name == "BudgetCategoryName");
        var amount = Descendants<TextBox>(root).Single(t => t.Name == "BudgetCategoryLimit");
        if (scenario == "FinanceEditDialog" && (name.Text != vm.BudgetCategories.First().Name || !BudgetMoney.TryParse(amount.Text, out var value) || value != vm.BudgetCategories.First().Limit))
            throw new InvalidOperationException("Редактор не заполнил сохранённые поля");
        if (scenario is "FinanceBudgetError" or "FinanceCrud")
        {
            name.Text = "Проверка лимита"; amount.Text = "100000,00";
            InvokeBudgetSave(root); await Task.Delay(150); await PrepareAsync(root);
            var error = Descendants<TextBlock>(root).Single(t => t.Name == "BudgetCategoryError");
            if (!ReferenceEquals(Ui.OpenDialogForRoot(view.XamlRoot), dialog) || error.Text != PlannerEngine.BudgetExceededMessage || error.Visibility != Visibility.Visible || vm.BudgetCategories.Count != previousCount)
                throw new InvalidOperationException("Нехватка бюджета должна оставить форму открытой без сохранения");
            if (error.Foreground is not SolidColorBrush { Color: { R: 204, G: 10, B: 0 } }) throw new InvalidOperationException("Ошибка бюджета должна быть красной");
            if (scenario == "FinanceCrud")
            {
                amount.Text = "123,45"; amount.Text = "123,456";
                if (amount.Text != "123,45") throw new InvalidOperationException("Поле приняло третью цифру после запятой");
                InvokeBudgetSave(root); await WaitForBudgetDialogCloseAsync(view); await PrepareAsync(view);
                var saved = vm.BudgetCategories.Single(c => c.Name == name.Text);
                if (saved.Limit != 123.45m || vm.BudgetCategories.Count != previousCount + 1) throw new InvalidOperationException("Категория не сохранилась из формы");
                vm.EditBudgetCategoryCommand.Execute(saved.Id); (dialog, root) = await FindBudgetDialogAsync(view);
                amount = Descendants<TextBox>(root).Single(t => t.Name == "BudgetCategoryLimit"); amount.Text = "99,99";
                InvokeBudgetSave(root); await WaitForBudgetDialogCloseAsync(view);
                if (vm.BudgetCategories.Single(c => c.Id == saved.Id).Limit != 99.99m) throw new InvalidOperationException("Редактирование не изменило лимит");
                vm.DeleteBudgetCategoryCommand.Execute(saved.Id); await PrepareAsync(view);
                if (vm.BudgetCategories.Count != previousCount || vm.BudgetCategories.Any(c => c.Id == saved.Id)) throw new InvalidOperationException("Не удалена плановая категория");
                await SaveAsync(view, path); return;
            }
        }
        var chrome = Descendants<Border>(root).FirstOrDefault(b => b.Name == "BackgroundElement");
        await SaveAsync(chrome ?? root, path);
    }
    private static void InvokeBudgetSave(FrameworkElement root)
    {
        var save = Descendants<Button>(root).Single(b => b.Content is string text && text == "Сохранить");
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(save) ?? new ButtonAutomationPeer(save);
        if (peer.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke) throw new InvalidOperationException("Нет действия сохранения формы");
        invoke.Invoke();
    }
    private static async Task<(ContentDialog Dialog, FrameworkElement Root)> FindBudgetDialogAsync(FrameworkElement view)
    {
        for (int i = 0; i < 100; i++)
        {
            await Task.Delay(30);
            if (Ui.OpenDialogForRoot(view.XamlRoot) is not { } dialog || dialog.Content is not DependencyObject content) continue;
            var root = VisualTreeHelper.GetOpenPopupsForXamlRoot(view.XamlRoot).Select(p => p.Child).OfType<FrameworkElement>()
                .FirstOrDefault(p => Descendants<DependencyObject>(p).Any(child => ReferenceEquals(child, content)));
            if (root is not null) { await PrepareAsync(root); return (dialog, root); }
        }
        throw new InvalidOperationException("Не открылась форма категории");
    }
    private static async Task WaitForBudgetDialogCloseAsync(FrameworkElement view)
    {
        for (int i = 0; i < 50; i++) { await Task.Delay(30); if (Ui.OpenDialogForRoot(view.XamlRoot) is null) return; }
        throw new InvalidOperationException("Форма не закрылась после успешного сохранения");
    }
}
