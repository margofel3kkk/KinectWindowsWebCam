using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KinectWindowsWebCam;

internal enum CameraInstallResult
{
    Success = 0,
    Cancelled = 1,
    FileError = 2,
    RegistrationFailed = 3,
    AccessDenied = 4,
    Failed = 5
}

internal static class VirtualCameraInstaller
{
    public const string InstallArgument = "--install-camera";
    public const string UninstallArgument = "--uninstall-camera";
    public const string DeviceName = "Kinect WebCam";

    private const string VideoInputDeviceCategory = "{860BB310-5D01-11d0-BD3B-00A0C911CE86}";
    private const string LicenseResource = "UnityCapture-LICENSE.txt";
    private const int ErrorCancelled = 1223;
    private const int Regsvr32TimeoutMs = 60000;

    private static readonly FilterBinary[] Filters =
    {
        new("UnityCaptureFilter64.dll", "{5C2CD55C-92AD-4999-8666-912BD3E70010}", RegistryView.Registry64, Environment.SystemDirectory),
        new("UnityCaptureFilter32.dll", "{5C2CD55C-92AD-4999-8666-912BD3E70020}", RegistryView.Registry32, Environment.GetFolderPath(Environment.SpecialFolder.SystemX86))
    };

    public static string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KinectWindowsWebCam");

    public static bool IsInstalled() => Filters.All(IsRegistered);

    public static CameraInstallResult Install() => RunPrivileged(InstallArgument);

    public static CameraInstallResult Uninstall() => RunPrivileged(UninstallArgument);

    public static int RunElevatedCommand(string argument)
    {
        try
        {
            return (int)(argument == InstallArgument ? InstallFiles() : RemoveFiles());
        }
        catch (Exception)
        {
            return (int)CameraInstallResult.Failed;
        }
    }

    public static string Describe(CameraInstallResult result)
    {
        return result switch
        {
            CameraInstallResult.Success => "zainstalowana",
            CameraInstallResult.Cancelled => "nie zainstalowana – odrzucono prośbę o uprawnienia administratora",
            CameraInstallResult.FileError => "nie można zapisać plików – zamknij aplikacje używające kamery",
            CameraInstallResult.RegistrationFailed => "rejestracja filtra kamery nie powiodła się",
            CameraInstallResult.AccessDenied => "brak uprawnień administratora",
            _ => "instalacja nie powiodła się"
        };
    }

    private static CameraInstallResult RunPrivileged(string argument)
    {
        if (IsElevated())
        {
            return (CameraInstallResult)RunElevatedCommand(argument);
        }

        try
        {
            var startInfo = new ProcessStartInfo(Application.ExecutablePath, argument)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return CameraInstallResult.Failed;
            }

            process.WaitForExit();
            return Enum.IsDefined(typeof(CameraInstallResult), process.ExitCode) ? (CameraInstallResult)process.ExitCode : CameraInstallResult.Failed;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            return CameraInstallResult.Cancelled;
        }
        catch (Win32Exception)
        {
            return CameraInstallResult.Failed;
        }
    }

    private static CameraInstallResult InstallFiles()
    {
        try
        {
            Directory.CreateDirectory(InstallDirectory);
            foreach (var filter in Filters)
            {
                WriteResource(filter.FileName, filter.InstalledPath);
            }

            WriteResource(LicenseResource, Path.Combine(InstallDirectory, LicenseResource));
        }
        catch (UnauthorizedAccessException)
        {
            return CameraInstallResult.AccessDenied;
        }
        catch (IOException)
        {
            return CameraInstallResult.FileError;
        }

        foreach (var filter in Filters)
        {
            var arguments = $"/s \"/i:UnityCaptureName={DeviceName}\" \"{filter.InstalledPath}\"";
            if (RunRegsvr32(filter.Regsvr32Path, arguments) != 0)
            {
                return CameraInstallResult.RegistrationFailed;
            }
        }

        return IsInstalled() ? CameraInstallResult.Success : CameraInstallResult.RegistrationFailed;
    }

    private static CameraInstallResult RemoveFiles()
    {
        foreach (var filter in Filters)
        {
            if (File.Exists(filter.InstalledPath))
            {
                RunRegsvr32(filter.Regsvr32Path, $"/u /s \"{filter.InstalledPath}\"");
            }
        }

        try
        {
            foreach (var filter in Filters)
            {
                File.Delete(filter.InstalledPath);
            }

            File.Delete(Path.Combine(InstallDirectory, LicenseResource));
            if (Directory.Exists(InstallDirectory) && !Directory.EnumerateFileSystemEntries(InstallDirectory).Any())
            {
                Directory.Delete(InstallDirectory);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return CameraInstallResult.AccessDenied;
        }
        catch (IOException)
        {
            return CameraInstallResult.FileError;
        }

        return CameraInstallResult.Success;
    }

    private static bool IsRegistered(FilterBinary filter)
    {
        try
        {
            using var classes = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, filter.View);
            using var server = classes.OpenSubKey($@"CLSID\{filter.Clsid}\InprocServer32");
            if (server?.GetValue(null) is not string registeredPath)
            {
                return false;
            }

            if (!PathsEqual(registeredPath, filter.InstalledPath) || !File.Exists(filter.InstalledPath))
            {
                return false;
            }

            using var device = classes.OpenSubKey($@"CLSID\{VideoInputDeviceCategory}\Instance\{filter.Clsid}");
            return device != null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left.Trim().Trim('"')), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteResource(string resourceName, string path)
    {
        var data = ReadResource(resourceName);
        if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(data))
        {
            return;
        }

        File.WriteAllBytes(path, data);
    }

    private static byte[] ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Brak zasobu " + resourceName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static int RunRegsvr32(string regsvr32Path, string arguments)
    {
        var startInfo = new ProcessStartInfo(regsvr32Path, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return -1;
        }

        if (!process.WaitForExit(Regsvr32TimeoutMs))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            return -1;
        }

        return process.ExitCode;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private sealed class FilterBinary
    {
        public FilterBinary(string fileName, string clsid, RegistryView view, string systemDirectory)
        {
            FileName = fileName;
            Clsid = clsid;
            View = view;
            Regsvr32Path = Path.Combine(systemDirectory, "regsvr32.exe");
        }

        public string FileName { get; }
        public string Clsid { get; }
        public RegistryView View { get; }
        public string Regsvr32Path { get; }
        public string InstalledPath => Path.Combine(InstallDirectory, FileName);
    }
}
