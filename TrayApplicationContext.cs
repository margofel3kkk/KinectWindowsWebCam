using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KinectWindowsWebCam;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string SdkDownloadUrl = "https://www.microsoft.com/en-us/download/details.aspx?id=40278";
    private const int PopupReopenGuardMs = 300;
    private const int NotifyIconTextLimit = 63;
    private const int BalloonTimeoutMs = 8000;
    private const int SaveDelayMs = 1000;

    private readonly AppSettings _settings;
    private readonly TrayPopupForm _popup;
    private readonly NotifyIcon _notifyIcon;
    private readonly Timer _statusTimer;
    private readonly Timer _saveTimer;
    private readonly TiltScheduler _tilt = new();
    private readonly CameraOutput _output;
    private readonly KinectCaptureService _capture;
    private bool _cameraInstalled;
    private bool _installing;
    private string _cameraProblem = "nie zainstalowana";
    private bool _driverBalloonShown;
    private bool _saveWarningShown;
    private bool _exited;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _output = new CameraOutput(_settings.Quality, _settings.Widescreen, _settings.Mirror);
        if (_settings.Tilt.HasValue)
        {
            _tilt.SetTarget(_settings.Tilt.Value);
        }

        Autostart.UpdatePathIfEnabled();

        _popup = new TrayPopupForm(_settings.Quality, _settings.Widescreen, _settings.Mirror, _settings.Tilt ?? 0, Autostart.IsEnabled());
        _popup.QualityChanged += (_, value) =>
        {
            _output.Quality = value;
            _settings.Quality = value;
            ScheduleSave();
        };
        _popup.TiltChanged += (_, value) =>
        {
            _tilt.SetTarget(value);
            _settings.Tilt = value;
            ScheduleSave();
        };
        _popup.WidescreenChanged += (_, value) =>
        {
            _output.Widescreen = value;
            _settings.Widescreen = value;
            ScheduleSave();
        };
        _popup.MirrorChanged += (_, value) =>
        {
            _output.Mirror = value;
            _settings.Mirror = value;
            ScheduleSave();
        };
        _popup.AutostartChanged += (_, value) => ChangeAutostart(value);
        _popup.InstallCameraRequested += (_, _) => InstallCamera();
        _popup.DownloadSdkRequested += (_, _) => OpenSdkDownload();
        _popup.ExitRequested += (_, _) => ExitThread();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = Program.AppName,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, _) => TogglePopup();

        _saveTimer = new Timer { Interval = SaveDelayMs };
        _saveTimer.Tick += (_, _) => SaveSettings();

        _capture = new KinectCaptureService(_tilt, _output);
        _capture.Start();

        _statusTimer = new Timer { Interval = 500 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();

        _cameraInstalled = VirtualCameraInstaller.IsInstalled();
        if (!_cameraInstalled)
        {
            InstallCamera();
        }

        RefreshStatus();
    }

    protected override void ExitThreadCore()
    {
        if (!_exited)
        {
            _exited = true;
            if (_saveTimer.Enabled)
            {
                SaveSettings();
            }

            _saveTimer.Dispose();
            _statusTimer.Stop();
            _statusTimer.Dispose();
            if (_capture.Stop(TimeSpan.FromSeconds(3)))
            {
                _output.Dispose();
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _popup.Hide();
        }

        base.ExitThreadCore();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveSettings()
    {
        _saveTimer.Stop();
        if (_settings.Save() || _saveWarningShown || _exited)
        {
            return;
        }

        _saveWarningShown = true;
        ShowBalloon("Nie udało się zapisać ustawień w " + AppSettings.DirectoryPath + ".", ToolTipIcon.Warning);
    }

    private void ChangeAutostart(bool enabled)
    {
        var changed = Autostart.SetEnabled(enabled);
        _popup.SetAutostart(Autostart.IsEnabled());
        if (!changed)
        {
            ShowBalloon("Nie udało się zmienić autostartu (brak dostępu do rejestru).", ToolTipIcon.Warning);
        }
    }

    private async void InstallCamera()
    {
        if (_installing)
        {
            return;
        }

        _installing = true;
        RefreshStatus();

        var result = await Task.Run(VirtualCameraInstaller.Install);

        _installing = false;
        if (_exited)
        {
            return;
        }

        _cameraInstalled = VirtualCameraInstaller.IsInstalled();
        if (!_cameraInstalled)
        {
            _cameraProblem = VirtualCameraInstaller.Describe(result == CameraInstallResult.Success ? CameraInstallResult.RegistrationFailed : result);
            ShowBalloon("Kamera wirtualna: " + _cameraProblem + ". Kliknij ikonę, aby spróbować ponownie.", ToolTipIcon.Warning);
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_exited)
        {
            return;
        }

        var kinect = _capture.Status;
        var driverMissing = kinect.State == KinectState.DriverMissing;
        _popup.SetKinectStatus(kinect.Text, driverMissing);
        if (driverMissing && !_driverBalloonShown)
        {
            _driverBalloonShown = true;
            ShowBalloon("Brak sterownika Kinecta. Zainstaluj Kinect for Windows SDK 1.8 (przycisk w oknie po kliknięciu ikony).", ToolTipIcon.Warning);
        }

        string camera;
        var outputError = _output.LastError;
        if (_installing)
        {
            camera = "instalowanie…";
        }
        else if (!_cameraInstalled)
        {
            camera = _cameraProblem;
        }
        else if (outputError != null)
        {
            camera = "błąd: " + outputError;
        }
        else
        {
            camera = _output.IsConsumerActive ? "nadaje" : $"gotowa – wybierz „{VirtualCameraInstaller.DeviceName}”";
        }

        _popup.SetCameraStatus(camera, !_cameraInstalled && !_installing);

        if (_capture.TryTakeTiltReading(out var angle))
        {
            _popup.SetTilt(angle);
        }

        var tooltip = $"Kinect: {kinect.Text}";
        _notifyIcon.Text = tooltip.Length > NotifyIconTextLimit ? tooltip.Substring(0, NotifyIconTextLimit) : tooltip;
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        if (unchecked(Environment.TickCount - _popup.LastHiddenTick) < PopupReopenGuardMs)
        {
            return;
        }

        RefreshStatus();
        _popup.SetAutostart(Autostart.IsEnabled());
        _popup.PerformLayout();

        var cursor = Cursor.Position;
        var area = Screen.FromPoint(cursor).WorkingArea;
        var size = _popup.Size;
        var x = Math.Max(area.Left, Math.Min(cursor.X - size.Width / 2, area.Right - size.Width));
        var y = cursor.Y - size.Height - 8;
        if (y < area.Top)
        {
            y = Math.Max(area.Top, Math.Min(cursor.Y + 8, area.Bottom - size.Height));
        }

        _popup.Location = new Point(x, y);
        _popup.Show();
        _popup.Activate();
    }

    private void OpenSdkDownload()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SdkDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show("Otwórz ręcznie: " + SdkDownloadUrl, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowBalloon(string text, ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(BalloonTimeoutMs, Program.AppName, text, icon);
    }
}
