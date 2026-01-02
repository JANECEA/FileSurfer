using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;

namespace FileSurfer.Linux.Models.FileInformation;

internal static class SvgHelper
{
    // DEP resvg
    internal static Bitmap? RenderSvg(string svgPath, int size)
    {
        string sizeStr = size.ToString(CultureInfo.InvariantCulture);
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "resvg",
            ArgumentList = { svgPath, "-w", sizeStr, "-h", sizeStr, "--quiet", "-c" },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            process.Start();
            using MemoryStream pngStream = new();
            process.StandardOutput.BaseStream.CopyTo(pngStream);
            process.WaitForExit();

            if (process.ExitCode != 0)
                return null;

            pngStream.Position = 0;
            return new Bitmap(pngStream);
        }
        catch
        {
            return null;
        }
    }
}
