using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Windows.Foundation;

namespace Planer_App.Views;

/// <summary>Measures real wrapped card heights before arranging the three scrollable sections.</summary>
public sealed class DaySectionsPanel : Panel
{
    private readonly bool[] _occupied;
    private readonly double[] _naturalHeights = new double[3];
    private readonly List<(FrameworkElement Content, double HorizontalInset, double VerticalInset)> _contents = [];
    private const double Gap = 2;
    private int? _insertionSection;
    public DaySectionsPanel(bool[] occupied) => _occupied = occupied;
    internal void AddSection(FrameworkElement section, FrameworkElement content, double horizontalInset, double verticalInset)
    {
        _contents.Add((content, horizontalInset, verticalInset)); Children.Add(section);
    }
    public void SetInsertionSection(int section, bool visible)
    {
        int? next = visible ? section : _insertionSection == section ? null : _insertionSection;
        if (next == _insertionSection) return;
        _insertionSection = next;
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
            // Only measure the scroll content with an infinite height. The section and its
            // ScrollViewer always receive their finite viewport, avoiding alternating constraints.
            var info = _contents[i];
            info.Content.Measure(new Size(Math.Max(0, availableSize.Width - info.HorizontalInset), double.PositiveInfinity));
            _naturalHeights[i] = Math.Max(28, Math.Max(19, info.Content.DesiredSize.Height) + info.VerticalInset);
            width = Math.Max(width, info.Content.DesiredSize.Width + info.HorizontalInset);
        }
        double height = double.IsFinite(availableSize.Height) ? availableSize.Height : _naturalHeights.Sum() + Gap * 2;
        var heights = Allocate(Math.Max(0, height - Gap * 2));
        for (int i = 0; i < Children.Count; i++) Children[i].Measure(new Size(availableSize.Width, heights[i]));
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width,
            height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var heights = Allocate(Math.Max(0, finalSize.Height - Gap * 2));
        double top = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(0, top, finalSize.Width, heights[i]));
            top += heights[i] + Gap;
        }
        return finalSize;
    }
}
