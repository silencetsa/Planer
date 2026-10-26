using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private bool _confirmationPending;

    private Border PaymentConfirmationPreview(Payment payment)
    {
        var revision = _vm.PaymentRevision(payment)!;
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(content, Ui.OriginalIcon("YellowAccent.png", 18));
        Ui.Add(content, Ui.Text(revision.Name, 14, "#FFFFFF"), 1);
        Ui.Add(content, Ui.Text(FormatAmount(revision.Amount), 16, Palette.Elements[2], true), 2);
        return new Border { Name = "PaymentConfirmationPreview", Child = content, Background = Ui.Brush(Ui.Accent), CornerRadius = new(4), Padding = new(8, 6, 8, 6) };
    }

    private bool CanConfirm => OwnsDialogs && !_confirmationPending && Ui.OpenDialogForRoot(XamlRoot) is null;

    private async void DeletePayment(object? sender, Payment payment)
    {
        if (!CanConfirm) return;
        _confirmationPending = true;
        try
        {
            var body = new StackPanel { Spacing = 14, MinWidth = 360 };
            body.Children.Add(PaymentConfirmationPreview(payment));
            body.Children.Add(Ui.Text("Удалится вся цепочка: прошлые, сегодняшние и будущие платежи, включая отметки об оплате.", 14));
            body.Children.Add(Ui.Text("Чтобы убрать только будущие платежи и сохранить историю, выберите «Завершить» в списке платежей. Сегодняшний платёж при завершении остаётся.", 14));
            var dialog = Ui.Dialog(XamlRoot, "Удалить платёж?", body, primary: "Удалить", scale: _scaleHost.ScaleFactor, defaultClose: true, destructive: true);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.DeletePayment(payment.Id);
        }
        finally { _confirmationPending = false; }
    }

    private async void FinishPayment(object? sender, Payment payment)
    {
        if (!CanConfirm) return;
        _confirmationPending = true;
        try
        {
            var body = new StackPanel { Spacing = 14, MinWidth = 360 };
            body.Children.Add(PaymentConfirmationPreview(payment));
            body.Children.Add(Ui.Text("Будущие платежи исчезнут из планера. Сегодняшний платёж, прошлые вхождения и их отметки об оплате сохранятся.", 14));
            body.Children.Add(Ui.Text("Дата окончания — сегодня включительно. Платёж переместится в раздел «Завершённые платежи».", 14));
            var dialog = Ui.Dialog(XamlRoot, "Завершить платёж?", body, primary: "Завершить", scale: _scaleHost.ScaleFactor, defaultClose: true);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.FinishPayment(payment.Id);
        }
        finally { _confirmationPending = false; }
    }

    private async void DeleteBudgetCategory(object? sender, BudgetCategory category)
    {
        if (!CanConfirm) return;
        _confirmationPending = true;
        try
        {
            var body = new StackPanel { Spacing = 14, MinWidth = 340 };
            int index = Array.IndexOf(Palette.Elements, category.Color);
            var preview = new Grid { ColumnSpacing = 12 };
            preview.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); preview.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(preview, Ui.Text(category.Name, 15)); Ui.Add(preview, Ui.Text(FormatAmount(category.Limit), 16, Ui.Ink, true), 1);
            body.Children.Add(new Border { Name = "BudgetCategoryDeletePreview", Child = preview, Background = Ui.Brush(index >= 0 ? Palette.PaleElements[index] : Ui.Paper), BorderBrush = Ui.Brush(category.Color), BorderThickness = new(3, 0, 0, 0), CornerRadius = new(4), Padding = new(9, 7, 9, 7) });
            body.Children.Add(Ui.Text("Категория и её плановый лимит будут удалены. Эта сумма снова станет доступна для распределения бюджета.", 14));
            var dialog = Ui.Dialog(XamlRoot, "Удалить категорию?", body, primary: "Удалить", scale: _scaleHost.ScaleFactor, defaultClose: true, destructive: true);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.DeleteBudgetCategory(category.Id);
        }
        finally { _confirmationPending = false; }
    }
}
