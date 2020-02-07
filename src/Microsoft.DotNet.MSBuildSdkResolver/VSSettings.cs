// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

#if NETFRAMEWORK
using Microsoft.VisualStudio.Setup.Configuration;
#endif

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal sealed class VSSettings
    {
        private readonly object _lock = new object();
        private readonly string _settingsFilePath;
        private readonly bool _disallowPrereleaseByDefault;
        private FileInfo _settingsFile;
        private bool _disallowPrerelease;

        // In the product, this singleton is used. It must be safe to use in parallel on multiple threads.
        // In tests, mock instances can be created with the test constructor below.
        public static readonly VSSettings Ambient = new VSSettings();

        private VSSettings()
        {
#if NETFRAMEWORK
            if (!Interop.RunningOnWindows)
            {
                return;
            }

            string instanceId;
            string installationVersion;
            bool isPrerelease;

            try
            {
                var configuration = new SetupConfiguration();
                var instance = configuration.GetInstanceForCurrentProcess();

                instanceId = instance.GetInstanceId();
                installationVersion = instance.GetInstallationVersion();
                isPrerelease = ((ISetupInstanceCatalog)instance).IsPrerelease();
            }
            catch (COMException)
            {
                return;
            }

            var version = Version.Parse(installationVersion);

            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "VisualStudio",
                version.Major + ".0_" + instanceId,
                "sdk.txt");

            _disallowPrereleaseByDefault = !isPrerelease;
            _disallowPrerelease = _disallowPrereleaseByDefault;
#endif
        }

        // Test constructor
        public VSSettings(string settingsFilePath, bool disallowPrereleaseByDefault)
        {
            _settingsFilePath = settingsFilePath;
            _disallowPrereleaseByDefault = disallowPrereleaseByDefault;
            _disallowPrerelease = _disallowPrereleaseByDefault;
        }

        public bool DisallowPrerelease()
        {
            if (_settingsFilePath != null)
            {
                Refresh();
            }

            return _disallowPrerelease;
        }

        private void Refresh()
        {
            Debug.Assert(_settingsFilePath != null);

            var file = new FileInfo(_settingsFilePath);

            // NB: All calls to Exists and LastWriteTimeUtc below will not hit the disk
            //     They will return data obtained during Refresh() here.
            file.Refresh(); 

            lock (_lock)
            {
                // File does not exist -> use default.
                if (!file.Exists)
                {
                    _disallowPrerelease = _disallowPrereleaseByDefault;
                    _settingsFile = file;
                    return;
                }

                // File has not changed -> reuse prior read.
                if (_settingsFile?.Exists == true && file.LastWriteTimeUtc <= _settingsFile.LastWriteTimeUtc)
                {
                    return;
                }

                // File has changed -> read from disk
                // If we encounter an I/O exception, assume writer is in the process of updating file,
                // ignore the exception, and use stale settings until the next resolution.
                try
                {
                    ReadFromDisk();
                    _settingsFile = file;
                    return;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private void ReadFromDisk()
        {
            using (var reader = new StreamReader(_settingsFilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int indexOfEquals = line.IndexOf('=');
                    if (indexOfEquals < 0 || indexOfEquals == (line.Length - 1))
                    {
                        continue;
                    }

                    string key = line.Substring(0, indexOfEquals).Trim();
                    string value = line.Substring(indexOfEquals + 1).Trim();

                    if (key.Equals("UsePreviews", StringComparison.OrdinalIgnoreCase)
                        && bool.TryParse(value, out bool usePreviews))
                    {
                        _disallowPrerelease = !usePreviews;
                        return;
                    }
                }
            }

            // File does not have UsePreviews entry -> use default
            _disallowPrerelease = _disallowPrereleaseByDefault;
        }
    }
}

