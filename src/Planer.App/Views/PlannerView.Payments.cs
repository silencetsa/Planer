using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private static readonly string[] RepeatNames = ["Без повторения", "Каждую неделю", "Каждые 2 недели", "Каждый месяц", "Каждый год"];
    private static int FrequencyRank(Repeat repeat) => repeat switch { Repeat.Weekly => 5, Repeat.Fortnightly => 4, Repeat.Monthly => 3, Repeat.Yearly => 2, _ => 1 };
    private static string FormatAmount(decimal amount) => amount.ToString(amount == decimal.Truncate(amount) ? "N0" : "N2", PlannerViewModel.Russian) + " ₽";
    private UIElement PaymentsBoard()
    {
        var outer = new Grid { RowSpacing = 12 }; outer.RowDefinitions.Add(new() { Height = GridLength.Auto }); outer.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var switchList = Ui.Button(Ui.Text(_vm.ShowCompletedPayments ? "← Активные платежи" : "Завершённые платежи", 13, Ui.Orange));
        switchList.HorizontalAlignment = HorizontalAlignment.Left; switchList.Click += (_, _) => { _vm.ShowCompletedPayments = !_vm.ShowCompletedPayments; _vm.Refresh(); }; Ui.Add(outer, switchList);
        var list = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
        var payments = _vm.Payments.Where(p => _vm.ShowCompletedPayments != p.IsActive(_vm.Today)).Select(p => (Payment: p, Revision: _vm.PaymentRevision(p)!))
            .OrderByDescending(p => FrequencyRank(p.Revision.Repeat)).ThenByDescending(p => p.Revision.Amount).ToList();
        if (payments.Count == 0) list.Children.Add(Ui.Card(Ui.Text(_vm.ShowCompletedPayments ? "Завершённых платежей пока нет." : "Добавьте платёж: он появится в карточках дней по своему расписанию.", 15)));
        foreach (var group in payments.GroupBy(p => p.Revision.Repeat))
        {
            list.Children.Add(Ui.Text(RepeatNames[(int)group.Key], 18, Ui.Blue, true));
            foreach (var item in group)
            {
                var p = item.Payment; var r = item.Revision;
                var row = new Grid { ColumnSpacing = 18 };
                row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = new(130) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                var info = new StackPanel { Spacing = 4 }; info.Children.Add(Ui.Text(r.Name, 19, Ui.Ink, true));
                info.Children.Add(Ui.Text($"С {r.Start:dd.MM.yyyy}" + (r.End is { } end ? $"   ·   До {end:dd.MM.yyyy} включительно" : "   ·   Без даты окончания"), 14, Ui.Muted));
                Ui.Add(row, info); Ui.Add(row, Ui.Text(FormatAmount(r.Amount), 20, Ui.Accent, true), 1);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                if (p.IsActive(_vm.Today)) actions.Children.Add(Ui.Button(Ui.Icon("\uE73E", 19, "#32AE65"), _vm.FinishPaymentCommand, p.Id, "Завершить серию сегодня"));
                actions.Children.Add(Ui.Button(Ui.Icon("\uE70F", 18), _vm.EditPaymentCommand, p.Id, "Редактировать платёж"));
                actions.Children.Add(Ui.Button(Ui.Icon("\uE74D", 18), _vm.DeletePaymentCommand, p.Id, "Удалить платёж с сегодняшнего дня")); Ui.Add(row, actions, 2);
                list.Children.Add(Ui.Card(row));
            }
            var total = Ui.Text("Итого: " + FormatAmount(group.Sum(p => p.Revision.Amount)), 17, Ui.Accent, true); total.HorizontalAlignment = HorizontalAlignment.Right; total.Margin = new(0, 0, 13, 10); list.Children.Add(total);
        }
        Ui.Add(outer, Ui.Scroll(list), row: 1); return outer;
    }
    private async void EditPayment(object? sender, Payment? payment)
    {
        if (!OwnsDialogs) return;
        var revision = payment is null ? null : _vm.PaymentRevision(payment);
        var name = new TextBox { Header = "Название", Text = revision?.Name ?? "", MaxLength = 200, PlaceholderText = "Например, интернет" };
        var amount = new TextBox { Header = "Сумма, ₽", Text = revision?.Amount.ToString(PlannerViewModel.Russian) ?? "", PlaceholderText = "805,00" };
        var start = new CalendarDatePicker { Header = "Дата", Date = Offset(revision?.Start ?? _vm.Today), DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}", HorizontalAlignment = HorizontalAlignment.Stretch };
        var repeat = new ComboBox { Header = "Повторение", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = RepeatNames, SelectedIndex = (int)(revision?.Repeat ?? Repeat.None) };
        var endDate = new CalendarDatePicker { Header = "Окончание (необязательно)", Date = revision?.End is { } end ? Offset(end) : null,
            DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}", PlaceholderText = "Без окончания", HorizontalAlignment = HorizontalAlignment.Stretch };
        var clearEnd = Ui.Button(Ui.Text("Убрать дату окончания", 11, Ui.Muted)); clearEnd.HorizontalAlignment = HorizontalAlignment.Left; clearEnd.Click += (_, _) => endDate.Date = null;
        var error = Ui.Text("", 12, "#BD3026"); var content = new StackPanel { Spacing = 13, MinWidth = 360 };
        content.Children.Add(name); content.Children.Add(amount);
        var dates = Ui.Grid(2); Ui.Add(dates, start); Ui.Add(dates, repeat, 1); content.Children.Add(dates); content.Children.Add(endDate); content.Children.Add(clearEnd); content.Children.Add(error);
        var dialog = Ui.Dialog(XamlRoot, payment is null ? "Новый платёж" : "Редактировать платёж", content, scale: _scaleHost.ScaleFactor);
        decimal parsedAmount = 0;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) error.Text = "Введите название платежа";
            else if (!ParseAmount(amount.Text, out parsedAmount) || parsedAmount <= 0 || parsedAmount > 999999999 || decimal.Round(parsedAmount, 2) != parsedAmount) error.Text = "Введите положительную сумму с точностью до копеек";
            else if (start.Date is null) error.Text = "Выберите дату начала";
            else if (endDate.Date is { } end && DateOnly.FromDateTime(end.DateTime) < DateOnly.FromDateTime(start.Date.Value.DateTime)
                && !(revision?.End is { } oldEnd && oldEnd <= _vm.Today && DateOnly.FromDateTime(end.DateTime) <= _vm.Today)) error.Text = "Окончание не может быть раньше начала";
            else { error.Text = ""; return; }
            args.Cancel = true;
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _vm.SavePayment(payment?.Id, name.Text, parsedAmount, DateOnly.FromDateTime(start.Date!.Value.DateTime), (Repeat)repeat.SelectedIndex,
                endDate.Date is { } ending ? DateOnly.FromDateTime(ending.DateTime) : null);
    }
    private static DateTimeOffset Offset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));
    private static bool ParseAmount(string text, out decimal amount) => decimal.TryParse(text, NumberStyles.Number, PlannerViewModel.Russian, out amount)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
