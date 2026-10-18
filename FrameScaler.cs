namespace KinectWindowsWebCam;

internal static class FrameScaler
{
    public static void ScaleBgra(byte[] source, int sourceWidth, int sourceHeight, byte[] destination, int destinationWidth, int destinationHeight)
    {
        var xRatio = sourceWidth / (float)destinationWidth;
        var yRatio = sourceHeight / (float)destinationHeight;

        for (var y = 0; y < destinationHeight; y++)
        {
            var sourceY = Math.Min((int)(y * yRatio), sourceHeight - 1);
            var sourceRow = sourceY * sourceWidth * 4;
            var destinationRow = y * destinationWidth * 4;

            for (var x = 0; x < destinationWidth; x++)
            {
                var sourceX = Math.Min((int)(x * xRatio), sourceWidth - 1);
                var sourceOffset = sourceRow + sourceX * 4;
                var destinationOffset = destinationRow + x * 4;
                destination[destinationOffset] = source[sourceOffset];
                destination[destinationOffset + 1] = source[sourceOffset + 1];
                destination[destinationOffset + 2] = source[sourceOffset + 2];
                destination[destinationOffset + 3] = source[sourceOffset + 3];
            }
        }
    }
}
