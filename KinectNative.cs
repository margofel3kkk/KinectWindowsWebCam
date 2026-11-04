using System;
using System.Runtime.InteropServices;

namespace KinectWindowsWebCam;

internal static class NuiConstants
{
    public const uint InitializeUsesColor = 0x00000002;
    public const int ImageTypeColor = 1;
    public const int ImageResolution640x480 = 2;
    public const uint ColorFrameLimit = 2;
    public const int ColorWidth = 640;
    public const int ColorHeight = 480;
    public const int ElevationMinimum = -27;
    public const int ElevationMaximum = 27;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NuiImageFrame
{
    public long TimeStamp;
    public uint FrameNumber;
    public int ImageType;
    public int Resolution;
    public IntPtr FrameTexture;
    public uint FrameFlags;
    public int ViewAreaDigitalZoom;
    public int ViewAreaCenterX;
    public int ViewAreaCenterY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NuiLockedRect
{
    public int Pitch;
    public int Size;
    public IntPtr Bits;
}

internal static class Kinect10
{
    private const string Library = "Kinect10.dll";

    [DllImport(Library)]
    public static extern int NuiGetSensorCount(out int count);

    [DllImport(Library)]
    public static extern int NuiCreateSensorByIndex(int index, out IntPtr sensor);
}

internal static class ComVtable
{
    public static IntPtr GetFunction(IntPtr instance, int slot)
    {
        var table = Marshal.ReadIntPtr(instance);
        return Marshal.ReadIntPtr(table, slot * IntPtr.Size);
    }

    public static T GetMethod<T>(IntPtr instance, int slot) where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(GetFunction(instance, slot));
    }
}

internal sealed class NuiSensor : IDisposable
{
    private const int ReleaseSlot = 2;
    private const int InitializeSlot = 3;
    private const int ShutdownSlot = 4;
    private const int ImageStreamOpenSlot = 6;
    private const int ImageStreamGetNextFrameSlot = 9;
    private const int ImageStreamReleaseFrameSlot = 10;
    private const int CameraElevationSetAngleSlot = 14;
    private const int CameraElevationGetAngleSlot = 15;
    private const int StatusSlot = 26;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseMethod(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InitializeMethod(IntPtr self, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ShutdownMethod(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ImageStreamOpenMethod(IntPtr self, int imageType, int resolution, uint frameFlags, uint frameLimit, IntPtr nextFrameEvent, out IntPtr stream);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ImageStreamGetNextFrameMethod(IntPtr self, IntPtr stream, uint millisecondsToWait, out NuiImageFrame frame);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ImageStreamReleaseFrameMethod(IntPtr self, IntPtr stream, ref NuiImageFrame frame);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CameraElevationSetAngleMethod(IntPtr self, int angle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CameraElevationGetAngleMethod(IntPtr self, out int angle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StatusMethod(IntPtr self);

    private readonly ReleaseMethod _release;
    private readonly InitializeMethod _initialize;
    private readonly ShutdownMethod _shutdown;
    private readonly ImageStreamOpenMethod _imageStreamOpen;
    private readonly ImageStreamGetNextFrameMethod _imageStreamGetNextFrame;
    private readonly ImageStreamReleaseFrameMethod _imageStreamReleaseFrame;
    private readonly CameraElevationSetAngleMethod _cameraElevationSetAngle;
    private readonly CameraElevationGetAngleMethod _cameraElevationGetAngle;
    private readonly StatusMethod _status;
    private IntPtr _instance;
    private bool _initialized;

    public NuiSensor(IntPtr instance)
    {
        _instance = instance;
        _release = ComVtable.GetMethod<ReleaseMethod>(instance, ReleaseSlot);
        _initialize = ComVtable.GetMethod<InitializeMethod>(instance, InitializeSlot);
        _shutdown = ComVtable.GetMethod<ShutdownMethod>(instance, ShutdownSlot);
        _imageStreamOpen = ComVtable.GetMethod<ImageStreamOpenMethod>(instance, ImageStreamOpenSlot);
        _imageStreamGetNextFrame = ComVtable.GetMethod<ImageStreamGetNextFrameMethod>(instance, ImageStreamGetNextFrameSlot);
        _imageStreamReleaseFrame = ComVtable.GetMethod<ImageStreamReleaseFrameMethod>(instance, ImageStreamReleaseFrameSlot);
        _cameraElevationSetAngle = ComVtable.GetMethod<CameraElevationSetAngleMethod>(instance, CameraElevationSetAngleSlot);
        _cameraElevationGetAngle = ComVtable.GetMethod<CameraElevationGetAngleMethod>(instance, CameraElevationGetAngleSlot);
        _status = ComVtable.GetMethod<StatusMethod>(instance, StatusSlot);
    }

    public int GetStatus() => _status(_instance);

    public int Initialize(uint flags)
    {
        var hr = _initialize(_instance, flags);
        _initialized = hr >= 0;
        return hr;
    }

    public int OpenColorStream(out IntPtr stream)
    {
        return _imageStreamOpen(_instance, NuiConstants.ImageTypeColor, NuiConstants.ImageResolution640x480, 0, NuiConstants.ColorFrameLimit, IntPtr.Zero, out stream);
    }

    public int GetNextFrame(IntPtr stream, uint millisecondsToWait, out NuiImageFrame frame)
    {
        return _imageStreamGetNextFrame(_instance, stream, millisecondsToWait, out frame);
    }

    public int ReleaseFrame(IntPtr stream, ref NuiImageFrame frame)
    {
        return _imageStreamReleaseFrame(_instance, stream, ref frame);
    }

    public int SetElevationAngle(int angle) => _cameraElevationSetAngle(_instance, angle);

    public int GetElevationAngle(out int angle) => _cameraElevationGetAngle(_instance, out angle);

    public void Dispose()
    {
        if (_instance == IntPtr.Zero)
        {
            return;
        }

        if (_initialized)
        {
            _shutdown(_instance);
            _initialized = false;
        }

        _release(_instance);
        _instance = IntPtr.Zero;
    }
}

internal sealed class NuiFrameTextureAccessor
{
    private const int LockRectSlot = 5;
    private const int UnlockRectSlot = 7;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LockRectMethod(IntPtr self, uint level, out NuiLockedRect lockedRect, IntPtr rect, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int UnlockRectMethod(IntPtr self, uint level);

    private IntPtr _lockRectFunction;
    private LockRectMethod? _lockRect;
    private IntPtr _unlockRectFunction;
    private UnlockRectMethod? _unlockRect;

    public int LockRect(IntPtr texture, out NuiLockedRect lockedRect)
    {
        var function = ComVtable.GetFunction(texture, LockRectSlot);
        if (_lockRect == null || function != _lockRectFunction)
        {
            _lockRect = Marshal.GetDelegateForFunctionPointer<LockRectMethod>(function);
            _lockRectFunction = function;
        }

        return _lockRect(texture, 0, out lockedRect, IntPtr.Zero, 0);
    }

    public int UnlockRect(IntPtr texture)
    {
        var function = ComVtable.GetFunction(texture, UnlockRectSlot);
        if (_unlockRect == null || function != _unlockRectFunction)
        {
            _unlockRect = Marshal.GetDelegateForFunctionPointer<UnlockRectMethod>(function);
            _unlockRectFunction = function;
        }

        return _unlockRect(texture, 0);
    }
}

internal static class NuiErrors
{
    public const int FrameNoData = unchecked((int)0x83010001);
    private const uint Initializing = 0x01830001;
    private const uint DeviceNotConnected = 0x8007048F;
    private const uint DeviceNotReady = 0x80070015;
    private const uint NotGenuine = 0x83010006;
    private const uint InsufficientBandwidth = 0x83010007;
    private const uint NotSupported = 0x83010008;
    private const uint DeviceInUse = 0x83010009;
    private const uint NotConnected = 0x83010014;
    private const uint NotReady = 0x83010015;
    private const uint NotPowered = 0x8301027F;

    public static KinectState StateFor(int hr)
    {
        return (uint)hr is DeviceNotConnected or NotConnected ? KinectState.NotConnected : KinectState.Error;
    }

    public static string Describe(int hr)
    {
        var text = (uint)hr switch
        {
            DeviceNotConnected or NotConnected => "nie podłączony",
            NotPowered => "brak zasilania – podłącz zasilacz Kinecta",
            DeviceNotReady or NotReady => "nie jest gotowy",
            Initializing => "inicjalizacja",
            DeviceInUse => "używany przez inną aplikację",
            InsufficientBandwidth => "za mała przepustowość USB – użyj innego portu",
            NotGenuine or NotSupported => "nieobsługiwany – wymagany pełny Kinect for Windows SDK 1.8",
            _ => "błąd"
        };

        return $"{text} (0x{hr:X8})";
    }
}
