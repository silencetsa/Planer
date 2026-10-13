using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace Planer_App.Views;

/// <summary>Starts a native drag explicitly because Button handles the pointer gesture itself.</summary>
public sealed class DragTaskButton : HandButton
{
    private const double DragThreshold = 6;
    private uint? _pressedPointer;
    private Point _pressPosition;
    private bool _dragging;
    private DateTime _ignoreClicksUntil;
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
        // Release Button's capture before handing the gesture to WinUI drag/drop.
        ReleasePointerCapture(args.Pointer);
        try { await StartDragAsync(point); }
        catch (Exception ex) { DragFailed?.Invoke(this, ex); }
        finally
        {
            _dragging = false; _ignoreClicksUntil = DateTime.UtcNow.AddMilliseconds(250);
            DragGestureFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearPress(object sender, PointerRoutedEventArgs args)
    {
        if (_pressedPointer == args.Pointer.PointerId) _pressedPointer = null;
    }

}
