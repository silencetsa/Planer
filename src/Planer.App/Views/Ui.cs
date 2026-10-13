using System.Windows.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Planer.Core;
using Windows.UI;

namespace Planer_App.Views;

public class HandButton : Button
{
    public HandButton() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
}
public static class Ui
{
    public const string Accent = "#C780E8", Muted = "#99908C", Ink = "#55504E", Paper = "#F5F5F5";
    public static SolidColorBrush Brush(string hex)
    {
        hex = hex.TrimStart('#'); byte a = 255;
        if (hex.Length == 8) { a = Convert.ToByte(hex[..2], 16); hex = hex[2..]; }
        return new(Color.FromArgb(a, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16)));
    }
    public static TextBlock Text(string value, double size = 13, string color = Ink, bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = Brush(color), FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    public static HandButton Button(object content, ICommand? command = null, object? parameter = null, string? tooltip = null)
    {
        var b = new HandButton { Content = content, Command = command, CommandParameter = parameter, Background = Brush("#00FFFFFF"), BorderThickness = new(0),
            Padding = new(5), CornerRadius = new(5), MinHeight = 0, MinWidth = 0 };
        if (tooltip is not null) { ToolTipService.SetToolTip(b, tooltip); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, tooltip); }
        return b;
    }
    public static Border Card(UIElement child, double radius = 17) => new() { Background = Brush("#FFFFFF"), CornerRadius = new(radius), Child = child, Padding = new(13) };
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
    public static Canvas MoodIcon(Mood mood, bool selected, double size = 23)
    {
        var color = Palette.MoodColors[(int)mood]; var line = selected ? "#FFFFFF" : color;
        var canvas = new Canvas { Width = 23, Height = 23 };
        canvas.Children.Add(new Ellipse { Width = 20, Height = 20, Margin = new(1.5), Stroke = Brush(color), StrokeThickness = 1.7, Fill = Brush(selected ? color : "#00FFFFFF") });
        if (mood == Mood.Angry) canvas.Children.Add(StrokePath("M 5,7 L 8,9 M 15,9 L 18,7 M 6,16 Q 11.5,11 17,16", line));
        else
        {
            foreach (double x in new[] { 6.7, 14.1 })
            {
                var eye = new Ellipse { Width = 2.2, Height = 2.2, Fill = Brush(line) }; Canvas.SetLeft(eye, x); Canvas.SetTop(eye, 7.1); canvas.Children.Add(eye);
            }
            canvas.Children.Add(StrokePath(mood switch
            {
                Mood.Happy => "M 5.5,12.5 Q 11.5,22 17.5,12.5 Z M 6,13 L 17,13", Mood.Good => "M 6,13 Q 11.5,20 17,13",
                Mood.Sad => "M 6,16 Q 11.5,10 17,16", _ => "M 7,15 L 16,15"
            }, line));
        }
        if (size != 23) { canvas.RenderTransform = new ScaleTransform { ScaleX = size / 23, ScaleY = size / 23 }; canvas.Width = size; canvas.Height = size; }
        return canvas;
    }
    public static Canvas PartIcon(DayPart part)
    {
        var c = new Canvas { Width = 17, Height = 17, Opacity = .65, HorizontalAlignment = HorizontalAlignment.Left };
        c.Children.Add(StrokePath(part switch
        {
            DayPart.Morning => "M 3,10 C 3,2 14,2 14,10 M 1,11 L 16,11 M 4,14 L 13,14",
            DayPart.Afternoon => "M 11,5 A 4,4 0 1 1 5,11 A 4,4 0 0 1 11,5 M 8.5,0 L 8.5,2 M 8.5,15 L 8.5,17 M 0,8.5 L 2,8.5 M 15,8.5 L 17,8.5 M 2,2 L 3.5,3.5 M 13.5,13.5 L 15,15 M 2,15 L 3.5,13.5 M 13.5,3.5 L 15,2",
            _ => "M 6,2 C 1,5 2,14 10,14 C 13,14 15,12 16,9 C 7,12 4,6 6,2 Z"
        }, Muted, 1.4));
        return c;
    }
}
