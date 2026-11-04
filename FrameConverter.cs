using System;
using System.Drawing;

namespace KinectWindowsWebCam;

internal sealed unsafe class FrameConverter
{
    private static readonly int[] QualityWidths = { 320, 480, 640 };

    private uint[] _pixels = new uint[0];
    private int[] _columnMap = new int[0];
    private int _mapSourceWidth;
    private int _mapOutputWidth;

    public static int QualityLevels => QualityWidths.Length;

    public static int DefaultQuality => QualityWidths.Length - 1;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public uint[] Pixels => _pixels;

    public static Size GetOutputSize(int quality, bool widescreen)
    {
        GetLayout(NuiConstants.ColorWidth, NuiConstants.ColorHeight, quality, widescreen, out var width, out var height, out _, out _);
        return new Size(width, height);
    }

    public void Convert(IntPtr bgrxPixels, int pitch, int sourceWidth, int sourceHeight, int quality, bool widescreen)
    {
        GetLayout(sourceWidth, sourceHeight, quality, widescreen, out var outputWidth, out var outputHeight, out var cropTop, out var cropHeight);

        var pixelCount = outputWidth * outputHeight;
        if (_pixels.Length < pixelCount)
        {
            _pixels = new uint[pixelCount];
        }

        if (_mapSourceWidth != sourceWidth || _mapOutputWidth != outputWidth)
        {
            _columnMap = new int[outputWidth];
            for (var x = 0; x < outputWidth; x++)
            {
                _columnMap[x] = (int)((long)x * sourceWidth / outputWidth);
            }

            _mapSourceWidth = sourceWidth;
            _mapOutputWidth = outputWidth;
        }

        var source = (byte*)bgrxPixels;
        fixed (uint* destination = _pixels)
        fixed (int* columnMap = _columnMap)
        {
            for (var y = 0; y < outputHeight; y++)
            {
                var sourceY = cropTop + (int)((long)y * cropHeight / outputHeight);
                var sourceRow = (uint*)(source + (long)sourceY * pitch);
                var destinationRow = destination + (long)(outputHeight - 1 - y) * outputWidth;
                for (var x = 0; x < outputWidth; x++)
                {
                    var pixel = sourceRow[columnMap[x]];
                    destinationRow[x] = 0xFF000000u | ((pixel & 0x000000FFu) << 16) | (pixel & 0x0000FF00u) | ((pixel >> 16) & 0x000000FFu);
                }
            }
        }

        Width = outputWidth;
        Height = outputHeight;
    }

    private static void GetLayout(int sourceWidth, int sourceHeight, int quality, bool widescreen, out int outputWidth, out int outputHeight, out int cropTop, out int cropHeight)
    {
        var level = Math.Max(0, Math.Min(QualityWidths.Length - 1, quality));
        outputWidth = Math.Min(QualityWidths[level], sourceWidth);
        cropHeight = widescreen ? Math.Min(sourceHeight, sourceWidth * 9 / 16) : sourceHeight;
        cropTop = (sourceHeight - cropHeight) / 2;
        outputHeight = Math.Max(1, (int)((long)outputWidth * cropHeight / sourceWidth));
    }
}
