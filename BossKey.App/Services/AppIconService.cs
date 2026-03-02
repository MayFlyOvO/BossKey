using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BossKey.App.Models;
using Drawing = System.Drawing;

namespace BossKey.App.Services;

public sealed class AppIconService
{
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource GetIcon(string? processPath)
    {
        var cacheKey = string.IsNullOrWhiteSpace(processPath) ? "__default__" : processPath;
        return _cache.GetOrAdd(cacheKey, _ => LoadIcon(processPath));
    }

    private static ImageSource LoadIcon(string? processPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                using var icon = Drawing.Icon.ExtractAssociatedIcon(processPath);
                if (icon is not null)
                {
                    return CreateFrozenImage(icon);
                }
            }
        }
        catch
        {
        }

        using var fallback = (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        return CreateFrozenImage(fallback);
    }

    private static ImageSource CreateFrozenImage(Drawing.Icon icon)
    {
        var imageSource = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(48, 48));
        imageSource.Freeze();
        return imageSource;
    }
}
