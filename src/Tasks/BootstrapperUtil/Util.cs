// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    internal static class Util
    {
        private const string BOOTSTRAPPER_REGISTRY_PATH_BASE = "Software\\Microsoft\\GenericBootstrapper\\";
        private const string BOOTSTRAPPER_WOW64_REGISTRY_PATH_BASE = "Software\\Wow6432Node\\Microsoft\\GenericBootstrapper\\";

        private const string BOOTSTRAPPER_REGISTRY_PATH_VERSION_VS2010 = "4.0";

        private const string REGISTRY_DEFAULTPATH = "Path";

        private static string s_defaultPath;

        public static string AddTrailingChar(string str, char ch)
        {
            if (str.LastIndexOf(ch) == str.Length - 1)
            {
                return str;
            }
            return str + ch;
        }

        public static bool IsUncPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                var uri = new Uri(path);
                return uri.IsUnc;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        public static bool IsWebUrl(string path)
        {
            return path.StartsWith("http://", StringComparison.Ordinal) || path.StartsWith("https://", StringComparison.Ordinal);
        }

        public static CultureInfo GetCultureInfoFromString(string cultureName)
        {
            try
            {
                var ci = new CultureInfo(cultureName);
                return ci;
            }
            catch (ArgumentException)
            {
            }

            return null;
        }

        public static CultureInfo DefaultCultureInfo => System.Threading.Thread.CurrentThread.CurrentUICulture;

        // This is the 4.0 property and will always point to the Dev10 registry key so that we don't break backwards compatibility.
        // Applications relying on 4.5 will need to use the new method that is introduced in 4.5.
        public static string DefaultPath
        {
            get
            {
                if (String.IsNullOrEmpty(s_defaultPath))
                {
                    s_defaultPath = ReadRegistryString(Registry.LocalMachine, String.Concat(BOOTSTRAPPER_REGISTRY_PATH_BASE, BOOTSTRAPPER_REGISTRY_PATH_VERSION_VS2010), REGISTRY_DEFAULTPATH);
                    if (!String.IsNullOrEmpty(s_defaultPath))
                    {
                        return s_defaultPath;
                    }

                    s_defaultPath = ReadRegistryString(Registry.LocalMachine, String.Concat(BOOTSTRAPPER_WOW64_REGISTRY_PATH_BASE, BOOTSTRAPPER_REGISTRY_PATH_VERSION_VS2010), REGISTRY_DEFAULTPATH);
                    if (!String.IsNullOrEmpty(s_defaultPath))
                    {
                        return s_defaultPath;
                    }

                    s_defaultPath = Directory.GetCurrentDirectory();
                }

                return s_defaultPath;
            }
        }

        // A new method in 4.5 to get the default path for bootstrapper packages.
        // This method is not going to cache the path as it could be different depending on the Visual Studio version.
        public static string GetDefaultPath(string visualStudioVersion)
        {
            // if the Visual Studio Version is not a valid string, we will fall back to using the v4.0 property.
            if (String.IsNullOrEmpty(visualStudioVersion))
            {
                return DefaultPath;
            }

            // With version 11.0 we start a direct mapping between the VS version and the registry key we use.
            // For Dev10, we use 4.0.
            // For Dev15 this will go versionless as there will be singleton MSI setting the registry which will be common for all future VS.

            int dotIndex = visualStudioVersion.IndexOf('.');
            if (dotIndex < 0)
            {
                dotIndex = visualStudioVersion.Length;
            }
            if (Int32.TryParse(visualStudioVersion.Substring(0, dotIndex), out int majorVersion) && (majorVersion < 11))
            {
                visualStudioVersion = BOOTSTRAPPER_REGISTRY_PATH_VERSION_VS2010;
            }

            string defaultPath = ReadRegistryString(Registry.LocalMachine, BOOTSTRAPPER_REGISTRY_PATH_BASE, REGISTRY_DEFAULTPATH);
            if (!String.IsNullOrEmpty(defaultPath))
            {
                return defaultPath;
            }

            defaultPath = ReadRegistryString(Registry.LocalMachine, BOOTSTRAPPER_WOW64_REGISTRY_PATH_BASE, REGISTRY_DEFAULTPATH);
            if (!String.IsNullOrEmpty(defaultPath))
            {
                return defaultPath;
            }

            defaultPath = ReadRegistryString(Registry.LocalMachine, String.Concat(BOOTSTRAPPER_REGISTRY_PATH_BASE, visualStudioVersion), REGISTRY_DEFAULTPATH);
            if (!String.IsNullOrEmpty(defaultPath))
            {
                return defaultPath;
            }

            defaultPath = ReadRegistryString(Registry.LocalMachine, String.Concat(BOOTSTRAPPER_WOW64_REGISTRY_PATH_BASE, visualStudioVersion), REGISTRY_DEFAULTPATH);
            if (!String.IsNullOrEmpty(defaultPath))
            {
                return defaultPath;
            }

            return Directory.GetCurrentDirectory();
        }

        private static string ReadRegistryString(RegistryKey key, string path, string registryValue)
        {
            RegistryKey subKey = key.OpenSubKey(path, false);

            object oValue = subKey?.GetValue(registryValue);
            if (oValue != null && subKey.GetValueKind(registryValue) == RegistryValueKind.String)
            {
                return (string)oValue;
            }

            return null;
        }
    }
}
