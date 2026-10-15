using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;

namespace Planer_App.Views;

public sealed class PrioritySwitch : ToggleButton
{
    public PrioritySwitch(bool value, string name)
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Width = 44; Height = 24; Padding = new(0); BorderThickness = new(0); MinWidth = 0; MinHeight = 0;
        Template = (ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ToggleButton">
                <ContentPresenter Content="{TemplateBinding Content}" HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch" />
            </ControlTemplate>
            """);
        var thumb = new Ellipse { Width = 18, Height = 18, Fill = Ui.Brush("#FFFFFF"), HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        Content = new Border { Background = Ui.Brush("#D6D3D1"), CornerRadius = new(12), Padding = new(3), Child = thumb };
        IsChecked = value;
        Checked += (_, _) => thumb.HorizontalAlignment = HorizontalAlignment.Right;
        Unchecked += (_, _) => thumb.HorizontalAlignment = HorizontalAlignment.Left;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, name);
    }
}

public static partial class Ui
{
    private static readonly HashSet<ContentDialog> OpenDialogs = [];
    internal static ContentDialog? OpenDialogForRoot(XamlRoot root) => OpenDialogs.FirstOrDefault(d => ReferenceEquals(d.XamlRoot, root));
    internal static void CloseDialogsForLayoutRecovery()
    {
        foreach (var dialog in OpenDialogs.ToArray()) dialog.Hide();
    }
    public static TextBlock FieldTitle(string title) => Text(title, 15, Blue, true);
    public static Grid PriorityRow(string left, PrioritySwitch toggle, string right)
    {
        var row = Grid(3, gap: 12); row.ColumnDefinitions[1].Width = GridLength.Auto;
        var leftText = Text(left, 14, Muted); leftText.HorizontalAlignment = HorizontalAlignment.Right;
        Add(row, leftText); Add(row, toggle, 1); Add(row, Text(right, 14, Muted), 2);
        return row;
    }
    public static ContentDialog Dialog(XamlRoot root, string title, UIElement content, string primary = "Сохранить", string close = "Отмена", double scale = 1, bool defaultClose = false, bool destructive = false)
    {
        PrepareFields(content);
        var dialog = new ContentDialog
        {
            XamlRoot = root, RequestedTheme = ElementTheme.Light, Background = Brush(Paper), Foreground = Brush(Muted),
            FontFamily = PlannerFont, CharacterSpacing = SmallTextSpacing,
            Title = Text(title, GoalTitleSize * scale, Accent, true), Content = content,
            PrimaryButtonText = primary, CloseButtonText = close,
            DefaultButton = string.IsNullOrEmpty(primary) || defaultClose ? ContentDialogButton.Close : ContentDialogButton.Primary,
            PrimaryButtonStyle = DialogButtonStyle(Palette.TaskBackgrounds[destructive ? 0 : 2], Palette.TaskForegrounds[destructive ? 0 : 2], scale),
            CloseButtonStyle = DialogButtonStyle(Palette.TaskBackgrounds[0], Palette.TaskForegrounds[0], scale)
        };
        dialog.Opened += (_, _) => OpenDialogs.Add(dialog);
        dialog.Closed += (_, _) => OpenDialogs.Remove(dialog);
        foreach (string key in new[] { "ContentDialogBackground", "ContentDialogTopOverlay" }) dialog.Resources[key] = Brush(Paper);
        dialog.Resources["ContentDialogSeparatorBorderBrush"] = Brush("#00FFFFFF");
        // WinUI applies AccentButtonStyle to the default action, even when its button style is set.
        int defaultKind = string.IsNullOrEmpty(primary) || defaultClose || destructive ? 0 : 2;
        foreach (string suffix in new[] { "", "PointerOver", "Pressed", "Disabled" })
        {
            dialog.Resources["AccentButtonBackground" + suffix] = Brush(Palette.TaskBackgrounds[defaultKind]);
            dialog.Resources["AccentButtonForeground" + suffix] = Brush(Palette.TaskForegrounds[defaultKind]);
            dialog.Resources["AccentButtonBorderBrush" + suffix] = Brush("#00FFFFFF");
        }
        dialog.Resources["ControlContentThemeFontSize"] = 14d * scale;
        foreach (string key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed", "CalendarDatePickerBackground", "CalendarDatePickerBackgroundPointerOver", "CalendarDatePickerBackgroundPressed" })
            dialog.Resources[key] = Brush("#FFFFFF");
        foreach (string key in new[] { "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused", "TextControlPlaceholderForegroundDisabled", "CalendarDatePickerTextForeground", "CalendarDatePickerTextForegroundSelected", "CalendarDatePickerTextForegroundPointerOver", "CalendarDatePickerTextForegroundPressed", "CalendarDatePickerCalendarGlyphForeground" })
            dialog.Resources[key] = Brush(Muted);
        if (scale > 1)
        {
            double width = content is FrameworkElement element ? Math.Max(340, element.MinWidth) : 340;
            var body = new Border { Child = content, Width = width };
            dialog.Content = new Viewbox { Child = body, Width = width * scale, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
            dialog.Resources["ContentDialogMaxWidth"] = 548d * scale;
            dialog.Resources["ContentDialogMinWidth"] = (width + 48) * scale;
        }
        return dialog;
    }
    private static void PrepareFields(UIElement element)
    {
        if (element is Control control) { control.FontFamily = PlannerFont; control.CharacterSpacing = SmallTextSpacing; control.Foreground = Brush(Muted); }
        switch (element)
        {
            case TextBox field:
                field.Background = Brush("#FFFFFF"); field.PlaceholderForeground = Brush(Muted); field.FontSize = 14;
                if (field.Header is string textCaption) field.Header = FieldTitle(textCaption);
                break;
            case ComboBox field:
                field.Background = Brush("#FFFFFF"); field.FontSize = 14;
                if (field.Header is string comboCaption) field.Header = FieldTitle(comboCaption);
                break;
            case CalendarDatePicker field:
                field.Background = Brush("#FFFFFF"); field.FontSize = 14;
                if (field.Header is string dateCaption) field.Header = FieldTitle(dateCaption);
                break;
            case Panel panel:
                foreach (var child in panel.Children) PrepareFields(child);
                break;
            case Border { Child: { } child }:
                PrepareFields(child); break;
        }
    }
    private static Style DialogButtonStyle(string background, string foreground, double scale)
    {
        var style = new Style { TargetType = typeof(Button) };
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(background)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(foreground)));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, PlannerFont));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 14d * scale));
        style.Setters.Add(new Setter(Control.CharacterSpacingProperty, SmallTextSpacing));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14 * scale, 7 * scale, 14 * scale, 7 * scale)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(5)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
                <Border x:Name="Chrome" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                            <VisualState x:Name="Normal" />
                            <VisualState x:Name="PointerOver"><VisualState.Setters><Setter Target="Chrome.Opacity" Value="0.85" /></VisualState.Setters></VisualState>
                            <VisualState x:Name="Pressed"><VisualState.Setters><Setter Target="Chrome.Opacity" Value="0.7" /></VisualState.Setters></VisualState>
                            <VisualState x:Name="Disabled"><VisualState.Setters><Setter Target="Chrome.Opacity" Value="0.45" /></VisualState.Setters></VisualState>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>
                    <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Foreground="{TemplateBinding Foreground}" FontFamily="{TemplateBinding FontFamily}" FontSize="{TemplateBinding FontSize}" CharacterSpacing="{TemplateBinding CharacterSpacing}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
                </Border>
            </ControlTemplate>
            """)));
        return style;
    }
}
