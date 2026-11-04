using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace KinectWindowsWebCam;

internal sealed class PlaceholderRenderer : IDisposable
{
    private const int HorizontalMargin = 40;
    private static readonly Color BackgroundColor = Color.FromArgb(24, 24, 24);
    private static readonly Color TitleColor = Color.White;
    private static readonly Color MessageColor = Color.FromArgb(200, 200, 200);

    private readonly Bitmap _bitmap = new(NuiConstants.ColorWidth, NuiConstants.ColorHeight, PixelFormat.Format32bppArgb);
    private readonly Font _titleFont = new("Segoe UI", 30f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _messageFont = new("Segoe UI", 21f, FontStyle.Regular, GraphicsUnit.Pixel);
    private string? _renderedMessage;

    public void Convert(FrameConverter converter, string message, int quality, bool widescreen)
    {
        Render(message);
        var bounds = new Rectangle(0, 0, _bitmap.Width, _bitmap.Height);
        var data = _bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            converter.Convert(data.Scan0, data.Stride, data.Width, data.Height, quality, widescreen);
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }
    }

    public void Dispose()
    {
        _bitmap.Dispose();
        _titleFont.Dispose();
        _messageFont.Dispose();
    }

    private void Render(string message)
    {
        if (message == _renderedMessage)
        {
            return;
        }

        using var graphics = Graphics.FromImage(_bitmap);
        using var titleBrush = new SolidBrush(TitleColor);
        using var messageBrush = new SolidBrush(MessageColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Far,
            Trimming = StringTrimming.EllipsisWord
        };

        graphics.Clear(BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var width = _bitmap.Width - 2 * HorizontalMargin;
        var middle = _bitmap.Height / 2;
        graphics.DrawString(VirtualCameraInstaller.DeviceName, _titleFont, titleBrush, new RectangleF(HorizontalMargin, middle - 110, width, 100), format);
        format.LineAlignment = StringAlignment.Near;
        graphics.DrawString(message, _messageFont, messageBrush, new RectangleF(HorizontalMargin, middle + 10, width, 120), format);

        _renderedMessage = message;
    }
}
