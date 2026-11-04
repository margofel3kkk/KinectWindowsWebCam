using System;
using System.Threading;
using System.Windows.Forms;

namespace KinectWindowsWebCam;

internal static class Program
{
    public const string AppName = "Kinect Windows WebCam";
    private const string UninstallArgument = "--uninstall";
    private const string InstanceMutexName = @"Local\KinectWindowsWebCam.Instance";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;
        switch (command)
        {
            case VirtualCameraInstaller.InstallArgument:
            case VirtualCameraInstaller.UninstallArgument:
                return VirtualCameraInstaller.RunElevatedCommand(command);
            case UninstallArgument:
                return Uninstall();
        }

        using var instance = new Mutex(false, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Aplikacja już działa – ikona jest w zasobniku systemowym (obok zegara).", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);
        Application.Run(new TrayApplicationContext());
        return 0;
    }

    private static int Uninstall()
    {
        if (Mutex.TryOpenExisting(InstanceMutexName, out var runningInstance))
        {
            runningInstance.Dispose();
            MessageBox.Show("Najpierw zamknij działającą aplikację (ikona w zasobniku → Zamknij aplikację).", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return (int)CameraInstallResult.Failed;
        }

        var result = VirtualCameraInstaller.Uninstall();
        if (result == CameraInstallResult.Success)
        {
            Autostart.SetEnabled(false);
            AppSettings.Delete();
            MessageBox.Show("Kamera wirtualna, autostart i ustawienia zostały usunięte.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Odinstalowanie nie powiodło się: " + VirtualCameraInstaller.Describe(result), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return (int)result;
    }

    private static void ShowError(Exception? exception)
    {
        MessageBox.Show("Nieoczekiwany błąd: " + (exception?.ToString() ?? "nieznany"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
