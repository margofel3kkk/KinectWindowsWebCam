using System;

namespace KinectWindowsWebCam;

internal sealed class TiltScheduler
{
    private const int DebounceMs = 400;
    private const int MinimumIntervalMs = 1400;

    private readonly object _sync = new();
    private int _target;
    private int _applied = int.MinValue;
    private bool _pending;
    private int _changedTick;
    private int _appliedTick;
    private bool _hasApplied;

    public static int Clamp(int angle)
    {
        return Math.Max(NuiConstants.ElevationMinimum, Math.Min(NuiConstants.ElevationMaximum, angle));
    }

    public void SetTarget(int angle)
    {
        lock (_sync)
        {
            _target = Clamp(angle);
            _pending = true;
            _changedTick = Environment.TickCount;
        }
    }

    public bool SetCurrent(int angle)
    {
        lock (_sync)
        {
            _applied = Clamp(angle);
            return !_pending;
        }
    }

    public bool TryTake(out int angle)
    {
        lock (_sync)
        {
            angle = _target;
            if (!_pending)
            {
                return false;
            }

            if (_target == _applied)
            {
                _pending = false;
                return false;
            }

            var now = Environment.TickCount;
            if (unchecked(now - _changedTick) < DebounceMs)
            {
                return false;
            }

            if (_hasApplied && unchecked(now - _appliedTick) < MinimumIntervalMs)
            {
                return false;
            }

            _pending = false;
            _applied = _target;
            _appliedTick = now;
            _hasApplied = true;
            return true;
        }
    }
}
