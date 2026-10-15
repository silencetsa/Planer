using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace Planer_App.Views;

/// <summary>Starts a native drag explicitly because Button handles the pointer gesture itself.</summary>
public sealed class DragTaskButton : HandButton
{
    private const double DragThreshold = 6;
    private uint? _pressedPointer;
    private Point _pressPosition;
    private bool _dragging;
    private DateTime _ignoreClicksUntil;
    private SoftwareBitmap? _dragBitmap;
    private double _sourceOpacity;
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
            var anchor = args.GetPosition(this);
            var bounds = TransformToVisual(XamlRoot.Content).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            anchor = new Point(anchor.X * bounds.Width / ActualWidth, anchor.Y * bounds.Height / ActualHeight);
            _dragBitmap = await RenderDragBitmapAsync();
            args.DragUI.SetContentFromSoftwareBitmap(_dragBitmap, anchor);
            // Keep the original slot visible until a drop is committed or cancelled.
            Opacity = .35;
        }
        catch (Exception ex) { args.Cancel = true; DragFailed?.Invoke(this, ex); }
        finally { deferral.Complete(); }
    }
}
