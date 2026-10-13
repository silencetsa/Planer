using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Windows.Foundation;

namespace Planer_App.Views;

/// <summary>Measures real wrapped card heights before arranging the three scrollable sections.</summary>
public sealed class DaySectionsPanel : Panel
{
    private readonly bool[] _occupied;
    private readonly double[] _naturalHeights = new double[3];
    private const double Gap = 2;
    public DaySectionsPanel(bool[] occupied) => _occupied = occupied;
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            // An unbounded vertical measure gives the collapsed task text (max two lines)
            // and payment cards their true natural height at the current card width.
            Children[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            _naturalHeights[i] = Children[i].DesiredSize.Height;
            width = Math.Max(width, Children[i].DesiredSize.Width);
        }
        if (!double.IsInfinity(availableSize.Height))
        {
            var heights = DaySectionLayout.Allocate(Math.Max(0, availableSize.Height - Gap * 2), _naturalHeights, _occupied);
            for (int i = 0; i < Children.Count; i++) Children[i].Measure(new Size(availableSize.Width, heights[i]));
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? _naturalHeights.Sum() + Gap * 2 : availableSize.Height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var heights = DaySectionLayout.Allocate(Math.Max(0, finalSize.Height - Gap * 2), _naturalHeights, _occupied);
        double top = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            // The ScrollViewer must also know its finite viewport when the final slot changes.
            Children[i].Measure(new Size(finalSize.Width, heights[i]));
            Children[i].Arrange(new Rect(0, top, finalSize.Width, heights[i]));
            top += heights[i] + Gap;
        }
        return finalSize;
    }
}
