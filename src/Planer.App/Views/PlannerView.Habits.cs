using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private UIElement ManagementHeader()
    {
        var row = new Grid { ColumnSpacing = 14 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Ui.Add(row, Ui.Button(Ui.Icon("\uE72B", 20), _vm.NavigateCommand, PlannerScreen.Week, "Вернуться в планер недели"));
        Ui.Add(row, Ui.Text(_vm.Period, 26, Ui.Accent, true), 1);
        var create = Ui.Button(Ui.Text("Создать", 14, "#FFFFFF", true), _vm.Screen == PlannerScreen.Habits ? _vm.CreateHabitCommand : _vm.CreatePaymentCommand);
        create.Background = Ui.Brush(Ui.Accent); create.Padding = new(22, 9, 22, 9); Ui.Add(row, create, 2); return row;
    }
    private UIElement HabitsBoard()
    {
        var content = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(Ui.Text($"Активных привычек: {_vm.Habits.Count(h => h.Ended is null)} из 3", 13, Ui.Muted));
        if (_vm.Habits.Count == 0) content.Children.Add(Ui.Card(Ui.Text("Создайте первую привычку. Её точки появятся в планере начиная с сегодняшнего дня.", 15)));
        bool? previousGroup = null;
        foreach (var h in _vm.Habits)
        {
            bool active = h.Ended is null;
            if (previousGroup != active) { content.Children.Add(Ui.Text(active ? "Активные" : "Завершённые", 18, Ui.Accent, true)); previousGroup = active; }
            var row = new Grid { ColumnSpacing = 20 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Ui.Add(row, Ui.Dot(Palette.HabitColor(h.Color), true, 22));
            var info = new StackPanel { Spacing = 5 }; info.Children.Add(Ui.Text(h.Name, 19, Ui.Ink, true));
            info.Children.Add(Ui.Text($"Создана {h.Created:dd.MM.yyyy}   ·   Активных дней: {h.ActiveDays(_vm.Today)}   ·   Успешных отметок: {_vm.HabitSuccess(h.Id)}", 14, Ui.Muted));
            if (h.Ended is { } ended) info.Children.Add(Ui.Text($"Завершена {ended:dd.MM.yyyy}", 14, Ui.Muted));
            Ui.Add(row, info, 1);
            if (active)
            {
                var finish = Ui.Button(Ui.Text("Завершить", 13, Ui.Muted), _vm.FinishHabitCommand, h.Id); finish.Background = Ui.Brush(Ui.Paper); finish.Padding = new(15, 8, 15, 8); Ui.Add(row, finish, 2);
            }
            content.Children.Add(Ui.Card(row));
        }
        return Ui.Scroll(content);
    }
    private async void CreateHabit(object? sender, EventArgs args)
    {
        if (!OwnsDialogs) return;
        if (_vm.Habits.Count(h => h.Ended is null) >= 3) { await ErrorDialog("Не более 3 активных привычек"); return; }
        var name = new TextBox { Header = "Название", PlaceholderText = "До 13 знаков", MaxLength = 26 };
        string selectedColor = Palette.Elements[3];
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var colorButtons = new List<HandButton>();
        foreach (var color in Palette.Elements)
        {
            var b = Ui.Button(Ui.Dot(color, true, 23), tooltip: color); b.Padding = new(4); b.BorderThickness = new(2);
            b.BorderBrush = Ui.Brush(color == selectedColor ? Ui.Accent : "#00FFFFFF");
            b.Click += (_, _) => { selectedColor = color; foreach (var other in colorButtons) other.BorderBrush = Ui.Brush(ReferenceEquals(other, b) ? Ui.Accent : "#00FFFFFF"); };
            colorButtons.Add(b); colors.Children.Add(b);
        }
        var error = Ui.Text("", 12, "#BD3026");
        var fields = new StackPanel { Spacing = 14, MinWidth = 340 }; fields.Children.Add(name); fields.Children.Add(Ui.FieldTitle("Цвет")); fields.Children.Add(colors); fields.Children.Add(error);
        var dialog = Ui.Dialog(XamlRoot, "Новая привычка", fields, primary: "Создать", scale: _scaleHost.ScaleFactor);
        dialog.PrimaryButtonClick += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || new StringInfo(name.Text.Trim()).LengthInTextElements > 13) { error.Text = "Название — от 1 до 13 знаков"; e.Cancel = true; }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.AddHabit(name.Text, selectedColor);
    }
    private async void FinishHabit(object? sender, Guid id)
    {
        if (!OwnsDialogs) return;
        var name = _vm.Habits.First(h => h.Id == id).Name;
        var dialog = Ui.Dialog(XamlRoot, $"Завершить привычку «{name}»?",
            Ui.Text("Точки исчезнут начиная с сегодняшнего дня. Прошлые отметки сохранятся. Для нового начала потребуется создать новую привычку.", 14, Ui.Muted),
            primary: "Завершить", scale: _scaleHost.ScaleFactor, defaultClose: true);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.FinishHabit(id);
    }
}
