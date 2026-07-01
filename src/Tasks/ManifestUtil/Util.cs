// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Win32;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class Util
    {
        internal static readonly string Schema = Environment.GetEnvironmentVariable("VSPSCHEMA");
        internal static readonly bool logging = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSPLOG"));
        internal static readonly string logPath = GetLogPath();
        private static readonly char[] s_fileNameInvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        private static StreamWriter s_logFileWriter;
#if !RUNTIME_TYPE_NETCORE
        // Major, Minor, Build and Revision of CLR v2.0
        private static readonly int[] s_clrVersion2 = { 2, 0, 50727, 0 };
#else
        // Major, Minor, Build and Revision of CLR v4.0
        private static readonly int[] s_clrVersion4 = { 4, 0, 30319, 0 };
#endif

        #region " Platform <-> ProcessorArchitecture mapping "
        // Note: These two arrays are parallel and must correspond to one another.
        private static readonly string[] s_platforms =
        {
            "AnyCPU",
            "x86",
            "x64",
            "Itanium",
            "arm",
            "arm64",
        };
        private static readonly string[] s_processorArchitectures =
        {
            "msil",
            "x86",
            "amd64",
            "ia64",
            "arm",
            "arm64",
        };
        #endregion

        public static string ByteArrayToHex(Byte[] a)
        {
            if (a == null)
            {
                return null;
            }

#if NET
            return Convert.ToHexString(a);
#else
            StringBuilder s = new StringBuilder(a.Length * 2);
            foreach (Byte b in a)
            {
                s.Append(b.ToString("X02", CultureInfo.InvariantCulture));
            }

            return s.ToString();
#endif
        }

        public static void CopyStream(Stream input, Stream output)
        {
            const int bufferSize = 0x4000;
            input.CopyTo(output, bufferSize);
            input.Position = 0;
            output.Position = 0;
        }

        public static string FilterNonprintableChars(string value)
        {
            StringBuilder sb = new StringBuilder(value);
            int i = 0;
            while (i < sb.Length)
            {
                if (sb[i] < ' ')
                {
                    sb.Remove(i, 1);
                }
                else
                {
                    ++i;
                }
            }

            return sb.ToString();
        }

        public static string GetAssemblyPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static string GetClrVersion()
        {
            Version v = Environment.Version;
#if RUNTIME_TYPE_NETCORE
            // This is a version of ClickOnce .NET FX target runtime, which cannot be obtained in .NET (Core) process.
            // Set to .NET FX v4 runtime as the ony one supported for manifest generation in .NET (Core) process.
            v = new Version(s_clrVersion4[0], s_clrVersion4[1], s_clrVersion4[2], s_clrVersion4[3]);
#else
            v = new Version(v.Major, v.Minor, v.Build, 0);
#endif
            return v.ToString();
        }

        /// <summary>
        /// Return a CLRVersion from a given target framework version.
        /// </summary>
        /// <param name="targetFrameworkVersion"></param>
        /// <returns></returns>
        public static string GetClrVersion(string targetFrameworkVersion)
        {
            if (string.IsNullOrEmpty(targetFrameworkVersion))
            {
                return GetClrVersion();
            }

            Version clrVersion;
#if RUNTIME_TYPE_NETCORE
            // This is a version of ClickOnce .NET FX target runtime, which cannot be obtained in .NET (Core) process.
            // Set to .NET FX v4 runtime as the ony one supported for manifest generation in .NET (Core) process.
            Version currentVersion = new Version(s_clrVersion4[0], s_clrVersion4[1], s_clrVersion4[2], s_clrVersion4[3]);
#else
            Version currentVersion = Environment.Version;
#endif
            Version frameworkVersion = GetTargetFrameworkVersion(targetFrameworkVersion);

            // for FX 4.0 or above use the current version.
            if (frameworkVersion != null && (frameworkVersion.Major >= currentVersion.Major))
            {
                clrVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build, 0);
            }
            else
            {
#if RUNTIME_TYPE_NETCORE
                // Set to .NET FX v4 runtime as the ony one supported for manifest generation in .NET (Core) process.
                clrVersion = new Version(s_clrVersion4[0], s_clrVersion4[1], s_clrVersion4[2], s_clrVersion4[3]);
#else
                clrVersion = new Version(s_clrVersion2[0], s_clrVersion2[1], s_clrVersion2[2], s_clrVersion2[3]);
#endif
            }
            return clrVersion.ToString();
        }

        /// <summary>
        /// Gets a Version object corresponding to the given target framework version string.
        /// </summary>
        public static Version GetTargetFrameworkVersion(string targetFramework)
        {
            Version frameworkVersion = null;
            if (!String.IsNullOrEmpty(targetFramework))
            {
                if (targetFramework[0] is 'v' or 'V')
                {
                    Version.TryParse(
#if NET
                        targetFramework.AsSpan(1),
#else
                        targetFramework.Substring(1),
#endif
                        out frameworkVersion);
                }
                else
                {
                    Version.TryParse(targetFramework, out frameworkVersion);
                }
            }
            return frameworkVersion;
        }

        public static string GetEmbeddedResourceString(string name)
        {
            Stream s = GetEmbeddedResourceStream(name);
            using StreamReader r = new StreamReader(s);
            return r.ReadToEnd();
        }

        public static Stream GetEmbeddedResourceStream(string name)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            Stream s = a.GetManifestResourceStream($"{typeof(Util).Namespace}.{name}");
            Debug.Assert(s != null, $"EmbeddedResource '{name}' not found");
            return s;
        }

        public static void GetFileInfo(string path, out string hash, out long length)
        {
            GetFileInfoImpl(path, null, out hash, out length);
        }

        public static void GetFileInfo(string path, string targetFrameworkVersion, out string hash, out long length)
        {
            GetFileInfoImpl(path, targetFrameworkVersion, out hash, out length);
        }

        [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = ".NET 4.0 and earlier versions cannot parse SHA-2.")]
        private static void GetFileInfoImpl(string path, string targetFrameWorkVersion, out string hash, out long length)
        {
            FileInfo fi = new FileInfo(path);
            length = fi.Length;

            Stream s = null;
            HashAlgorithm hashAlg = null;
            try
            {
                s = fi.OpenRead();

                if (string.IsNullOrEmpty(targetFrameWorkVersion) || CompareFrameworkVersions(targetFrameWorkVersion, Constants.TargetFrameworkVersion40) <= 0)
                {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                    // codeql[cs/weak-crypto] .NET 4.0 and earlier versions cannot parse SHA-2. Newer Frameworks use SHA256. https://devdiv.visualstudio.com/DevDiv/_workitems/edit/139025
                    hashAlg = SHA1.Create(
#if FEATURE_CRYPTOGRAPHIC_FACTORY_ALGORITHM_NAMES
                        "System.Security.Cryptography.SHA1CryptoServiceProvider"
#endif
                        );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                }
                else
                {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                    hashAlg = SHA256.Create(
#if FEATURE_CRYPTOGRAPHIC_FACTORY_ALGORITHM_NAMES
                        "System.Security.Cryptography.SHA256CryptoServiceProvider"
#endif
                        );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                }
                byte[] hashBytes = hashAlg.ComputeHash(s);
                hash = Convert.ToBase64String(hashBytes);
            }
            finally
            {
                s?.Close();
                hashAlg?.Dispose();
            }
        }

        private static string GetLogPath()
        {
            if (!logging)
            {
                return null;
            }

            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\VisualStudio\8.0\VSPLOG");
            if (!FileSystems.Default.DirectoryExists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            return logPath;
        }

        [SupportedOSPlatform("windows")]
        public static string GetRegisteredOrganization()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", false);
            if (key != null)
            {
                string org = (string)key.GetValue("RegisteredOrganization");
                org = org?.Trim();
                if (!String.IsNullOrEmpty(org))
                {
                    return org;
                }
            }
            return null;
        }

        public static bool IsValidAssemblyName(string value)
        {
            return IsValidFileName(value);
        }

        public static bool IsValidCulture(string value)
        {
            if (String.Equals(value, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                return true; // "neutral" is valid in a manifest but not in CultureInfo class
            }

            if (String.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
            {
                return true; // "*" is same as "neutral"
            }

            CultureInfo culture;
            try
            {
                culture = new CultureInfo(value);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        public static bool IsValidFileName(string value)
        {
            return value.IndexOfAny(s_fileNameInvalidChars) < 0;
        }

        public static bool IsValidVersion(string value, int octets)
        {
            Version version;
            try
            {
                version = new Version(value);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }

            if (octets >= 1 && version.Major < 0)
            {
                return false;
            }

            if (octets >= 2 && version.Minor < 0)
            {
                return false;
            }

            if (octets >= 3 && version.Build < 0)
            {
                return false;
            }

            if (octets >= 4 && version.Revision < 0)
            {
                return false;
            }

            return true;
        }

        internal static bool IsValidFrameworkVersion(string value)
        {
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return IsValidVersion(value.Substring(1), 2);
            }

            return IsValidVersion(value, 2);
        }

        public static string PlatformToProcessorArchitecture(string platform)
        {
            for (int i = 0; i < s_platforms.Length; ++i)
            {
                if (String.Equals(platform, s_platforms[i], StringComparison.OrdinalIgnoreCase))
                {
                    return s_processorArchitectures[i];
                }
            }

            return null;
        }

        private static ITaskItem[] RemoveDuplicateItems(ITaskItem[] items)
        {
            if (items == null)
            {
                return null;
            }

            if (items.Length <= 1)
            {
                return items;
            }

            var list = new Dictionary<string, ITaskItem>();
            foreach (ITaskItem item in items)
            {
                if (String.IsNullOrEmpty(item.ItemSpec))
                {
                    continue;
                }

                string key;
                var id = new AssemblyIdentity(item.ItemSpec);
                if (id.IsStrongName)
                {
                    key = id.GetFullName(AssemblyIdentity.FullNameFlags.All);
                }
                else
                {
                    key = Path.GetFullPath(item.ItemSpec).ToUpperInvariant();
                }

                if (!list.ContainsKey(key))
                {
                    list.Add(key, item);
                }
            }

            return list.Values.ToArray();
        }

        public static ITaskItem[] SortItems(ITaskItem[] items)
        {
            ITaskItem[] outputItems = RemoveDuplicateItems(items);
            if (outputItems != null)
            {
                Array.Sort(outputItems, s_itemComparer);
            }

            return outputItems;
        }

        public static void WriteFile(string path, string s)
        {
            using (StreamWriter w = new StreamWriter(path))
            {
                w.Write(s);
            }
        }

        public static void WriteFile(string path, Stream s)
        {
            using StreamReader r = new StreamReader(s);
            WriteFile(path, r.ReadToEnd());
        }

        public static void WriteLog(string text)
        {
            if (!logging)
            {
                return;
            }

            if (s_logFileWriter == null)
            {
                try
                {
                    s_logFileWriter = new StreamWriter(Path.Combine(logPath, "Microsoft.Build.Tasks.log"), false);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                catch (ArgumentException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }
                catch (SecurityException)
                {
                    return;
                }
            }

            s_logFileWriter.WriteLine(text);
            s_logFileWriter.Flush();
        }

        public static void WriteLogFile(string filename, Stream s)
        {
            if (!logging)
            {
                return;
            }

            string path = Path.Combine(logPath, filename);
            using var r = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
            string text = r.ReadToEnd();
            try
            {
                WriteFile(path, text);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (SecurityException)
            {
            }

            s.Position = 0;
        }

        public static void WriteLogFile(string filename, string s)
        {
            if (!logging)
            {
                return;
            }

            string path = Path.Combine(logPath, filename);
            try
            {
                WriteFile(path, s);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            catch (SecurityException)
            {
                return;
            }
        }

        public static void WriteLogFile(string filename, System.Xml.XmlElement element)
        {
            if (!logging)
            {
                return;
            }

            WriteLogFile(filename, element.OuterXml);
        }

        public static string WriteTempFile(Stream s)
        {
            // May throw IO-related exceptions
            string path = FileUtilities.GetTemporaryFileName();

            WriteFile(path, s);
            return path;
        }

        public static string WriteTempFile(string s)
        {
            // May throw IO-related exceptions
            string path = FileUtilities.GetTemporaryFileName();

            WriteFile(path, s);
            return path;
        }

        #region ItemComparer
        private static readonly ItemComparer s_itemComparer = new ItemComparer();
        private class ItemComparer : IComparer
        {
            int IComparer.Compare(object obj1, object obj2)
            {
                if (obj1 == null || obj2 == null)
                {
                    Debug.Fail("Comparing null objects");
                    return 0;
                }
                if (!(obj1 is ITaskItem) || !(obj2 is ITaskItem))
                {
                    Debug.Fail("Comparing objects that are not ITaskItem");
                    return 0;
                }
                ITaskItem item1 = obj1 as ITaskItem;
                ITaskItem item2 = obj2 as ITaskItem;
                if (item1.ItemSpec == null || item2.ItemSpec == null)
                {
                    Debug.Fail("Objects do not have a ItemSpec");
                    return 0;
                }
                return String.Compare(item1.ItemSpec, item2.ItemSpec, StringComparison.Ordinal);
            }
        }
        #endregion

        public static Version ConvertFrameworkVersionToString(string version)
        {
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return Version.Parse(
#if NET
                    version.AsSpan(1));
#else
                    version.Substring(1));
#endif
            }

            return new Version(version);
        }

        public static int CompareFrameworkVersions(string versionA, string versionB)
        {
            Version version1 = ConvertFrameworkVersionToString(versionA);
            Version version2 = ConvertFrameworkVersionToString(versionB);
            return version1.CompareTo(version2);
        }
    }
}
