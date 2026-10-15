using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Planer_App.Views;

/// <summary>Compute layout from the parent's constraint, without SizeChanged feeding back into it.</summary>
public sealed class PlannerScaleHost : Panel
{
    private readonly FrameworkElement _surface;
    private readonly bool _floating;
    private readonly ScaleTransform _transform = new();
    public double ScaleFactor { get; private set; } = 1;
    public PlannerScaleHost(FrameworkElement surface, bool floating)
    {
        _surface = surface; _floating = floating;
        surface.RenderTransform = _transform;
        surface.RenderTransformOrigin = new Point(0, 0);
        Children.Add(surface);
    }
    private (Size Logical, double Scale) LayoutFor(Size size)
    {
        double width = double.IsFinite(size.Width) ? Math.Max(0, size.Width) : _floating ? 320 : 1384;
        double height = double.IsFinite(size.Height) ? Math.Max(0, size.Height) : _floating ? 568 : 809;
        double minimumWidth = _floating ? 280 : 1100, minimumHeight = _floating ? 360 : 650;
        double scale = Math.Min(1, Math.Min(width / minimumWidth, height / minimumHeight));
        if (scale >= 1 && !_floating) scale = Math.Max(1, Math.Min(width / 1384, height / 809));
        scale = Math.Max(.01, scale);
        return (new(width / scale, height / scale), scale);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = LayoutFor(availableSize);
        _surface.Measure(layout.Logical);
        return new(layout.Logical.Width * layout.Scale, layout.Logical.Height * layout.Scale);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = LayoutFor(finalSize); ScaleFactor = layout.Scale;
        _surface.Arrange(new Rect(new Point(), layout.Logical));
        _transform.ScaleX = _transform.ScaleY = layout.Scale;
        return finalSize;
    }
}
