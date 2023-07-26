// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Utils
{
#pragma warning disable CA1416
    internal class WindowsRegistryEnvironmentPathEditor : IWindowsRegistryEnvironmentPathEditor
    {
        private static string Path = "PATH";
        public string Get(SdkEnvironmentVariableTarget currentUserBeforeEvaluation)
        {
            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(writable: false, sdkEnvironmentVariableTarget: currentUserBeforeEvaluation))
            {
                return environmentKey?.GetValue(Path, "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            }
        }

        public void Set(string value, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
        {
            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(writable: true, sdkEnvironmentVariableTarget: sdkEnvironmentVariableTarget))
            {
                environmentKey?.SetValue(Path, value, RegistryValueKind.ExpandString);
            }

            Task.Factory.StartNew(() =>
            {
                unsafe
                {
                    // send a WM_SETTINGCHANGE message to all windows
                    fixed (char* lParam = "Environment")
                    {
                        IntPtr r = SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, IntPtr.Zero, (IntPtr)lParam, 0, 1000, out IntPtr _);
                    }
                }
            });
        }

        private static RegistryKey OpenEnvironmentKeyIfExists(bool writable, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
        {
            RegistryKey baseKey;
            string keyName;

            if (sdkEnvironmentVariableTarget == SdkEnvironmentVariableTarget.CurrentUser)
            {
                baseKey = Registry.CurrentUser;
                keyName = "Environment";
            }
            else if (sdkEnvironmentVariableTarget == SdkEnvironmentVariableTarget.DotDefault)
            {
                baseKey = Registry.Users;
                keyName = ".DEFAULT\\Environment";
            }
            else
            {
                throw new ArgumentException(nameof(sdkEnvironmentVariableTarget) + " cannot be matched, the value is: " + sdkEnvironmentVariableTarget.ToString());
            }

            return baseKey.OpenSubKey(keyName, writable: writable);
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr pdwResult);

        private const int HWND_BROADCAST = 0xffff;
        private const int WM_SETTINGCHANGE = 0x001A;
    }
#pragma warning restore CA1416
}
