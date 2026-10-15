using Microsoft.UI.Xaml;
using Planer.Core;
using Planer_App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private sealed record DropSlot(FrameworkElement Target, DateOnly? Date, DayPart? Part, Func<FrameworkElement> Anchor, FrameworkElement WidthTarget);
    private readonly List<DropSlot> _dropSlots = [];
    private readonly DragSnapTracker _snap = new();

    private void EnableDrop(FrameworkElement target, DateOnly? date, DayPart? part = null, Func<FrameworkElement>? anchor = null, FrameworkElement? widthTarget = null)
    {
        target.AllowDrop = true; _dropSlots.Add(new(target, date, part, anchor ?? (() => target), widthTarget ?? target));
        target.DragEnter += PlannerDragOver;
        target.DragOver += PlannerDragOver;
        target.Drop += PlannerDrop;
    }

    private Rect SlotBounds(DropSlot slot) => slot.Target.TransformToVisual(XamlRoot.Content)
        .TransformBounds(new Rect(0, 0, slot.Target.ActualWidth, slot.Target.ActualHeight));
    private Point SlotOrigin(DropSlot slot, DragTaskButton source)
    {
        var anchor = slot.Anchor(); var point = anchor.TransformToVisual(XamlRoot.Content).TransformPoint(new Point());
        var bounds = SlotBounds(slot);
        // A scrolled list's insertion point may lie outside the viewport. Snap to its visible end.
        return new(Math.Clamp(point.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - 1)),
            Math.Clamp(point.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - source.PreviewSize.Height)));
    }
    private (DropSlot? Slot, Point? Anchor) ResolveDrop(Point cursor, DragTaskButton source)
    {
        var candidates = _dropSlots.Select((slot, i) => (Slot: slot, Key: i, Anchor: SlotOrigin(slot, source))).ToList();
        double pixel = 1 / XamlRoot.RasterizationScale;
        var held = _snap.Update(new(cursor.X - source.GrabOffset.X, cursor.Y - source.GrabOffset.Y),
            candidates.Select(c => new SnapTarget(c.Key, new(c.Anchor.X, c.Anchor.Y),
                c.Slot.WidthTarget.TransformToVisual(XamlRoot.Content)
                    .TransformBounds(new Rect(0, 0, c.Slot.WidthTarget.ActualWidth, c.Slot.WidthTarget.ActualHeight)).Width / 2)).ToArray(),
            20 * pixel, 60 * pixel);
        if (held is { } key) { var snapped = candidates[key]; return (snapped.Slot, snapped.Anchor); }
        return (_dropSlots.FirstOrDefault(slot => SlotBounds(slot).Contains(cursor)), null);
    }
    private void PlannerDragOver(object sender, DragEventArgs args)
    {
        HideDragLabels(args);
        if (DragTaskButton.Active is not { DragBitmap: { } bitmap } source || !args.DataView.Contains(StandardDataFormats.Text)) return;
        var cursor = args.GetPosition(XamlRoot.Content); var result = ResolveDrop(cursor, source);
        if (result.Anchor is { } anchor)
        {
            args.DragUIOverride.SetContentFromSoftwareBitmap(bitmap, new Point(cursor.X - anchor.X, cursor.Y - anchor.Y));
            args.DragUIOverride.IsContentVisible = true;
        }
        else { args.DragUIOverride.Clear(); HideDragLabels(args); }
        args.AcceptedOperation = result.Slot is null ? DataPackageOperation.None : DataPackageOperation.Move; args.Handled = true;
        DragDiagnostics.Write("over", new { CursorX = cursor.X, CursorY = cursor.Y, Snapped = result.Anchor is not null,
            AnchorX = result.Anchor?.X, AnchorY = result.Anchor?.Y, Date = result.Slot?.Date, Part = result.Slot?.Part });
    }
    private async void PlannerDrop(object sender, DragEventArgs args)
    {
        if (DragTaskButton.Active is not { } source || !args.DataView.Contains(StandardDataFormats.Text)) return;
        var slot = ResolveDrop(args.GetPosition(XamlRoot.Content), source).Slot;
        if (slot is null) return;
        // Stop bubbling before awaiting, so the leaf and root cannot submit the same move twice.
        args.Handled = true;
        var deferral = args.GetDeferral();
        try
        {
            if (Guid.TryParse(await args.DataView.GetTextAsync(), out var id))
            {
                args.AcceptedOperation = DataPackageOperation.Move;
                _suppressClicksUntil = DateTime.UtcNow.AddMilliseconds(250);
                _vm.MoveTaskCommand.Execute(new TaskMove(id, slot.Date, slot.Part));
                DragDiagnostics.Write("drop", new { Id = id, slot.Date, slot.Part });
            }
            args.Handled = true;
        }
        catch (Exception ex) { await ErrorDialog(ex.Message); }
        finally { ResetDrag(this, EventArgs.Empty); deferral.Complete(); }
    }
    private void PlannerDragLeave(object sender, DragEventArgs args)
    {
        if (!new Rect(0, 0, _root.ActualWidth, _root.ActualHeight).Contains(args.GetPosition(_root)))
        { ResetDrag(this, EventArgs.Empty); args.DragUIOverride.Clear(); HideDragLabels(args); }
    }
    private void ResetDrag(object? sender, EventArgs args) => _snap.Reset();
    private static void HideDragLabels(DragEventArgs args)
    {
        args.DragUIOverride.IsCaptionVisible = false; args.DragUIOverride.IsGlyphVisible = false;
    }
}
