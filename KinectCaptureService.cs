using System.Reflection;

namespace KinectWindowsWebCam;

public sealed class KinectCaptureService : IDisposable
{
    private readonly System.Threading.Timer _reconnectTimer;
    private readonly object _sync = new();
    private Assembly? _kinectAssembly;
    private object? _sensorCollection;
    private object? _sensor;
    private EventInfo? _statusChangedEvent;
    private EventInfo? _colorFrameReadyEvent;
    private Delegate? _statusChangedHandler;
    private Delegate? _colorFrameReadyHandler;
    private byte[]? _frameBuffer;

    public KinectCaptureService()
    {
        _reconnectTimer = new System.Threading.Timer(_ => TryConnect(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<KinectFrameEventArgs>? FrameReady;
    public event EventHandler<KinectStatusEventArgs>? SensorStateChanged;
    public event EventHandler<object?>? SensorChanged;

    public void Start()
    {
        if (!LoadKinectAssembly())
        {
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("brak SDK"));
            return;
        }

        SubscribeStatusChanged();
        TryConnect();
        _reconnectTimer.Change(2000, 2000);
    }

    private bool LoadKinectAssembly()
    {
        try
        {
            _kinectAssembly = Assembly.Load("Microsoft.Kinect");
            var sensorType = _kinectAssembly.GetType("Microsoft.Kinect.KinectSensor");
            _sensorCollection = sensorType?.GetProperty("KinectSensors", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return _sensorCollection != null;
        }
        catch
        {
            return false;
        }
    }

    private void SubscribeStatusChanged()
    {
        if (_sensorCollection == null)
        {
            return;
        }

        _statusChangedEvent = _sensorCollection.GetType().GetEvent("StatusChanged");
        if (_statusChangedEvent == null)
        {
            return;
        }

        _statusChangedHandler = Delegate.CreateDelegate(_statusChangedEvent.EventHandlerType!, this, nameof(OnKinectStatusChanged));
        _statusChangedEvent.AddEventHandler(_sensorCollection, _statusChangedHandler);
    }

    private void OnKinectStatusChanged(object? sender, EventArgs e)
    {
        dynamic statusArgs = e;
        lock (_sync)
        {
            if (_sensor != null && ReferenceEquals(_sensor, statusArgs.Sensor) && statusArgs.Status.ToString() != "Connected")
            {
                DisconnectCurrentSensor();
                SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("brak"));
            }
        }

        TryConnect();
    }

    private void TryConnect()
    {
        if (_sensorCollection == null)
        {
            return;
        }

        lock (_sync)
        {
            if (_sensor != null)
            {
                return;
            }

            foreach (var candidate in (System.Collections.IEnumerable)_sensorCollection)
            {
                dynamic sensor = candidate;
                if (sensor.Status.ToString() != "Connected")
                {
                    continue;
                }

                if (AttachSensor(candidate))
                {
                    SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("wykryty"));
                    SensorChanged?.Invoke(this, _sensor);
                    return;
                }
            }

            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("brak"));
            SensorChanged?.Invoke(this, null);
        }
    }

    private bool AttachSensor(object sensor)
    {
        if (_kinectAssembly == null)
        {
            return false;
        }

        try
        {
            dynamic sensorDynamic = sensor;
            var formatType = _kinectAssembly.GetType("Microsoft.Kinect.ColorImageFormat");
            var format = Enum.Parse(formatType!, "RgbResolution640x480Fps30");
            sensorDynamic.ColorStream.Enable(format);

            _colorFrameReadyEvent = sensor.GetType().GetEvent("ColorFrameReady");
            _colorFrameReadyHandler = Delegate.CreateDelegate(_colorFrameReadyEvent!.EventHandlerType!, this, nameof(OnColorFrameReady));
            _colorFrameReadyEvent.AddEventHandler(sensor, _colorFrameReadyHandler);

            sensorDynamic.Start();
            _sensor = sensor;
            return true;
        }
        catch
        {
            DisconnectCurrentSensor();
            SensorStateChanged?.Invoke(this, new KinectStatusEventArgs("błąd"));
            return false;
        }
    }

    private void OnColorFrameReady(object? sender, EventArgs e)
    {
        dynamic args = e;
        dynamic frame = args.OpenColorImageFrame();
        if (frame == null)
        {
            return;
        }

        try
        {
            int length = frame.PixelDataLength;
            _frameBuffer ??= new byte[length];
            if (_frameBuffer.Length != length)
            {
                _frameBuffer = new byte[length];
            }

            frame.CopyPixelDataTo(_frameBuffer);
            var frameCopy = new byte[length];
            Buffer.BlockCopy(_frameBuffer, 0, frameCopy, 0, length);
            int width = frame.Width;
            int height = frame.Height;
            FrameReady?.Invoke(this, new KinectFrameEventArgs(frameCopy, width, height));
        }
        finally
        {
            frame.Dispose();
        }
    }

    private void DisconnectCurrentSensor()
    {
        if (_sensor == null)
        {
            return;
        }

        try
        {
            if (_colorFrameReadyEvent != null && _colorFrameReadyHandler != null)
            {
                _colorFrameReadyEvent.RemoveEventHandler(_sensor, _colorFrameReadyHandler);
            }

            dynamic sensorDynamic = _sensor;
            sensorDynamic.Stop();
        }
        catch
        {
        }

        _sensor = null;
        _colorFrameReadyEvent = null;
        _colorFrameReadyHandler = null;
        SensorChanged?.Invoke(this, null);
    }

    public void Dispose()
    {
        _reconnectTimer.Dispose();
        if (_statusChangedEvent != null && _statusChangedHandler != null && _sensorCollection != null)
        {
            _statusChangedEvent.RemoveEventHandler(_sensorCollection, _statusChangedHandler);
        }

        lock (_sync)
        {
            DisconnectCurrentSensor();
        }
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
