using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Planer.Core;

namespace Planer_App.Views;

/// <summary>Starts a native drag explicitly because Button handles the pointer gesture itself.</summary>
public sealed class DragTaskButton : HandButton
{
    private const double DragThreshold = 6;
    private uint? _pressedPointer;
    private Point _pressPosition;
    private Point _localGrabPosition;
    private bool _dragging;
    private DateTime _ignoreClicksUntil;
    private SoftwareBitmap? _dragBitmap;
    private double _sourceOpacity;
    internal static DragTaskButton? Active { get; private set; }
    internal static event EventHandler? ActiveDragEnded;
    internal SoftwareBitmap? DragBitmap => _dragBitmap;
    internal Point GrabOffset { get; private set; }
    internal Size PreviewSize { get; private set; }
    internal PlannerTask? SourceTask { get; init; }
    public bool IsClickSuppressed => _dragging || DateTime.UtcNow <= _ignoreClicksUntil;
    public event EventHandler? DragGestureFinished;
    public event EventHandler<Exception>? DragFailed;

    public DragTaskButton()
    {
        BorderThickness = new(0); MinHeight = 0; MinWidth = 0;
        // Button captures and handles pointer input. Observe handled events as well.
        AddHandler(PointerPressedEvent, new PointerEventHandler(Pressed), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(Moved), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(ClearPress), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(ClearPress), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(ClearPress), true);
        DragStarting += CreateDragVisual;
    }

    private void Pressed(object sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(XamlRoot.Content as UIElement);
        if (_dragging || !point.Properties.IsLeftButtonPressed) return;
        _pressedPointer = point.PointerId; _pressPosition = point.Position;
        _localGrabPosition = args.GetCurrentPoint(this).Position;
        DragDiagnostics.Write("press", new { Id = SourceTask?.Id, LocalX = _localGrabPosition.X, LocalY = _localGrabPosition.Y });
    }

    private async void Moved(object sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(XamlRoot.Content as UIElement);
        if (_dragging || _pressedPointer != point.PointerId) return;
        if (!point.Properties.IsLeftButtonPressed) { _pressedPointer = null; return; }
        double dx = point.Position.X - _pressPosition.X, dy = point.Position.Y - _pressPosition.Y;
        if (dx * dx + dy * dy < DragThreshold * DragThreshold) return;
        _pressedPointer = null; _dragging = true; args.Handled = true;
        _sourceOpacity = Opacity;
        Active = this;
        try
        {
            // Release Button's capture before handing the gesture to WinUI drag/drop.
            ReleasePointerCapture(args.Pointer);
            await StartDragAsync(point);
        }
        catch (Exception ex) { DragFailed?.Invoke(this, ex); }
        finally
        {
            Opacity = _sourceOpacity; _dragBitmap?.Dispose(); _dragBitmap = null;
            _dragging = false; _ignoreClicksUntil = DateTime.UtcNow.AddMilliseconds(250);
            Active = null; ActiveDragEnded?.Invoke(this, EventArgs.Empty);
            DragGestureFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearPress(object sender, PointerRoutedEventArgs args)
    {
        if (_pressedPointer == args.Pointer.PointerId) _pressedPointer = null;
    }

    internal async Task<SoftwareBitmap> RenderDragBitmapAsync()
    {
        var renderer = new RenderTargetBitmap();
        var bounds = TransformToVisual(XamlRoot.Content).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
        double pixelsPerDip = XamlRoot.RasterizationScale;
        await renderer.RenderAsync(this, Math.Max(1, (int)Math.Ceiling(bounds.Width * pixelsPerDip)), Math.Max(1, (int)Math.Ceiling(bounds.Height * pixelsPerDip)));
        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(await renderer.GetPixelsAsync(), BitmapPixelFormat.Bgra8,
            renderer.PixelWidth, renderer.PixelHeight, BitmapAlphaMode.Premultiplied);
        bitmap.DpiX = bitmap.DpiY = 96 * pixelsPerDip;
        return bitmap;
    }

    private async void CreateDragVisual(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var bounds = TransformToVisual(XamlRoot.Content).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            var grip = DragSnapTracker.GrabOffset(new(_localGrabPosition.X, _localGrabPosition.Y), ActualWidth, ActualHeight, bounds.Width, bounds.Height);
            GrabOffset = new(grip.X, grip.Y); PreviewSize = new(bounds.Width, bounds.Height);
            _dragBitmap = await RenderDragBitmapAsync();
            // Preserve the point pressed before crossing the drag threshold, including view scaling.
            args.DragUI.SetContentFromSoftwareBitmap(_dragBitmap, GrabOffset);
            DragDiagnostics.Write("preview", new { GrabOffset.X, GrabOffset.Y, PreviewSize.Width, PreviewSize.Height });
            // Keep the original slot visible until a drop is committed or cancelled.
            Opacity = .35;
        }
        catch (Exception ex) { args.Cancel = true; DragFailed?.Invoke(this, ex); }
        finally { deferral.Complete(); }
    }
}
