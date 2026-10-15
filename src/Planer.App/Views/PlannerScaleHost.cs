using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Planer_App.Views;

/// <summary>Uniformly enlarges the complete planner while keeping its layout edge to edge.</summary>
public sealed class PlannerScaleHost : Grid
{
    private readonly FrameworkElement _surface;
    private readonly bool _floating;
    public double ScaleFactor { get; private set; } = 1;
    public PlannerScaleHost(FrameworkElement surface, bool floating)
    {
        _surface = surface; _floating = floating;
        surface.Width = floating ? 320 : 1384; surface.Height = floating ? 568 : 809;
        Children.Add(new Viewbox { Child = surface, Stretch = Stretch.Uniform });
        SizeChanged += (_, args) => Fit(args.NewSize.Width, args.NewSize.Height);
    }
    private void Fit(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        ScaleFactor = _floating ? 1 : Math.Max(1, Math.Min(width / 1384, height / 809));
        _surface.Width = width / ScaleFactor; _surface.Height = height / ScaleFactor;
    }
}
