using System;
using System.IO;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KinectWindowsWebCam;

internal static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "KinectWindowsWebCam";

    private static string Command => "\"" + Application.ExecutablePath + "\"";

    public static bool IsEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (run?.GetValue(ValueName) is not string command || command.Length == 0)
            {
                return false;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath);
            return !(approved?.GetValue(ValueName) is byte[] state && state.Length > 0 && (state[0] & 1) == 1);
        }
        catch (Exception exception) when (IsRegistryError(exception))
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (run == null)
                {
                    return false;
                }

                if (enabled)
                {
                    run.SetValue(ValueName, Command, RegistryValueKind.String);
                }
                else
                {
                    run.DeleteValue(ValueName, false);
                }
            }

            using (var approved = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, true))
            {
                approved?.DeleteValue(ValueName, false);
            }

            return IsEnabled() == enabled;
        }
        catch (Exception exception) when (IsRegistryError(exception))
        {
            return false;
        }
    }

    public static void UpdatePathIfEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (run?.GetValue(ValueName) is string command && command.Length > 0 && !string.Equals(command, Command, StringComparison.OrdinalIgnoreCase))
            {
                run.SetValue(ValueName, Command, RegistryValueKind.String);
            }
        }
        catch (Exception exception) when (IsRegistryError(exception))
        {
        }
    }

    private static bool IsRegistryError(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or SecurityException;
    }
}
