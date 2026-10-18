using System.Drawing;

namespace KinectWindowsWebCam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int OutputWidth = 1280;
    private const int OutputHeight = 960;
    private const int OutputStride = OutputWidth * 4;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayPopupForm _popup;
    private readonly KinectCaptureService _captureService;
    private readonly KinectMotorService _motorService;
    private readonly VirtualCameraWriter _cameraWriter;
    private readonly byte[] _outputBuffer = new byte[OutputWidth * OutputHeight * 4];
    private readonly object _renderLock = new();
    private volatile int _qualityIndex = 2;

    public TrayApplicationContext()
    {
        _popup = new TrayPopupForm();
        _captureService = new KinectCaptureService();
        _motorService = new KinectMotorService();
        _cameraWriter = new VirtualCameraWriter(0);

        _popup.QualityChanged += (_, quality) => _qualityIndex = quality;
        _popup.TiltChanged += (_, tilt) => _motorService.SetTargetTilt(tilt);
        _popup.ExitRequested += (_, _) => ExitThread();

        _captureService.FrameReady += OnFrameReady;
        _captureService.SensorStateChanged += OnSensorStateChanged;
        _captureService.SensorChanged += (_, sensor) => _motorService.SetSensor(sensor);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Kinect Windows WebCam"
        };
        _notifyIcon.MouseClick += (_, _) => TogglePopup();

        _captureService.Start();
    }

    private void OnSensorStateChanged(object? sender, KinectStatusEventArgs e)
    {
        if (_popup.IsDisposed)
        {
            return;
        }

        _popup.BeginInvoke(new Action(() => _popup.SetKinectStatus(e.Text)));
    }

    private void OnFrameReady(object? sender, KinectFrameEventArgs e)
    {
        lock (_renderLock)
        {
            var (renderWidth, renderHeight) = QualityPreset.GetRenderSize(_qualityIndex);
            var qualityBuffer = QualityPreset.GetReusableBuffer(renderWidth, renderHeight);
            FrameScaler.ScaleBgra(e.BgraFrame, e.Width, e.Height, qualityBuffer, renderWidth, renderHeight);
            FrameScaler.ScaleBgra(qualityBuffer, renderWidth, renderHeight, _outputBuffer, OutputWidth, OutputHeight);
            _cameraWriter.SendFrame(OutputWidth, OutputHeight, OutputStride, _outputBuffer);
        }
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        var cursor = Cursor.Position;
        var x = Math.Max(0, cursor.X - _popup.Width + 30);
        var y = Math.Max(0, cursor.Y - _popup.Height - 10);
        _popup.Location = new Point(x, y);
        _popup.Show();
        _popup.Activate();
    }

    protected override void ExitThreadCore()
    {
        _captureService.FrameReady -= OnFrameReady;
        _captureService.SensorStateChanged -= OnSensorStateChanged;
        _captureService.Dispose();
        _motorService.Dispose();
        _cameraWriter.Dispose();
        _popup.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}
