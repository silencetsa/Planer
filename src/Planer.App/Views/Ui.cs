using System.Windows.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;
using Windows.UI;

namespace Planer_App.Views;

public class HandButton : Button
{
    public HandButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        FontFamily = Ui.PlannerFont;
        CharacterSpacing = Ui.SmallTextSpacing;
    }
}
public static partial class Ui
{
    public const string Accent = "#C780E8", Blue = "#59ADF6", Orange = "#FFB480", Muted = "#99908C", Ink = Muted, Paper = "#F5F5F5";
    public const double GoalTitleSize = 21;
    public const int SmallTextSpacing = 25;
    public static FontFamily PlannerFont { get; } = new("ms-appx:///Assets/Fonts/HelveticaNeue-Roman.otf#Helvetica Neue");
    public static FontFamily PlannerFontBold { get; } = new("ms-appx:///Assets/Fonts/HelveticaNeue-Bold.otf#Helvetica Neue");
    public static FontFamily PlannerFontLight { get; } = new("ms-appx:///Assets/Fonts/HelveticaNeue-Light.otf#Helvetica Neue");
    public static SolidColorBrush Brush(string hex)
    {
        hex = hex.TrimStart('#'); byte a = 255;
        if (hex.Length == 8) { a = Convert.ToByte(hex[..2], 16); hex = hex[2..]; }
        return new(Color.FromArgb(a, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16)));
    }
    public static TextBlock Text(string value, double size = 13, string color = Ink, bool bold = false, bool light = false) => new()
    {
        Text = value, FontFamily = bold ? PlannerFontBold : light ? PlannerFontLight : PlannerFont, FontSize = size, Foreground = Brush(color), FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : light ? Microsoft.UI.Text.FontWeights.Light : Microsoft.UI.Text.FontWeights.Normal,
        CharacterSpacing = size < GoalTitleSize ? SmallTextSpacing : 0,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    public static HandButton Button(object content, ICommand? command = null, object? parameter = null, string? tooltip = null)
    {
        var b = new HandButton { Content = content, Command = command, CommandParameter = parameter, Background = Brush("#00FFFFFF"), BorderThickness = new(0),
            Padding = new(5), CornerRadius = new(5), MinHeight = 0, MinWidth = 0 };
        if (tooltip is not null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, tooltip);
        return b;
    }
    public static Border Card(UIElement child, double radius = 17) => new() { Background = Brush("#FFFFFF"), CornerRadius = new(radius), Child = child, Padding = new(13) };
    public static HandButton PeriodButton(string title, ICommand? command = null, string? name = null, string foreground = Accent, string background = "#F3E3FA")
    {
        var button = Button(Text(title, GoalTitleSize, foreground, true), command, tooltip: name);
        button.Background = Brush(background); button.Padding = new(12, 7, 12, 7);
        button.Resources["ButtonBackgroundPointerOver"] = button.Background;
        button.Resources["ButtonBackgroundPressed"] = button.Background;
        return button;
    }
    public static Grid Grid(int columns, int rows = 1, double gap = 10)
    {
        var g = new Grid { ColumnSpacing = gap, RowSpacing = gap };
        for (int i = 0; i < columns; i++) g.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        for (int i = 0; i < rows; i++) g.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        return g;
    }
    public static void Add(Grid grid, UIElement child, int column = 0, int row = 0, int span = 1)
    {
        var element = (FrameworkElement)child;
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(element, column); Microsoft.UI.Xaml.Controls.Grid.SetRow(element, row);
        Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(element, span); grid.Children.Add(child);
    }
    public static FontIcon Icon(string glyph, double size = 16, string color = Muted) => new() { Glyph = glyph, FontSize = size, Foreground = Brush(color) };
    public static ScrollViewer Scroll(UIElement content) => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollMode = ScrollMode.Auto,
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
    public static Ellipse Dot(string color, bool filled, double size = 14) => new() { Width = size, Height = size, Fill = Brush(filled ? color : "#00FFFFFF"), Stroke = Brush(color), StrokeThickness = 1.3 };
    private static Microsoft.UI.Xaml.Shapes.Path StrokePath(string data, string color, double thickness = 1.6) => (Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load(
        $"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}' Stroke='{color}' StrokeThickness='{thickness.ToString(System.Globalization.CultureInfo.InvariantCulture)}' StrokeStartLineCap='Round' StrokeEndLineCap='Round' />");
    private static readonly string[] MoodOutlineImages = ["GreenPanel.png", "YellowPanelSoft.png", "RedPanel.png", "BluePanel.png", "PurplePanelWarm.png"];
    private static readonly string[] MoodSelectedImages = ["GreenAccent.png", "YellowPanel.png", "RedAccent.png", "BlueAccent.png", "PurplePanel.png"];
    internal sealed record OriginalImageLoad(string Name, Task Ready);
    public static Image OriginalIcon(string name, double size)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false,
            Tag = new OriginalImageLoad(name, ready.Task) };
        image.ImageOpened += (_, _) => ready.TrySetResult();
        image.ImageFailed += (_, e) => ready.TrySetException(new InvalidOperationException($"Не загрузилась иконка {name}: {e.ErrorMessage}"));
        image.Source = new BitmapImage(new Uri("ms-appx:///Assets/Originals/" + name));
        return image;
    }
    public static Image MoodIcon(Mood mood, bool selected, double size = 27) =>
        OriginalIcon((selected ? MoodSelectedImages : MoodOutlineImages)[(int)mood], size);
    public static Image PartIcon(DayPart part) => OriginalIcon(part switch
    {
        DayPart.Morning => "GrayPanelAlt.png", DayPart.Afternoon => "GrayPanel.png", _ => "GrayAccent.png"
    }, part == DayPart.Evening ? 15 : 17);
    public static Canvas PeriodArrow(bool forward)
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(StrokePath(forward ? "M 8,4 L 16,12 L 8,20" : "M 16,4 L 8,12 L 16,20", "#E7D92B", 3));
        return canvas;
    }
}
