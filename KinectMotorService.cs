namespace KinectWindowsWebCam;

public sealed class KinectMotorService : IDisposable
{
    private readonly object _sync = new();
    private readonly System.Threading.Timer _timer;
    private object? _sensor;
    private int _targetTilt;

    public KinectMotorService()
    {
        _timer = new System.Threading.Timer(_ => ApplyTilt(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void SetSensor(object? sensor)
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
        object? sensor;
        int target;

        lock (_sync)
        {
            sensor = _sensor;
            target = _targetTilt;
        }

        if (sensor == null)
        {
            return;
        }

        try
        {
            dynamic sensorDynamic = sensor;
            if (sensorDynamic.Status.ToString() != "Connected")
            {
                return;
            }

            sensorDynamic.ElevationAngle = target;
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
