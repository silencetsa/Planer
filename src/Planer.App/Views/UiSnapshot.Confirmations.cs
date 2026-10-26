using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

internal static partial class UiSnapshot
{
    private static async Task SaveConfirmationScenarioAsync(FrameworkElement view, string path, string scenario, PlannerViewModel vm)
    {
        if (scenario == "ConfirmationActions") { await CheckPaymentConfirmationActionsAsync(view, path, vm); return; }
        if (scenario == "ConfirmationCategory") vm.DeleteBudgetCategoryCommand.Execute(vm.BudgetCategories.First().Id);
        else if (scenario == "ConfirmationPaymentFinish") vm.FinishPaymentCommand.Execute(vm.Payments.First(p => p.IsActive(vm.Today)).Id);
        else vm.DeletePaymentCommand.Execute(vm.Payments.First().Id);
        var (dialog, root) = await FindBudgetDialogAsync(view);
        ValidateConfirmation(dialog, root, scenario == "ConfirmationCategory" ? "BudgetCategoryDeletePreview" : "PaymentConfirmationPreview");
        var text = string.Join(' ', Descendants<TextBlock>(root).Select(t => t.Text));
        if (scenario != "ConfirmationCategory" && (!text.Contains("прошлые", StringComparison.OrdinalIgnoreCase) || !text.Contains("сегодня", StringComparison.OrdinalIgnoreCase) || !text.Contains("будущ", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Подтверждение не объясняет границы изменения цепочки");
        if (scenario == "ConfirmationPaymentDelete" && !text.Contains("Завершить"))
            throw new InvalidOperationException("Удаление не объясняет альтернативу завершения серии");
        var chrome = Descendants<Border>(root).FirstOrDefault(b => b.Name == "BackgroundElement");
        await SaveAsync(chrome ?? root, path);
    }

    private static void ValidateConfirmation(ContentDialog dialog, FrameworkElement root, string previewName)
    {
        if (dialog.DefaultButton != ContentDialogButton.Close) throw new InvalidOperationException("Подтверждение должно по умолчанию выбирать отмену");
        if (!Descendants<Border>(root).Any(b => b.Name == previewName && b.Background is SolidColorBrush))
            throw new InvalidOperationException("В подтверждении отсутствует цветная карточка удаляемого элемента");
        var cancel = Descendants<Button>(root).Single(b => b.Content is string title && title == "Отмена");
        if (cancel.Background is not SolidColorBrush { Color: { R: 255, G: 238, B: 227 } } || cancel.Foreground is not SolidColorBrush { Color: { R: 255, G: 124, B: 5 } })
            throw new InvalidOperationException("Отмена должна использовать оранжевую палитру");
    }

    private static async Task CheckPaymentConfirmationActionsAsync(FrameworkElement view, string path, PlannerViewModel vm)
    {
        var yesterday = vm.Today.AddDays(-1);
        var dates = new[] { vm.Today.AddDays(-7), yesterday, vm.Today, vm.Today.AddDays(7), yesterday.AddMonths(1) };
        string Snapshot() => JsonSerializer.Serialize(new { vm.Payments, Occurrences = dates.Select(date => vm.PaymentsOn(date).ToArray()).ToArray(), vm.Budget });
        vm.SavePayment(null, "Проверка удаления", 99, yesterday, Repeat.Monthly, null);
        var deleted = vm.Payments.Single(p => vm.PaymentRevision(p)?.Name == "Проверка удаления");
        vm.PaymentCommand.Execute(new PaymentToggle(deleted.Id, yesterday));
        vm.PaymentCommand.Execute(new PaymentToggle(deleted.Id, yesterday.AddMonths(1)));
        string before = Snapshot();
        vm.DeletePaymentCommand.Execute(deleted.Id);
        var (dialog, root) = await FindBudgetDialogAsync(view); ValidateConfirmation(dialog, root, "PaymentConfirmationPreview");
        if (Snapshot() != before) throw new InvalidOperationException("Запрос удаления уже изменил платёж");
        InvokeDialogButton(root, "Отмена"); await WaitForBudgetDialogCloseAsync(view);
        if (Snapshot() != before) throw new InvalidOperationException("Отмена удаления изменила данные");
        vm.DeletePaymentCommand.Execute(deleted.Id); (dialog, root) = await FindBudgetDialogAsync(view);
        InvokeDialogButton(root, "Удалить"); await WaitForBudgetDialogCloseAsync(view);
        if (vm.Payments.Any(p => p.Id == deleted.Id) || dates.Any(date => vm.PaymentsOn(date).Any(p => p.PaymentId == deleted.Id)))
            throw new InvalidOperationException("Подтверждённое удаление оставило часть цепочки платежа");

        var start = vm.Today.AddDays(-7);
        vm.SavePayment(null, "Проверка завершения", 150, start, Repeat.Weekly, null);
        var finished = vm.Payments.Single(p => vm.PaymentRevision(p)?.Name == "Проверка завершения");
        foreach (var date in new[] { start, vm.Today, vm.Today.AddDays(7) }) vm.PaymentCommand.Execute(new PaymentToggle(finished.Id, date));
        before = Snapshot(); vm.FinishPaymentCommand.Execute(finished.Id);
        (dialog, root) = await FindBudgetDialogAsync(view); ValidateConfirmation(dialog, root, "PaymentConfirmationPreview");
        if (Snapshot() != before) throw new InvalidOperationException("Запрос завершения уже изменил платёж");
        InvokeDialogButton(root, "Отмена"); await WaitForBudgetDialogCloseAsync(view);
        if (Snapshot() != before) throw new InvalidOperationException("Отмена завершения изменила данные");
        vm.FinishPaymentCommand.Execute(finished.Id); (dialog, root) = await FindBudgetDialogAsync(view);
        InvokeDialogButton(root, "Завершить"); await WaitForBudgetDialogCloseAsync(view);
        if (!vm.PaymentsOn(start).Single(p => p.PaymentId == finished.Id).Completed || !vm.PaymentsOn(vm.Today).Single(p => p.PaymentId == finished.Id).Completed
            || vm.PaymentsOn(vm.Today.AddDays(7)).Any(p => p.PaymentId == finished.Id) || finished.IsActive(vm.Today))
            throw new InvalidOperationException("Завершение не сохранило оплаченную историю или не убрало будущий платёж");
        vm.ShowCompletedPayments = true; vm.Refresh(); await SaveAsync(view, path);
    }
}
