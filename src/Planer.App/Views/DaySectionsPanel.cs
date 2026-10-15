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
    private int? _insertionSection;
    public DaySectionsPanel(bool[] occupied) => _occupied = occupied;
    public void SetInsertionSection(int section, bool visible)
    {
        if (visible) _insertionSection = section;
        else if (_insertionSection == section) _insertionSection = null;
        InvalidateMeasure();
    }
    private double[] Allocate(double availableHeight)
    {
        var heights = DaySectionLayout.Allocate(availableHeight, _naturalHeights, _occupied);
        if (_insertionSection is not { } inserting) return heights;
        double need = Math.Max(0, _naturalHeights[inserting] - heights[inserting]);
        // A transient add button can use genuinely unused space even in occupied sections.
        // Task/payment allocation without the button still follows the empty-section rule.
        var spare = heights.Select((height, i) => i == inserting ? 0 : Math.Max(0, height - _naturalHeights[i])).ToArray();
        double available = spare.Sum(), transfer = Math.Min(need, available);
        if (transfer <= 0) return heights;
        for (int i = 0; i < heights.Length; i++) heights[i] -= transfer * spare[i] / available;
        heights[inserting] += transfer;
        return heights;
    }
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
            var heights = Allocate(Math.Max(0, availableSize.Height - Gap * 2));
            for (int i = 0; i < Children.Count; i++) Children[i].Measure(new Size(availableSize.Width, heights[i]));
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? _naturalHeights.Sum() + Gap * 2 : availableSize.Height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var heights = Allocate(Math.Max(0, finalSize.Height - Gap * 2));
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
