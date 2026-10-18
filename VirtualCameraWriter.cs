using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace KinectWindowsWebCam;

public sealed class VirtualCameraWriter : IDisposable
{
    private readonly string _mutexName;
    private readonly string _wantFrameEventName;
    private readonly string _sentFrameEventName;
    private readonly string _sharedDataName;
    private Mutex? _mutex;
    private EventWaitHandle? _wantFrameEvent;
    private EventWaitHandle? _sentFrameEvent;
    private MemoryMappedFile? _mappedFile;
    private MemoryMappedViewAccessor? _view;

    public VirtualCameraWriter(int captureDeviceIndex)
    {
        var suffix = captureDeviceIndex == 0 ? string.Empty : captureDeviceIndex.ToString();
        _mutexName = $"UnityCapture_Mutx{suffix}";
        _wantFrameEventName = $"UnityCapture_Want{suffix}";
        _sentFrameEventName = $"UnityCapture_Sent{suffix}";
        _sharedDataName = $"UnityCapture_Data{suffix}";
    }

    public bool SendFrame(int width, int height, int stride, byte[] bgraData)
    {
        if (bgraData.Length > SharedMemHeader.MaxSharedImageSize)
        {
            return false;
        }

        if (!EnsureConnected())
        {
            return false;
        }

        if (_view == null || _mutex == null || _sentFrameEvent == null)
        {
            return false;
        }

        var header = new SharedMemHeader
        {
            MaxSize = SharedMemHeader.MaxSharedImageSize,
            Width = width,
            Height = height,
            Stride = stride,
            Format = 0,
            ResizeMode = 0,
            MirrorMode = 0,
            Timeout = 1000
        };

        _mutex.WaitOne();
        try
        {
            _view.Write(0, ref header);
            _view.WriteArray(SharedMemHeader.Size, bgraData, 0, bgraData.Length);
        }
        finally
        {
            _mutex.ReleaseMutex();
        }

        _sentFrameEvent.Set();
        return true;
    }

    private bool EnsureConnected()
    {
        if (_view != null)
        {
            return true;
        }

        try
        {
            _mutex = Mutex.OpenExisting(_mutexName, MutexRights.Synchronize);
            _wantFrameEvent = EventWaitHandle.OpenExisting(_wantFrameEventName);
            _sentFrameEvent = EventWaitHandle.OpenExisting(_sentFrameEventName);
            _mappedFile = MemoryMappedFile.OpenExisting(_sharedDataName, MemoryMappedFileRights.ReadWrite);
            _view = _mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            return true;
        }
        catch
        {
            DisposeHandles();
            return false;
        }
    }

    private void DisposeHandles()
    {
        _view?.Dispose();
        _mappedFile?.Dispose();
        _wantFrameEvent?.Dispose();
        _sentFrameEvent?.Dispose();
        _mutex?.Dispose();
        _view = null;
        _mappedFile = null;
        _wantFrameEvent = null;
        _sentFrameEvent = null;
        _mutex = null;
    }

    public void Dispose()
    {
        DisposeHandles();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SharedMemHeader
    {
        public const int MaxSharedImageSize = 3840 * 2160 * 4 * sizeof(short);
        public const int Size = 32;
        public int MaxSize;
        public int Width;
        public int Height;
        public int Stride;
        public int Format;
        public int ResizeMode;
        public int MirrorMode;
        public int Timeout;
    }
}
