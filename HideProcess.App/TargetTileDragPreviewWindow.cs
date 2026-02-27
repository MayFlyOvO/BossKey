using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace HideProcess.App;

public sealed class TargetTileDragPreviewWindow : Window
{
    private const double PreviewScale = 0.92d;
    private const int GwlExStyle = -20;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;

    public TargetTileDragPreviewWindow(ImageSource previewSource, double width, double height)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Width = Math.Max(1d, width * PreviewScale);
        Height = Math.Max(1d, height * PreviewScale);

        Content = new Grid
        {
            Background = System.Windows.Media.Brushes.Transparent,
            Width = Width,
            Height = Height,
            ClipToBounds = true,
            Children =
            {
                new System.Windows.Controls.Image
                {
                    Source = previewSource,
                    Stretch = Stretch.Fill,
                    Width = Width,
                    Height = Height,
                    Opacity = 0.96,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                }
            }
        };
    }

    public void UpdatePosition(int cursorX, int cursorY)
    {
        Left = cursorX + 6;
        Top = cursorY + 6;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLongPtr(handle, GwlExStyle);
        var newStyle = exStyle.ToInt64()
                       | WsExTransparent
                       | WsExToolWindow
                       | WsExNoActivate;

        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(newStyle));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
