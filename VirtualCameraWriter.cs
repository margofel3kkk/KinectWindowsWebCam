using System;
using System.Threading;

namespace KinectWindowsWebCam;

internal sealed unsafe class VirtualCameraWriter : IDisposable
{
    private const string MutexName = "UnityCapture_Mutx";
    private const string WantFrameEventName = "UnityCapture_Want";
    private const string SentFrameEventName = "UnityCapture_Sent";
    private const string SharedDataName = "UnityCapture_Data";
    private const int HeaderSize = 32;
    private const int FormatUInt8 = 0;
    private const int ResizeModeLinear = 1;
    private const int MirrorModeDisabled = 0;
    private const int MirrorModeHorizontal = 1;
    private const int ReceiverTimeoutMs = 2000;
    private const uint MutexWaitMs = 1000;
    private const int OpenRetryMs = 500;
    private const int ConsumerActiveMs = 2000;

    private IntPtr _mutex;
    private IntPtr _wantFrameEvent;
    private IntPtr _sentFrameEvent;
    private IntPtr _sharedFile;
    private byte* _view;
    private bool _openAttempted;
    private int _lastOpenAttemptTick;
    private int _lastRequestTick;
    private volatile bool _requested;

    public bool IsConsumerActive => _requested && unchecked(Environment.TickCount - Volatile.Read(ref _lastRequestTick)) < ConsumerActiveMs;

    public bool TryBeginFrame()
    {
        if (!EnsureOpen())
        {
            return false;
        }

        var header = (int*)_view;
        if (header[1] == 0)
        {
            return true;
        }

        if (NativeMethods.WaitForSingleObject(_wantFrameEvent, 0) != NativeMethods.WaitObject0)
        {
            return false;
        }

        Volatile.Write(ref _lastRequestTick, Environment.TickCount);
        _requested = true;
        return true;
    }

    public void Send(int width, int height, uint[] rgbaPixels, bool mirror)
    {
        if (_view == null)
        {
            return;
        }

        var header = (int*)_view;
        var dataSize = (long)width * height * 4;
        if (dataSize > (uint)header[0] || dataSize > (long)rgbaPixels.Length * 4)
        {
            return;
        }

        if (!LockMutex())
        {
            return;
        }

        try
        {
            header[1] = width;
            header[2] = height;
            header[3] = width;
            header[4] = FormatUInt8;
            header[5] = ResizeModeLinear;
            header[6] = mirror ? MirrorModeHorizontal : MirrorModeDisabled;
            header[7] = ReceiverTimeoutMs;
            fixed (uint* source = rgbaPixels)
            {
                Buffer.MemoryCopy(source, _view + HeaderSize, dataSize, dataSize);
            }
        }
        finally
        {
            NativeMethods.ReleaseMutex(_mutex);
        }

        NativeMethods.SetEvent(_sentFrameEvent);
    }

    public void Dispose()
    {
        if (_view != null)
        {
            NativeMethods.UnmapViewOfFile((IntPtr)_view);
            _view = null;
        }

        Close(ref _sharedFile);
        Close(ref _sentFrameEvent);
        Close(ref _wantFrameEvent);
        Close(ref _mutex);
    }

    private bool EnsureOpen()
    {
        if (_view != null)
        {
            return true;
        }

        var now = Environment.TickCount;
        if (_openAttempted && unchecked(now - _lastOpenAttemptTick) < OpenRetryMs)
        {
            return false;
        }

        _openAttempted = true;
        _lastOpenAttemptTick = now;

        if (_mutex == IntPtr.Zero)
        {
            _mutex = NativeMethods.OpenMutex(NativeMethods.Synchronize, false, MutexName);
            if (_mutex == IntPtr.Zero)
            {
                return false;
            }
        }

        if (!LockMutex())
        {
            return false;
        }

        try
        {
            if (_wantFrameEvent == IntPtr.Zero)
            {
                _wantFrameEvent = NativeMethods.CreateEvent(IntPtr.Zero, false, false, WantFrameEventName);
                if (_wantFrameEvent == IntPtr.Zero)
                {
                    return false;
                }
            }

            if (_sentFrameEvent == IntPtr.Zero)
            {
                _sentFrameEvent = NativeMethods.OpenEvent(NativeMethods.EventModifyState, false, SentFrameEventName);
                if (_sentFrameEvent == IntPtr.Zero)
                {
                    return false;
                }
            }

            if (_sharedFile == IntPtr.Zero)
            {
                _sharedFile = NativeMethods.OpenFileMapping(NativeMethods.FileMapWrite, false, SharedDataName);
                if (_sharedFile == IntPtr.Zero)
                {
                    return false;
                }
            }

            var view = NativeMethods.MapViewOfFile(_sharedFile, NativeMethods.FileMapWrite, 0, 0, UIntPtr.Zero);
            if (view == IntPtr.Zero)
            {
                return false;
            }

            _view = (byte*)view;
            return true;
        }
        finally
        {
            NativeMethods.ReleaseMutex(_mutex);
        }
    }

    private bool LockMutex()
    {
        var result = NativeMethods.WaitForSingleObject(_mutex, MutexWaitMs);
        return result == NativeMethods.WaitObject0 || result == NativeMethods.WaitAbandoned;
    }

    private static void Close(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.CloseHandle(handle);
        handle = IntPtr.Zero;
    }
}
