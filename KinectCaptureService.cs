using Microsoft.Kinect;

namespace KinectWindowsWebCam;

public sealed class KinectCaptureService : IDisposable
{
    private KinectSensor? _sensor;
    private byte[]? _frameBuffer;

    public event EventHandler<KinectFrameEventArgs>? FrameReady;
    public event EventHandler<KinectStatusEventArgs>? SensorStateChanged;
    public event EventHandler<KinectSensor?>? SensorChanged;

    public void Start()
    {
        KinectSensor.KinectSensors.StatusChanged += OnKinectStatusChanged;
        TryConnect();
    }

    private void OnKinectStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        if (_sensor == e.Sensor && e.Status != KinectStatus.Connected)
        {
            DisconnectCurrentSensor();
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("brak"));
            TryConnect();
            return;
        }

        if (_sensor == null)
        {
            TryConnect();
        }
    }

    private void TryConnect()
    {
        var sensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
        if (sensor == null)
        {
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("brak"));
            SensorChanged?.Invoke(this, null);
            return;
        }

        try
        {
            _sensor = sensor;
            _sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            _sensor.ColorFrameReady += OnColorFrameReady;
            _sensor.Start();
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("wykryty"));
            SensorChanged?.Invoke(this, _sensor);
        }
        catch
        {
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("błąd"));
            DisconnectCurrentSensor();
        }
    }

    private void OnColorFrameReady(object? sender, ColorImageFrameReadyEventArgs e)
    {
        using var frame = e.OpenColorImageFrame();
        if (frame == null)
        {
            return;
        }

        var length = frame.PixelDataLength;
        _frameBuffer ??= new byte[length];
        if (_frameBuffer.Length != length)
        {
            _frameBuffer = new byte[length];
        }

        frame.CopyPixelDataTo(_frameBuffer);
        var frameCopy = new byte[length];
        Buffer.BlockCopy(_frameBuffer, 0, frameCopy, 0, length);
        FrameReady?.Invoke(this, new KinectFrameEventArgs(frameCopy, frame.Width, frame.Height));
    }

    private void DisconnectCurrentSensor()
    {
        if (_sensor == null)
        {
            return;
        }

        try
        {
            _sensor.ColorFrameReady -= OnColorFrameReady;
            _sensor.Stop();
        }
        catch
        {
        }

        _sensor = null;
        SensorChanged?.Invoke(this, null);
    }

    public void Dispose()
    {
        KinectSensor.KinectSensors.StatusChanged -= OnKinectStatusChanged;
        DisconnectCurrentSensor();
    }
}

public sealed class KinectFrameEventArgs : EventArgs
{
    public KinectFrameEventArgs(byte[] bgraFrame, int width, int height)
    {
        BgraFrame = bgraFrame;
        Width = width;
        Height = height;
    }

    public byte[] BgraFrame { get; }
    public int Width { get; }
    public int Height { get; }
}

public sealed class KinectStatusEventArgs : EventArgs
{
    public KinectStatusEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
