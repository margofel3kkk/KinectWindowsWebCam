namespace KinectWindowsWebCam;

public sealed class TrayPopupForm : Form
{
    private readonly TrackBar _qualityTrackBar;
    private readonly TrackBar _tiltTrackBar;
    private readonly Label _statusLabel;

    public event EventHandler<int>? QualityChanged;
    public event EventHandler<int>? TiltChanged;
    public event EventHandler? ExitRequested;

    public TrayPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Width = 320;
        Height = 250;

        var qualityLabel = new Label
        {
            Text = "Jakość",
            Left = 12,
            Top = 12,
            Width = 280
        };

        _qualityTrackBar = new TrackBar
        {
            Left = 12,
            Top = 35,
            Width = 290,
            Minimum = 0,
            Maximum = 2,
            TickStyle = TickStyle.BottomRight,
            Value = 2
        };
        _qualityTrackBar.ValueChanged += (_, _) => QualityChanged?.Invoke(this, _qualityTrackBar.Value);

        var tiltLabel = new Label
        {
            Text = "Tilt",
            Left = 12,
            Top = 85,
            Width = 280
        };

        _tiltTrackBar = new TrackBar
        {
            Left = 12,
            Top = 108,
            Width = 290,
            Minimum = -27,
            Maximum = 27,
            TickFrequency = 3,
            Value = 0
        };
        _tiltTrackBar.ValueChanged += (_, _) => TiltChanged?.Invoke(this, _tiltTrackBar.Value);

        _statusLabel = new Label
        {
            Left = 12,
            Top = 165,
            Width = 290,
            Text = "Kinect: inicjalizacja"
        };

        var exitButton = new Button
        {
            Text = "Exit",
            Left = 12,
            Top = 192,
            Width = 290,
            Height = 36
        };
        exitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(qualityLabel);
        Controls.Add(_qualityTrackBar);
        Controls.Add(tiltLabel);
        Controls.Add(_tiltTrackBar);
        Controls.Add(_statusLabel);
        Controls.Add(exitButton);
    }

    public void SetKinectStatus(string status)
    {
        _statusLabel.Text = $"Kinect: {status}";
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }
}
