// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class DangerousFileDetectorTests : SdkTest
    {
        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        public DangerousFileDetectorTests(ITestOutputHelper log) : base(log)
        {
        }

#if NETCOREAPP
        [SupportedOSPlatform("windows")]
#endif
        [WindowsOnlyFact]
        public void ItShouldDetectFileWithMarkOfTheWeb()
        {
            var testFile = Path.Combine(_testAssetsManager.CreateTestDirectory().Path, Path.GetRandomFileName());
            
            File.WriteAllText(testFile, string.Empty);
            AlternateStream.WriteAlternateStream(
                testFile,
                "Zone.Identifier",
                "[ZoneTransfer]\r\nZoneId=3\r\nReferrerUrl=C:\\Users\\test.zip\r\n");

            bool isTestFileDangerous = new DangerousFileDetector().IsDangerous(testFile);
            if (!HasInternetSecurityManagerNativeApi())
            {
                isTestFileDangerous.Should().BeFalse("Locked down version of Windows does not have IE to download files");
            }
            else
            {
                isTestFileDangerous.Should().BeTrue();
            }
        }

        [Fact]
        public void WhenThereIsNoFileItReturnsFalse()
        {
            var testFile = Path.Combine(_testAssetsManager.CreateTestDirectory().Path, Path.GetRandomFileName());

            new DangerousFileDetector().IsDangerous(testFile).Should().BeFalse();
        }

        [UnixOnlyFact]
        public void WhenRunOnNonWindowsReturnFalse()
        {
            var testFile = Path.Combine(_testAssetsManager.CreateTestDirectory().Path, Path.GetRandomFileName());
            File.WriteAllText(testFile, string.Empty);

            new DangerousFileDetector().IsDangerous(testFile).Should().BeFalse();
        }

#if NETCOREAPP
        [SupportedOSPlatform("windows")]
#endif
        private static bool HasInternetSecurityManagerNativeApi()
        {
            try
            {
                string CLSID_InternetSecurityManager = "7b8a2d94-0ac9-11d1-896c-00c04fb6bfc4";

                Type iismType = Type.GetTypeFromCLSID(new Guid(CLSID_InternetSecurityManager));
                var internetSecurityManager = (IInternetSecurityManager)Activator.CreateInstance(iismType);
                return true;
            }
            catch (COMException ex) when (ex.ErrorCode == REGDB_E_CLASSNOTREG)
            {
                // When the COM is missing(Class not registered error), it is in a locked down
                // version like Nano Server
                return false;
            }
        }

        private static class AlternateStream
        {
            private const uint GenericWrite = 0x40000000;

            public static void WriteAlternateStream(string filePath, string altStreamName, string content)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException("message", nameof(filePath));
                }

                if (string.IsNullOrWhiteSpace(altStreamName))
                {
                    throw new ArgumentException("message", nameof(altStreamName));
                }

                if (content == null)
                {
                    throw new ArgumentNullException(nameof(content));
                }

                string altStream = filePath + ":" + altStreamName;

                SafeFileHandle fileHandle
                    = CreateFile(
                        filename: altStream,
                        desiredAccess: GenericWrite,
                        shareMode: 0,
                        attributes: IntPtr.Zero,
                        creationDisposition: (uint)FileMode.CreateNew,
                        flagsAndAttributes: 0,
                        templateFile: IntPtr.Zero);

                if (!fileHandle.IsInvalid)
                {
                    using (var streamWriter = new StreamWriter(new FileStream(fileHandle, FileAccess.Write)))
                    {
                        streamWriter.WriteLine(content);
                        streamWriter.Flush();
                    }
                }
                else
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern SafeFileHandle CreateFile(
                string filename,
                uint desiredAccess,
                uint shareMode,
                IntPtr attributes,
                uint creationDisposition,
                uint flagsAndAttributes,
                IntPtr templateFile);
        }
    }
}
