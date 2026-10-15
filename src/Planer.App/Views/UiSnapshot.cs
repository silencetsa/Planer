using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Planer_App.Views;

/// <summary>Opt-in local rendering for QA; requires an explicit alternate data directory.</summary>
internal static class UiSnapshot
{
    public static async Task SaveTaskPreviewAsync(FrameworkElement view, string path)
    {
        await PrepareAsync(view);
        var card = FindTaskCard(view) ?? throw new InvalidOperationException("Нет карточки задачи для проверки образа переноса");
        using var bitmap = await card.RenderDragBitmapAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
    }
    private static DragTaskButton? FindTaskCard(DependencyObject node)
    {
        if (node is DragTaskButton card) return card;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindTaskCard(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }
    private static IEnumerable<Ui.OriginalImageLoad> OriginalImages(DependencyObject node)
    {
        if (node is Microsoft.UI.Xaml.Controls.Image { Tag: Ui.OriginalImageLoad image }) yield return image;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in OriginalImages(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static async Task PrepareAsync(FrameworkElement view)
    {
        await Task.Delay(150); view.UpdateLayout();
        var images = OriginalImages(view).ToArray();
        try { await Task.WhenAll(images.Select(i => i.Ready)).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Не завершилась загрузка иконок: " + string.Join(", ", images.Where(i => !i.Ready.IsCompleted).Select(i => i.Name).Distinct()));
        }
        // ImageOpened confirms decoding; allow the native compositor to present the pixels.
        await Task.Delay(150); view.UpdateLayout();
    }
    public static async Task SaveAsync(FrameworkElement view, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await PrepareAsync(view);
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(view);
        var buffer = await bitmap.GetPixelsAsync(); using var reader = DataReader.FromBuffer(buffer);
        byte[] pixels = new byte[buffer.Length]; reader.ReadBytes(pixels);
        File.WriteAllBytes(path, []);
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }
}
