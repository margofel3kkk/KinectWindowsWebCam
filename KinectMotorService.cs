using Microsoft.Kinect;

namespace KinectWindowsWebCam;

public sealed class KinectMotorService : IDisposable
{
    private readonly object _sync = new();
    private readonly System.Threading.Timer _timer;
    private KinectSensor? _sensor;
    private int _targetTilt;

    public KinectMotorService()
    {
        _timer = new System.Threading.Timer(_ => ApplyTilt(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void SetSensor(KinectSensor? sensor)
    {
        lock (_sync)
        {
            _sensor = sensor;
        }
    }

    public void SetTargetTilt(int tilt)
    {
        var clamped = Math.Clamp(tilt, -27, 27);
        lock (_sync)
        {
            _targetTilt = clamped;
            _timer.Change(150, Timeout.Infinite);
        }
    }

    private void ApplyTilt()
    {
        KinectSensor? sensor;
        int target;

        lock (_sync)
        {
            sensor = _sensor;
            target = _targetTilt;
        }

        if (sensor == null || sensor.Status != KinectStatus.Connected)
        {
            return;
        }

        try
        {
            sensor.ElevationAngle = target;
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
