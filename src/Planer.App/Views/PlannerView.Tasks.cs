using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer_App.ViewModels;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private static readonly string[] TaskRepeatNames = ["Нет", "Каждую неделю", "Каждые 2 недели", "Каждый месяц", "Каждый год"];

    private async void DeleteTask(object? sender, PlannerTask task)
    {
        if (!OwnsDialogs) return;
        var body = new StackPanel { Spacing = 12, MinWidth = 340 };
        var (contents, text) = TaskContents(task);
        text.MaxLines = 0; text.TextTrimming = TextTrimming.None;
        body.Children.Add(new Border { Name = "DeleteTaskPreview", Background = Ui.Brush(TaskBackground(task)),
            CornerRadius = new(4), Padding = new(7, 5, 7, 5), Child = new ScrollViewer { Content = contents, MaxHeight = 180,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        body.Children.Add(Ui.Text(task.SeriesId is null ? "Удалить эту задачу?" :
            "Будут удалены эта задача и все следующие задачи в цепочке. Предыдущие задачи и их отметки сохранятся.", 14));
        var dialog = Ui.Dialog(XamlRoot, "Удалить задачу?", body, primary: "Удалить", scale: _scaleHost.ScaleFactor, defaultClose: true, destructive: true);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) _vm.DeleteTask(task.Id);
    }

    private async void ChooseTaskMoveScope(object? sender, TaskMove move)
    {
        if (!OwnsDialogs) return;
        var group = Guid.NewGuid().ToString();
        var onlyThis = new RadioButton { Content = Ui.Text("Только эту задачу", 14), IsChecked = true, GroupName = group };
        var allFuture = new RadioButton { Content = Ui.Text("Эту и все будущие задачи", 14), GroupName = group };
        var choice = new StackPanel { Spacing = 6 }; choice.Children.Add(onlyThis); choice.Children.Add(allFuture);
        var body = new StackPanel { Spacing = 14, MinWidth = 340 };
        body.Children.Add(Ui.Text(move.Date is { } date ? $"Новая дата: {date:dd.MM.yyyy}" : "Перенести в «Нужно распределить»", 14));
        body.Children.Add(choice);
        if (move.Date is null) body.Children.Add(Ui.Text("Для всей цепочки повтор приостановится до назначения новой даты.", 14));
        var dialog = Ui.Dialog(XamlRoot, "Перенести задачу", body, primary: "Перенести", scale: _scaleHost.ScaleFactor);
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _vm.MoveTask(move, allFuture.IsChecked == true ? TaskMoveScope.ThisAndFuture : TaskMoveScope.Occurrence);
    }
}
