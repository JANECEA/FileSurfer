namespace FileSurfer.Interfaces;

public interface IIconProvider
{
    public Avalonia.Media.Imaging.Bitmap GetFileIcon(string pathToFile);
    
    public Avalonia.Media.Imaging.Bitmap GetDirectoryIcon();

    public Avalonia.Media.Imaging.Bitmap GetDriveIcon();
}