using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planer_App.Views;

public static partial class Ui
{
    internal static FrameworkElement LayoutRecoveryMessage(Action retry)
    {
        var content = new StackPanel { Spacing = 18, MaxWidth = 420, Margin = new(20),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(Text("Не удалось обновить экран", GoalTitleSize, Accent, true));
        content.Children.Add(Text("Планер остаётся открытым. Можно повторить загрузку экрана.", 14, Muted));
        var button = PeriodButton("Восстановить планер", new RelayCommand(retry));
        button.Name = "RetryPlannerLayout"; button.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(button); return content;
    }
}
