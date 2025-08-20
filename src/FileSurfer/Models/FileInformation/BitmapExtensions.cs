using System.Drawing.Imaging;
using System.IO;
using Avalonia.Media.Imaging;

namespace FileSurfer.Models.FileInformation;

public static class BitmapExtensions
{
    public static Bitmap ConvertToAvaloniaBitmap(this System.Drawing.Bitmap bitmap)
    {
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
