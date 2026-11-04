using System;
using System.Drawing;
using System.Windows.Forms;

namespace KinectWindowsWebCam;

internal sealed class TrayPopupForm : Form
{
    private const int ContentWidth = 290;
    private const int DropShadowClassStyle = 0x00020000;
    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color ButtonColor = Color.FromArgb(60, 60, 60);
    private static readonly Color ButtonBorderColor = Color.FromArgb(95, 95, 95);
    private static readonly Color StatusTextColor = Color.FromArgb(200, 200, 200);

    private readonly Label _qualityLabel;
    private readonly TrackBar _qualityTrackBar;
    private readonly Label _tiltLabel;
    private readonly TrackBar _tiltTrackBar;
    private readonly CheckBox _widescreenCheckBox;
    private readonly CheckBox _mirrorCheckBox;
    private readonly CheckBox _autostartCheckBox;
    private readonly Label _kinectStatusLabel;
    private readonly Button _downloadSdkButton;
    private readonly Label _cameraStatusLabel;
    private readonly Button _installCameraButton;
    private bool _updatingTilt;
    private bool _updatingAutostart;

    public TrayPopupForm(int quality, bool widescreen, bool mirror, int tilt, bool autostart)
    {
        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Text = "Kinect Windows WebCam";

        _qualityLabel = CreateLabel(Color.White);
        _qualityTrackBar = CreateTrackBar(0, FrameConverter.QualityLevels - 1, 1, quality);
        _tiltLabel = CreateLabel(Color.White);
        _tiltTrackBar = CreateTrackBar(NuiConstants.ElevationMinimum, NuiConstants.ElevationMaximum, 3, tilt);
        _widescreenCheckBox = CreateCheckBox("Kadr 16:9 (przycięty z 4:3)", widescreen);
        _mirrorCheckBox = CreateCheckBox("Odbicie lustrzane", mirror);
        _autostartCheckBox = CreateCheckBox("Uruchamiaj razem z Windows", autostart);
        _kinectStatusLabel = CreateLabel(StatusTextColor);
        _downloadSdkButton = CreateButton("Pobierz Kinect for Windows SDK 1.8");
        _cameraStatusLabel = CreateLabel(StatusTextColor);
        _installCameraButton = CreateButton("Zainstaluj kamerę wirtualną");
        var exitButton = CreateButton("Zamknij aplikację");
        _downloadSdkButton.Visible = false;
        _installCameraButton.Visible = false;

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = Padding.Empty,
            Location = Point.Empty,
            BackColor = BackgroundColor
        };
        layout.Controls.AddRange(new Control[]
        {
            _qualityLabel,
            _qualityTrackBar,
            _tiltLabel,
            _tiltTrackBar,
            _widescreenCheckBox,
            _mirrorCheckBox,
            _autostartCheckBox,
            _kinectStatusLabel,
            _downloadSdkButton,
            _cameraStatusLabel,
            _installCameraButton,
            exitButton
        });
        Controls.Add(layout);

        UpdateQualityLabel();
        UpdateTiltLabel();
        SetKinectStatus("uruchamianie…", false);
        SetCameraStatus("sprawdzanie…", false);

        _qualityTrackBar.ValueChanged += (_, _) =>
        {
            UpdateQualityLabel();
            QualityChanged?.Invoke(this, _qualityTrackBar.Value);
        };
        _tiltTrackBar.ValueChanged += (_, _) =>
        {
            UpdateTiltLabel();
            if (!_updatingTilt)
            {
                TiltChanged?.Invoke(this, _tiltTrackBar.Value);
            }
        };
        _widescreenCheckBox.CheckedChanged += (_, _) =>
        {
            UpdateQualityLabel();
            WidescreenChanged?.Invoke(this, _widescreenCheckBox.Checked);
        };
        _mirrorCheckBox.CheckedChanged += (_, _) => MirrorChanged?.Invoke(this, _mirrorCheckBox.Checked);
        _autostartCheckBox.CheckedChanged += (_, _) =>
        {
            if (!_updatingAutostart)
            {
                AutostartChanged?.Invoke(this, _autostartCheckBox.Checked);
            }
        };
        _downloadSdkButton.Click += (_, _) => DownloadSdkRequested?.Invoke(this, EventArgs.Empty);
        _installCameraButton.Click += (_, _) => InstallCameraRequested?.Invoke(this, EventArgs.Empty);
        exitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        ResumeLayout(true);
    }

    public event EventHandler<int>? QualityChanged;
    public event EventHandler<int>? TiltChanged;
    public event EventHandler<bool>? WidescreenChanged;
    public event EventHandler<bool>? MirrorChanged;
    public event EventHandler<bool>? AutostartChanged;
    public event EventHandler? InstallCameraRequested;
    public event EventHandler? DownloadSdkRequested;
    public event EventHandler? ExitRequested;

    public int LastHiddenTick { get; private set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= DropShadowClassStyle;
            return parameters;
        }
    }

    public void SetKinectStatus(string text, bool showSdkDownload)
    {
        SetText(_kinectStatusLabel, "Kinect: " + text);
        SetVisible(_downloadSdkButton, showSdkDownload);
    }

    public void SetCameraStatus(string text, bool showInstall)
    {
        SetText(_cameraStatusLabel, "Kamera wirtualna: " + text);
        SetVisible(_installCameraButton, showInstall);
    }

    public void SetTilt(int angle)
    {
        var value = TiltScheduler.Clamp(angle);
        if (_tiltTrackBar.Value == value)
        {
            return;
        }

        _updatingTilt = true;
        try
        {
            _tiltTrackBar.Value = value;
        }
        finally
        {
            _updatingTilt = false;
        }
    }

    public void SetAutostart(bool enabled)
    {
        if (_autostartCheckBox.Checked == enabled)
        {
            return;
        }

        _updatingAutostart = true;
        try
        {
            _autostartCheckBox.Checked = enabled;
        }
        finally
        {
            _updatingAutostart = false;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        HidePopup();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HidePopup();
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            HidePopup();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void HidePopup()
    {
        if (!Visible)
        {
            return;
        }

        Hide();
        LastHiddenTick = Environment.TickCount;
    }

    private void UpdateQualityLabel()
    {
        var size = FrameConverter.GetOutputSize(_qualityTrackBar.Value, _widescreenCheckBox.Checked);
        SetText(_qualityLabel, $"Jakość źródła: {size.Width}×{size.Height}");
    }

    private void UpdateTiltLabel()
    {
        SetText(_tiltLabel, $"Nachylenie: {_tiltTrackBar.Value:+0;-0;0}°");
    }

    private static void SetText(Control control, string text)
    {
        if (control.Text != text)
        {
            control.Text = text;
        }
    }

    private static void SetVisible(Control control, bool visible)
    {
        if (control.Visible != visible)
        {
            control.Visible = visible;
        }
    }

    private static Label CreateLabel(Color color)
    {
        return new Label
        {
            AutoSize = true,
            MinimumSize = new Size(ContentWidth, 0),
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = color,
            Margin = new Padding(0, 6, 0, 0)
        };
    }

    private static TrackBar CreateTrackBar(int minimum, int maximum, int tickFrequency, int value)
    {
        return new TrackBar
        {
            Minimum = minimum,
            Maximum = maximum,
            TickFrequency = tickFrequency,
            Value = Math.Max(minimum, Math.Min(maximum, value)),
            TickStyle = TickStyle.BottomRight,
            Width = ContentWidth,
            BackColor = BackgroundColor,
            Margin = Padding.Empty
        };
    }

    private static CheckBox CreateCheckBox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(0, 4, 0, 0)
        };
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = ContentWidth,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonColor,
            ForeColor = Color.White,
            UseVisualStyleBackColor = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        button.FlatAppearance.BorderColor = ButtonBorderColor;
        return button;
    }
}
