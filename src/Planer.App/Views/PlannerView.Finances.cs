using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;
using Planer_App.ViewModels;
using Windows.Foundation;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private Action? _refreshFinanceValues;

    private UIElement FinancesBoard()
    {
        var board = new Grid { ColumnSpacing = 44, VerticalAlignment = VerticalAlignment.Top, Name = "FinancesBoard" };
        board.ColumnDefinitions.Add(new() { Width = new(1.15, GridUnitType.Star) });
        board.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var left = new StackPanel { Spacing = 24 };
        var income = new TextBox
        {
            Name = "BudgetIncome", Text = _vm.Budget.Income.ToString("F2", PlannerViewModel.Russian),
            FontFamily = Ui.PlannerFontBold, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CharacterSpacing = 0, Foreground = Ui.Brush(Ui.Ink), Background = Ui.Brush("#FFFFFF"),
            BorderThickness = new(0), CornerRadius = new(6), Padding = new(13, 9, 13, 7), HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(income, "Текущий доход, рублей");
        FilterBudgetMoney(income);
        var incomeError = Ui.Text("", 13, Palette.TaskForegrounds[0]); incomeError.Name = "BudgetIncomeError";
        incomeError.Visibility = Visibility.Collapsed;
        void SaveIncome()
        {
            string? error = _vm.SaveBudgetIncome(income.Text);
            incomeError.Text = error ?? ""; incomeError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
            if (error is null) income.Text = _vm.Budget.Income.ToString("F2", PlannerViewModel.Russian);
        }
        income.LostFocus += (_, _) => SaveIncome();
        income.KeyDown += (_, args) => { if (args.Key == Windows.System.VirtualKey.Enter) { SaveIncome(); args.Handled = true; } };
        var incomeBlock = new StackPanel { Spacing = 8 };
        incomeBlock.Children.Add(Ui.Text("Текущий доход", 21, Ui.Blue, true)); incomeBlock.Children.Add(income); incomeBlock.Children.Add(incomeError);
        left.Children.Add(incomeBlock);

        var mandatory = Ui.Text("", 22, Palette.Elements[2], true); mandatory.Name = "BudgetMandatoryAmount";
        var paymentRow = new Grid { ColumnSpacing = 9 };
        paymentRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); paymentRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); paymentRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(paymentRow, Ui.OriginalIcon("YellowAccent.png", 21));
        Ui.Add(paymentRow, Ui.Text("Ежемесячные платежи", 15, "#FFFFFF"), 1); Ui.Add(paymentRow, mandatory, 2);
        var mandatoryBlock = new StackPanel { Spacing = 8 };
        mandatoryBlock.Children.Add(Ui.Text("Обязательные платежи", 21, Ui.Blue, true));
        mandatoryBlock.Children.Add(new Border { Child = paymentRow, Background = Ui.Brush(Ui.Accent), Padding = new(10, 7, 10, 7), CornerRadius = new(5) });
        left.Children.Add(mandatoryBlock);

        var categories = new StackPanel { Spacing = 6, MinHeight = 296 };
        categories.Children.Add(Ui.Text("Категории", 21, Ui.Blue, true));
        var columnTitles = CategoryRowGrid(); columnTitles.Margin = new(0, 9, 0, 2);
        Ui.Add(columnTitles, Ui.Text("Название", 12, Ui.Muted), 1);
        Ui.Add(columnTitles, RightText("Плановый лимит", 12), 2); Ui.Add(columnTitles, RightText("% дохода", 12), 3);
        categories.Children.Add(columnTitles);
        var percents = new List<(BudgetCategory Category, TextBlock Text)>();
        foreach (var category in _vm.BudgetCategories)
        {
            var row = CategoryRowGrid(); row.Name = "BudgetCategoryRow"; row.Tag = category.Id;
            Ui.Add(row, Ui.Dot(category.Color, true, 13));
            var title = Ui.Text(category.Name, 16); title.MaxLines = 2; title.TextTrimming = TextTrimming.CharacterEllipsis; Ui.Add(row, title, 1);
            Ui.Add(row, RightText(FormatAmount(category.Limit), 17, true), 2);
            var percent = RightText("", 14); percents.Add((category, percent)); Ui.Add(row, percent, 3);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(Ui.Button(Ui.Icon("\uE70F", 16), _vm.EditBudgetCategoryCommand, category.Id, "Редактировать категорию «" + category.Name + "»"));
            actions.Children.Add(Ui.Button(Ui.Icon("\uE74D", 16), _vm.DeleteBudgetCategoryCommand, category.Id, "Удалить категорию «" + category.Name + "»")); Ui.Add(row, actions, 4);
            categories.Children.Add(new Border { Child = row, Padding = new(0, 9, 0, 9), BorderThickness = new(0, 0, 0, 1), BorderBrush = Ui.Brush(Ui.Paper) });
        }
        if (_vm.BudgetCategories.Count == 0)
        {
            var empty = Ui.Text("Добавьте категории и задайте плановые лимиты.", 15); empty.Margin = new(0, 12, 0, 5); categories.Children.Add(empty);
        }
        var plus = Ui.Button(Ui.Icon("\uE710", 15), _vm.CreateBudgetCategoryCommand, tooltip: "Добавить категорию бюджета");
        plus.Name = "AddBudgetCategory"; plus.Height = 32; plus.Padding = new(0); plus.Background = Ui.Brush(Ui.Paper);
        plus.HorizontalAlignment = HorizontalAlignment.Stretch; plus.Opacity = 0; categories.Children.Add(plus);
        var categoryCard = Ui.Card(categories); categoryCard.Padding = new(19, 17, 19, 13);
        bool pointerInside = false;
        categoryCard.PointerEntered += (_, _) => { pointerInside = true; plus.Opacity = 1; };
        categoryCard.PointerExited += (_, _) => { pointerInside = false; if (plus.FocusState == FocusState.Unfocused) plus.Opacity = 0; };
        categoryCard.GotFocus += (_, _) => plus.Opacity = 1;
        categoryCard.LostFocus += (_, _) => { if (!pointerInside) plus.Opacity = 0; };
        left.Children.Add(categoryCard); Ui.Add(board, left);

        var right = new StackPanel { Spacing = 20, Margin = new(0, 0, 0, 12) };
        var pie = new Grid { Width = 320, Height = 320, Name = "BudgetPie" };
        var pieBox = new Viewbox { Child = pie, MaxWidth = 354, Height = 354, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
        right.Children.Add(pieBox);
        var legend = new Grid { ColumnSpacing = 12, RowSpacing = 8, Margin = new(8, 0, 8, 1) };
        legend.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); legend.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); right.Children.Add(legend);
        var total = RightText("", 28, true); total.Name = "BudgetTotal";
        var remaining = RightText("", 28, true); remaining.Name = "BudgetRemaining";
        right.Children.Add(BudgetTotalRow("Всего трат", total)); right.Children.Add(BudgetTotalRow("Остаток (план)", remaining));
        var warning = Ui.Text("", 13, Palette.TaskForegrounds[0]); warning.Name = "BudgetWarning"; right.Children.Add(warning); Ui.Add(board, right, 1);
        _refreshFinanceValues = () =>
        {
            var budget = _vm.Budget;
            mandatory.Text = FormatAmount(budget.MandatoryPayments); total.Text = FormatAmount(budget.Total); remaining.Text = FormatAmount(budget.Remaining);
            foreach (var item in percents) item.Text.Text = FormatPercent(budget.Percent(item.Category.Limit));
            DrawBudgetPie(pie, budget, _vm.BudgetCategories);
            FillBudgetLegend(legend, budget, _vm.BudgetCategories);
            warning.Text = budget.Remaining < 0 ? "План превышает доход на " + FormatAmount(-budget.Remaining) + ". Уменьшите лимиты или измените доход, чтобы увидеть распределение." : "";
            warning.Visibility = budget.Remaining < 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        _refreshFinanceValues(); return Ui.Scroll(board);
    }
    private static Grid CategoryRowGrid()
    {
        var row = new Grid { ColumnSpacing = 9 };
        foreach (var width in new[] { new GridLength(16), new GridLength(1, GridUnitType.Star), new GridLength(120), new GridLength(64), new GridLength(59) }) row.ColumnDefinitions.Add(new() { Width = width });
        return row;
    }
    private static TextBlock RightText(string text, double size, bool bold = false)
    {
        var label = Ui.Text(text, size, Ui.Ink, bold); label.HorizontalAlignment = HorizontalAlignment.Right; label.TextAlignment = TextAlignment.Right; return label;
    }
    private static Grid BudgetTotalRow(string label, TextBlock amount)
    {
        var row = new Grid { ColumnSpacing = 18 };
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(row, Ui.Text(label, 21, Ui.Blue, true)); Ui.Add(row, amount, 1); return row;
    }
    private static string FormatPercent(decimal value) => value.ToString("0.##", PlannerViewModel.Russian) + "%";

    private static void DrawBudgetPie(Grid host, BudgetSummary budget, IReadOnlyList<BudgetCategory> categories)
    {
        host.Children.Clear(); host.Tag = budget;
        host.Children.Add(new Ellipse { Name = "BudgetPieBase", Fill = Ui.Brush("#DFDCDA"), Margin = new(1) });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(host, "Плановое распределение дохода: " + FormatAmount(budget.Income));
        if (budget.Income <= 0 || budget.Remaining < 0) return;
        double start = -Math.PI / 2;
        void AddSector(string label, decimal amount, string color)
        {
            if (amount <= 0) return;
            double sweep = (double)(amount / budget.Income) * Math.PI * 2;
            Shape sector;
            if (sweep >= Math.PI * 2 - 1e-10) sector = new Ellipse { Margin = new(1) };
            else
            {
                Point OnCircle(double angle) => new(160 + 159 * Math.Cos(angle), 160 + 159 * Math.Sin(angle));
                var figure = new PathFigure { StartPoint = new(160, 160), IsClosed = true, IsFilled = true };
                figure.Segments.Add(new LineSegment { Point = OnCircle(start) });
                figure.Segments.Add(new ArcSegment { Point = OnCircle(start + sweep), Size = new Size(159, 159), IsLargeArc = sweep > Math.PI, SweepDirection = SweepDirection.Clockwise });
                var geometry = new PathGeometry(); geometry.Figures.Add(figure);
                sector = new Microsoft.UI.Xaml.Shapes.Path { Data = geometry };
            }
            sector.Fill = Ui.Brush(color); sector.Tag = "BudgetSector";
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sector, label + ": " + FormatAmount(amount) + ", " + FormatPercent(budget.Percent(amount)));
            host.Children.Add(sector); start += sweep;
        }
        AddSector("Обязательные платежи", budget.MandatoryPayments, Ui.Accent);
        foreach (var category in categories) AddSector(category.Name, category.Limit, category.Color);
    }
    private static void FillBudgetLegend(Grid host, BudgetSummary budget, IReadOnlyList<BudgetCategory> categories)
    {
        host.Children.Clear(); host.RowDefinitions.Clear();
        var items = new List<(string Name, decimal Amount, string Color)> { ("Платежи", budget.MandatoryPayments, Ui.Accent) };
        items.AddRange(categories.Select(c => (c.Name, c.Limit, c.Color)));
        if (budget.Remaining >= 0) items.Add(("Остаток", budget.Remaining, "#DFDCDA"));
        for (int i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0) host.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var item = items[i]; var line = new Grid { ColumnSpacing = 6 };
            line.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); line.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            Ui.Add(line, Ui.Dot(item.Color, true, 10)); var text = Ui.Text(item.Name + " · " + FormatPercent(budget.Percent(item.Amount)), 12);
            text.MaxLines = 1; text.TextTrimming = TextTrimming.CharacterEllipsis; Ui.Add(line, text, 1); Ui.Add(host, line, i % 2, i / 2);
        }
    }
    private static void FilterBudgetMoney(TextBox field) => field.BeforeTextChanging += (_, args) => args.Cancel = !BudgetMoney.IsInput(args.NewText);

    private async void EditBudgetCategory(object? sender, BudgetCategory? category)
    {
        if (!OwnsDialogs) return;
        if (category is null && _vm.BudgetCategories.Count >= PlannerEngine.MaximumBudgetCategories) { await ErrorDialog("Не более 7 категорий"); return; }
        var name = new TextBox { Name = "BudgetCategoryName", Header = "Название", Text = category?.Name ?? "", MaxLength = 120, PlaceholderText = "Например, продукты" };
        var amount = new TextBox { Name = "BudgetCategoryLimit", Header = "Плановый лимит, ₽", Text = category?.Limit.ToString("F2", PlannerViewModel.Russian) ?? "", PlaceholderText = "0,00" };
        FilterBudgetMoney(amount);
        var nameError = Ui.Text("", 13, Palette.TaskForegrounds[0]); nameError.Visibility = Visibility.Collapsed;
        var amountError = Ui.Text("", 13, Palette.TaskForegrounds[0]); amountError.Name = "BudgetCategoryError"; amountError.Visibility = Visibility.Collapsed;
        var nameGroup = new StackPanel { Spacing = 4 }; nameGroup.Children.Add(name); nameGroup.Children.Add(nameError);
        var amountGroup = new StackPanel { Spacing = 4 }; amountGroup.Children.Add(amount); amountGroup.Children.Add(amountError);
        var fields = new StackPanel { Spacing = 17, MinWidth = 360 }; fields.Children.Add(nameGroup); fields.Children.Add(amountGroup);
        var dialog = Ui.Dialog(XamlRoot, category is null ? "Новая категория" : "Редактировать категорию", fields, scale: _scaleHost.ScaleFactor);
        dialog.PrimaryButtonClick += (_, args) =>
        {
            bool validName = !string.IsNullOrWhiteSpace(name.Text);
            nameError.Text = validName ? "" : "Введите название"; nameError.Visibility = validName ? Visibility.Collapsed : Visibility.Visible;
            if (!validName) { args.Cancel = true; return; }
            string? error = _vm.SaveBudgetCategory(category?.Id, name.Text, amount.Text);
            amountError.Text = error ?? ""; amountError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
            args.Cancel = error is not null;
        };
        await dialog.ShowAsync();
    }
}
