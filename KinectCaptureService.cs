using System;
using System.Threading;

namespace KinectWindowsWebCam;

internal enum KinectState
{
    Starting,
    DriverMissing,
    NotConnected,
    Error,
    Running
}

internal sealed class KinectStatus
{
    public KinectStatus(KinectState state, string text)
    {
        State = state;
        Text = text;
    }

    public KinectState State { get; }
    public string Text { get; }
}

internal sealed class KinectCaptureService : IDisposable
{
    private const int RetryDelayMs = 2000;
    private const int IdleTickMs = 250;
    private const uint FrameWaitMs = 100;
    private const int NoSignalPlaceholderMs = 1500;
    private const int NoFrameTimeoutMs = 3000;
    private const int NoTiltReading = int.MinValue;
    private const int SuccessFalse = 1;
    private const string NoPictureMessage = "Kinect: brak obrazu";

    private readonly TiltScheduler _tilt;
    private readonly IKinectFrameSink _sink;
    private readonly NuiFrameTextureAccessor _texture = new();
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _thread;
    private volatile KinectStatus _status = new(KinectState.Starting, "uruchamianie…");
    private int _tiltReading = NoTiltReading;
    private NuiSensor? _sensor;
    private IntPtr _colorStream;

    public KinectCaptureService(TiltScheduler tilt, IKinectFrameSink sink)
    {
        _tilt = tilt;
        _sink = sink;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Kinect capture"
        };
    }

    public KinectStatus Status => _status;

    public void Start()
    {
        _thread.Start();
    }

    public bool TryTakeTiltReading(out int angle)
    {
        angle = Interlocked.Exchange(ref _tiltReading, NoTiltReading);
        return angle != NoTiltReading;
    }

    public bool Stop(TimeSpan timeout)
    {
        _stop.Set();
        return !_thread.IsAlive || _thread.Join(timeout);
    }

    public void Dispose()
    {
        if (Stop(TimeSpan.FromSeconds(3)))
        {
            _stop.Dispose();
        }
    }

    private void Run()
    {
        var comResult = NativeMethods.CoInitializeEx(IntPtr.Zero, NativeMethods.CoInitMultithreaded);
        try
        {
            while (!_stop.WaitOne(0))
            {
                try
                {
                    if (Connect())
                    {
                        Pump();
                    }
                }
                catch (Exception exception)
                {
                    SetStatus(KinectState.Error, "błąd: " + exception.Message);
                }
                finally
                {
                    Disconnect();
                }

                if (WaitShowingStatus(RetryDelayMs))
                {
                    break;
                }
            }

            _sink.OnShutdown();
        }
        finally
        {
            if (comResult >= 0)
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    private bool Connect()
    {
        int count;
        int hr;
        try
        {
            hr = Kinect10.NuiGetSensorCount(out count);
        }
        catch (DllNotFoundException)
        {
            SetStatus(KinectState.DriverMissing, "brak sterownika – zainstaluj Kinect for Windows SDK 1.8");
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            SetStatus(KinectState.DriverMissing, "niezgodny Kinect10.dll – zainstaluj Kinect for Windows SDK 1.8");
            return false;
        }
        catch (BadImageFormatException)
        {
            SetStatus(KinectState.DriverMissing, "uszkodzony Kinect10.dll – przeinstaluj Kinect for Windows SDK 1.8");
            return false;
        }

        if (hr < 0)
        {
            SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
            return false;
        }

        if (count < 1)
        {
            SetStatus(KinectState.NotConnected, "nie podłączony");
            return false;
        }

        hr = Kinect10.NuiCreateSensorByIndex(0, out var instance);
        if (hr < 0 || instance == IntPtr.Zero)
        {
            SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
            return false;
        }

        _sensor = new NuiSensor(instance);

        hr = _sensor.GetStatus();
        if (hr != 0)
        {
            SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
            return false;
        }

        hr = _sensor.Initialize(NuiConstants.InitializeUsesColor);
        if (hr < 0)
        {
            SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
            return false;
        }

        hr = _sensor.OpenColorStream(out _colorStream);
        if (hr < 0)
        {
            SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
            return false;
        }

        if (_sensor.GetElevationAngle(out var angle) >= 0 && _tilt.SetCurrent(angle))
        {
            Interlocked.Exchange(ref _tiltReading, angle);
        }

        SetStatus(KinectState.Running, "działa");
        return true;
    }

    private void Pump()
    {
        var sensor = _sensor!;
        var lastFrameTick = Environment.TickCount;

        while (!_stop.WaitOne(0))
        {
            var hr = sensor.GetNextFrame(_colorStream, FrameWaitMs, out var frame);
            if (hr == 0)
            {
                lastFrameTick = Environment.TickCount;
                try
                {
                    DeliverFrame(frame.FrameTexture);
                }
                finally
                {
                    sensor.ReleaseFrame(_colorStream, ref frame);
                }

                if (_status.State != KinectState.Running)
                {
                    SetStatus(KinectState.Running, "działa");
                }
            }
            else if (hr != NuiErrors.FrameNoData && hr != SuccessFalse)
            {
                SetStatus(NuiErrors.StateFor(hr), NuiErrors.Describe(hr));
                return;
            }
            else
            {
                var silentMs = unchecked(Environment.TickCount - lastFrameTick);
                if (silentMs > NoFrameTimeoutMs)
                {
                    var status = sensor.GetStatus();
                    if (status == 0)
                    {
                        SetStatus(KinectState.Error, "brak obrazu z kamery – ponowne łączenie");
                    }
                    else
                    {
                        SetStatus(NuiErrors.StateFor(status), NuiErrors.Describe(status));
                    }

                    return;
                }

                if (silentMs > NoSignalPlaceholderMs)
                {
                    _sink.OnNoSignal(NoPictureMessage);
                }
            }

            if (_tilt.TryTake(out var angle))
            {
                sensor.SetElevationAngle(angle);
            }
        }
    }

    private void DeliverFrame(IntPtr texture)
    {
        if (texture == IntPtr.Zero || _texture.LockRect(texture, out var lockedRect) < 0)
        {
            return;
        }

        try
        {
            var minimumPitch = NuiConstants.ColorWidth * 4;
            if (lockedRect.Bits != IntPtr.Zero && lockedRect.Pitch >= minimumPitch && lockedRect.Size >= lockedRect.Pitch * NuiConstants.ColorHeight)
            {
                _sink.OnFrame(lockedRect.Bits, lockedRect.Pitch, NuiConstants.ColorWidth, NuiConstants.ColorHeight);
            }
        }
        finally
        {
            _texture.UnlockRect(texture);
        }
    }

    private bool WaitShowingStatus(int durationMs)
    {
        var start = Environment.TickCount;
        while (!_stop.WaitOne(0))
        {
            _sink.OnNoSignal("Kinect: " + _status.Text);
            var remainingMs = durationMs - unchecked(Environment.TickCount - start);
            if (remainingMs <= 0)
            {
                return _stop.WaitOne(0);
            }

            if (_stop.WaitOne(Math.Min(remainingMs, IdleTickMs)))
            {
                return true;
            }
        }

        return true;
    }

    private void Disconnect()
    {
        _colorStream = IntPtr.Zero;
        if (_sensor == null)
        {
            return;
        }

        try
        {
            _sensor.Dispose();
        }
        catch (Exception exception)
        {
            SetStatus(KinectState.Error, "błąd zamykania: " + exception.Message);
        }

        _sensor = null;
    }

    private void SetStatus(KinectState state, string text)
    {
        _status = new KinectStatus(state, text);
    }
}
