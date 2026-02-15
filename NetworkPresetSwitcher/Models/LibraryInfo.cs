using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NetworkPresetSwitcher.Infrastructure;

namespace NetworkPresetSwitcher.Models;

public sealed class LibraryInfo
{
    public LibraryInfo(string name, string version, string license, string description = "", string url = "")
    {
        Name = name;
        Version = version;
        License = license;
        Description = description;
        Url = url;
    }

    public string Name { get; }
    public string Version { get; }
    public string License { get; }
    public string Description { get; }
    public string Url { get; }
}

public static class LibraryCatalog
{
    public static IReadOnlyList<LibraryInfo> GetAll()
    {
        var appName = GetApplicationName();
        var appVersion = GetApplicationVersion();

        return new List<LibraryInfo>
        {
            new(
                appName,
                appVersion,
                "MIT License",
                Localization.T("Library.App.Description"),
                string.Empty
            ),
            new(
                "System.Text.Json",
                "8.0.5",
                "MIT License",
                Localization.T("Library.SystemTextJson.Description"),
                "https://github.com/dotnet/runtime"
            ),
            new(
                "System.Management",
                "8.0.0",
                "MIT License",
                Localization.T("Library.SystemManagement.Description"),
                "https://github.com/dotnet/runtime"
            ),
            new(
                "System.Text.Encoding.CodePages",
                "8.0.0",
                "MIT License",
                Localization.T("Library.SystemTextEncodingCodePages.Description"),
                "https://github.com/dotnet/runtime"
            ),
            new(
                ".NET 8",
                "8.x",
                "MIT License",
                Localization.T("Library.DotNet.Description"),
                "https://github.com/dotnet/runtime"
            ),
            new(
                "WPF",
                "8.x",
                "MIT License",
                Localization.T("Library.Wpf.Description"),
                "https://github.com/dotnet/wpf"
            )
        };
    }

    public static string GetApplicationName()
    {
        return "Network Preset Switcher";
    }

    public static string GetApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex >= 0)
            {
                infoVersion = infoVersion[..plusIndex];
            }

            return infoVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    }

    public static string GetRuntimeVersion()
    {
        return RuntimeInformation.FrameworkDescription;
    }

    public static string GetLicenseText(LibraryInfo library)
    {
        if (string.Equals(library.Name, GetApplicationName(), StringComparison.OrdinalIgnoreCase))
        {
            return GetMitLicenseText(library.Name, "fa-yoshinobu");
        }

        return GetMitLicenseText(library.Name, "Microsoft Corporation");
    }

    private static string GetMitLicenseText(string libraryName, string copyright)
    {
        return $@"{libraryName} - MIT License

Copyright (c) {DateTime.Now.Year} {copyright}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";
    }
}

