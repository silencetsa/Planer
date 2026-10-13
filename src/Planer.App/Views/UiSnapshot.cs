using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Planer_App.Views;

/// <summary>Opt-in local rendering for QA; requires an explicit alternate data directory.</summary>
internal static class UiSnapshot
{
    public static async Task SaveAsync(FrameworkElement view, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Allow the first native layout/render frame to finish in the standalone QA process.
        await Task.Delay(350);
        view.UpdateLayout();
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
