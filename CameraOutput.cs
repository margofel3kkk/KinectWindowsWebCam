using System;

namespace KinectWindowsWebCam;

internal interface IKinectFrameSink
{
    void OnFrame(IntPtr bgrxPixels, int pitch, int width, int height);

    void OnNoSignal(string message);

    void OnShutdown();
}

internal sealed class CameraOutput : IKinectFrameSink, IDisposable
{
    private const int PlaceholderIntervalMs = 500;
    private const int ClosedFrameTimeoutMs = 24 * 60 * 60 * 1000;
    private const string ClosedMessage = "Aplikacja jest zamknięta – uruchom KinectWindowsWebCam.exe";

    private readonly FrameConverter _converter = new();
    private readonly VirtualCameraWriter _writer = new();
    private readonly PlaceholderRenderer _placeholder = new();
    private volatile int _quality;
    private volatile bool _widescreen;
    private volatile bool _mirror;
    private volatile string? _lastError;
    private bool _placeholderSent;
    private int _lastPlaceholderTick;

    public CameraOutput(int quality, bool widescreen, bool mirror)
    {
        _quality = quality;
        _widescreen = widescreen;
        _mirror = mirror;
    }

    public int Quality
    {
        get => _quality;
        set => _quality = value;
    }

    public bool Widescreen
    {
        get => _widescreen;
        set => _widescreen = value;
    }

    public bool Mirror
    {
        get => _mirror;
        set => _mirror = value;
    }

    public bool IsConsumerActive => _writer.IsConsumerActive;

    public string? LastError => _lastError;

    public void OnFrame(IntPtr bgrxPixels, int pitch, int width, int height)
    {
        try
        {
            if (!_writer.TryBeginFrame())
            {
                return;
            }

            _converter.Convert(bgrxPixels, pitch, width, height, _quality, _widescreen);
            _writer.Send(_converter.Width, _converter.Height, _converter.Pixels, _mirror, VirtualCameraWriter.DefaultTimeoutMs);
            _placeholderSent = false;
            _lastError = null;
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
        }
    }

    public void OnNoSignal(string message)
    {
        var now = Environment.TickCount;
        if (_placeholderSent && unchecked(now - _lastPlaceholderTick) < PlaceholderIntervalMs)
        {
            return;
        }

        try
        {
            if (!_writer.TryBeginFrame())
            {
                return;
            }

            SendPlaceholder(message, VirtualCameraWriter.DefaultTimeoutMs);
            _placeholderSent = true;
            _lastPlaceholderTick = now;
            _lastError = null;
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
        }
    }

    public void OnShutdown()
    {
        try
        {
            if (_writer.TryOpenNow())
            {
                SendPlaceholder(ClosedMessage, ClosedFrameTimeoutMs);
            }
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
        _placeholder.Dispose();
    }

    private void SendPlaceholder(string message, int timeoutMs)
    {
        _placeholder.Convert(_converter, message, _quality, _widescreen);
        _writer.Send(_converter.Width, _converter.Height, _converter.Pixels, false, timeoutMs);
    }
}
