namespace KinectWindowsWebCam;

internal static class QualityPreset
{
    private static readonly Dictionary<int, byte[]> Buffers = new();

    public static (int Width, int Height) GetRenderSize(int index)
    {
        return index switch
        {
            0 => (320, 240),
            1 => (480, 360),
            _ => (640, 480)
        };
    }

    public static byte[] GetReusableBuffer(int width, int height)
    {
        var key = width * 10000 + height;
        if (!Buffers.TryGetValue(key, out var buffer))
        {
            buffer = new byte[width * height * 4];
            Buffers[key] = buffer;
        }

        return buffer;
    }
}
