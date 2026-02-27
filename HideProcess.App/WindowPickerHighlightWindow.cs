using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace HideProcess.App;

public sealed class WindowPickerHighlightWindow : Window
{
    private const int HighlightInset = 4;
    private readonly TextBlock _processNameTextBlock;

    public WindowPickerHighlightWindow()
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

        _processNameTextBlock = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        };

        Content = new Grid
        {
            Children =
            {
                new Border
                {
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)),
                    BorderThickness = new Thickness(4),
                    CornerRadius = new CornerRadius(4),
                    Background = System.Windows.Media.Brushes.Transparent,
                    SnapsToDevicePixels = true
                },
                new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(10, 10, 0, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Child = _processNameTextBlock
                }
            }
        };
    }

    public void ShowHighlight(int left, int top, int width, int height, string processName)
    {
        if (width <= 0 || height <= 0)
        {
            HideHighlight();
            return;
        }

        var adjustedBounds = AdjustToScreenBounds(left, top, width, height);
        if (adjustedBounds.Width <= 0 || adjustedBounds.Height <= 0)
        {
            HideHighlight();
            return;
        }

        _processNameTextBlock.Text = processName;
        Left = adjustedBounds.Left;
        Top = adjustedBounds.Top;
        Width = adjustedBounds.Width;
        Height = adjustedBounds.Height;

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideHighlight()
    {
        if (IsVisible)
        {
            Hide();
        }
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

    private static Drawing.Rectangle AdjustToScreenBounds(int left, int top, int width, int height)
    {
        var targetBounds = new Drawing.Rectangle(left, top, width, height);
        var screenBounds = Forms.Screen.FromRectangle(targetBounds).Bounds;

        var adjustedLeft = left;
        var adjustedTop = top;
        var adjustedRight = left + width;
        var adjustedBottom = top + height;

        if (adjustedLeft < screenBounds.Left + HighlightInset)
        {
            adjustedLeft = screenBounds.Left + HighlightInset;
        }

        if (adjustedTop < screenBounds.Top + HighlightInset)
        {
            adjustedTop = screenBounds.Top + HighlightInset;
        }

        if (adjustedRight > screenBounds.Right - HighlightInset)
        {
            adjustedRight = screenBounds.Right - HighlightInset;
        }

        if (adjustedBottom > screenBounds.Bottom - HighlightInset)
        {
            adjustedBottom = screenBounds.Bottom - HighlightInset;
        }

        if (adjustedRight <= adjustedLeft || adjustedBottom <= adjustedTop)
        {
            return Drawing.Rectangle.Empty;
        }

        return Drawing.Rectangle.FromLTRB(adjustedLeft, adjustedTop, adjustedRight, adjustedBottom);
    }

    private const int GwlExStyle = -20;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
