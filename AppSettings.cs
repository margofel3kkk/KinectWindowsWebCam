using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;

namespace KinectWindowsWebCam;

internal sealed class AppSettings
{
    private const string QualityKey = "quality";
    private const string WidescreenKey = "widescreen";
    private const string MirrorKey = "mirror";
    private const string TiltKey = "tilt";

    public int Quality { get; set; } = FrameConverter.DefaultQuality;

    public bool Widescreen { get; set; } = true;

    public bool Mirror { get; set; }

    public int? Tilt { get; set; }

    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KinectWindowsWebCam");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.ini");

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        string[] lines;
        try
        {
            if (!File.Exists(FilePath))
            {
                return settings;
            }

            lines = File.ReadAllLines(FilePath);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            return settings;
        }

        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line.Substring(0, separator).Trim().ToLowerInvariant();
            var value = line.Substring(separator + 1).Trim();
            switch (key)
            {
                case QualityKey when TryParseInt(value, out var quality):
                    settings.Quality = Math.Max(0, Math.Min(FrameConverter.QualityLevels - 1, quality));
                    break;
                case WidescreenKey when bool.TryParse(value, out var widescreen):
                    settings.Widescreen = widescreen;
                    break;
                case MirrorKey when bool.TryParse(value, out var mirror):
                    settings.Mirror = mirror;
                    break;
                case TiltKey when TryParseInt(value, out var tilt):
                    settings.Tilt = TiltScheduler.Clamp(tilt);
                    break;
            }
        }

        return settings;
    }

    public static void Delete()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
        catch (Exception exception) when (IsFileError(exception))
        {
        }
    }

    public bool Save()
    {
        var lines = new List<string>
        {
            QualityKey + "=" + Quality.ToString(CultureInfo.InvariantCulture),
            WidescreenKey + "=" + (Widescreen ? "true" : "false"),
            MirrorKey + "=" + (Mirror ? "true" : "false")
        };

        if (Tilt.HasValue)
        {
            lines.Add(TiltKey + "=" + Tilt.Value.ToString(CultureInfo.InvariantCulture));
        }

        var temporaryPath = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllLines(temporaryPath, lines);
            if (File.Exists(FilePath))
            {
                File.Replace(temporaryPath, FilePath, null);
            }
            else
            {
                File.Move(temporaryPath, FilePath);
            }

            return true;
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            return false;
        }
    }

    private static bool TryParseInt(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsFileError(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
    }
}
