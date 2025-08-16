﻿using Avalonia.Media.Imaging;

namespace FileSurfer.Models.FileInformation;

public interface IIconProvider
{
    /// <summary>
    /// Retrieves the associated icon based on the supplied file path. 
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>The associated icon if file exists, otherwise returns <see langword="null" />.</returns>
    public Bitmap? GetFileIcon(string filePath);
    
    /// <summary>
    /// Retrieves the icon associated with directories.
    /// </summary>
    public Bitmap GetDirectoryIcon();

    /// <summary>
    /// Retrieves the icon associated with drives.
    /// </summary>
    public Bitmap GetDriveIcon();
}