using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WinAPI;

public sealed class MacOSProcessHelper : INativeProcess
{
    public string GetExecutablePath(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(fileName))
            {
                var path = Path.GetDirectoryName(fileName);
                return string.IsNullOrEmpty(path) ? string.Empty : path;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public Version GetVersion(Process process, string executablePath)
    {
        try
        {
            string? appBundlePath = FindAppBundle(executablePath);
            if (appBundlePath == null)
                return new Version();

            string plistPath = Path.Combine(appBundlePath, "Contents", "Info.plist");
            if (!File.Exists(plistPath))
                return new Version();

            return ParseVersionFromPlist(plistPath);
        }
        catch
        {
            return new Version();
        }
    }

    private static string? FindAppBundle(string executablePath)
    {
        DirectoryInfo? dir = new DirectoryInfo(executablePath).Parent;
        while (dir != null)
        {
            if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static Version ParseVersionFromPlist(string plistPath)
    {
        var doc = XDocument.Load(plistPath);
        var dict = doc.Root?.Element("dict");
        if (dict == null)
            return new Version();

        var keys = dict.Elements("key").ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].Value == "CFBundleVersion")
            {
                var versionString = keys[i].ElementsAfterSelf("string").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(versionString) && 
                    Version.TryParse(versionString, out Version? v))
                {
                    return v;
                }
            }
        }

        return new Version();
    }
}
