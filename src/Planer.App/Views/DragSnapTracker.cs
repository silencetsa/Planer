namespace Planer_App.Views;

internal readonly record struct SnapPoint(double X, double Y)
{
    public double DistanceSquared(SnapPoint other) => (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
}
internal readonly record struct SnapTarget(int Key, SnapPoint Position);

/// <summary>Keep the chosen slot until the freely dragged card moves beyond the snap radius.</summary>
internal sealed class DragSnapTracker
{
    private int? _held;
    public int? Update(SnapPoint freeOrigin, IReadOnlyList<SnapTarget> targets, double radius)
    {
        double limit = radius * radius;
        foreach (var target in targets)
            if (target.Key == _held && target.Position.DistanceSquared(freeOrigin) <= limit) return _held;
        _held = null;
        double nearest = double.PositiveInfinity;
        foreach (var target in targets)
        {
            double distance = target.Position.DistanceSquared(freeOrigin);
            if (distance <= limit && distance < nearest) { nearest = distance; _held = target.Key; }
        }
        return _held;
    }
    public void Reset() => _held = null;
    public static SnapPoint GrabOffset(SnapPoint localPoint, double localWidth, double localHeight, double renderedWidth, double renderedHeight) =>
        new(localPoint.X * renderedWidth / localWidth, localPoint.Y * renderedHeight / localHeight);
}
