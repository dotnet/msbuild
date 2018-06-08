// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class Util
    {
        internal static readonly string Schema = Environment.GetEnvironmentVariable("VSPSCHEMA");
        internal static readonly bool logging = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSPLOG"));
        internal static readonly string logPath = GetLogPath();
        private static readonly char[] s_fileNameInvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        private static StreamWriter s_logFileWriter;
        // Major, Minor, Build and Revision of CLR v2.0
        private static readonly int[] s_clrVersion2 = { 2, 0, 50727, 0 };

        #region " Platform <-> ProcessorArchitecture mapping "
        // Note: These two arrays are parallel and must correspond to one another.
        private static readonly string[] s_platforms =
        {
            "AnyCPU",
            "x86",
            "x64",
            "Itanium",
            "arm"
        };
        private static readonly string[] s_processorArchitectures =
        {
            "msil",
            "x86",
            "amd64",
            "ia64",
            "arm"
        };
        #endregion

        public static string ByteArrayToHex(Byte[] a)
        {
            if (a == null)
                return null;
            StringBuilder s = new StringBuilder(a.Length);
            foreach (Byte b in a)
                s.Append(b.ToString("X02", CultureInfo.InvariantCulture));
            return s.ToString();
        }

        public static string ByteArrayToString(Byte[] a)
        {
            if (a == null)
                return null;
            StringBuilder s = new StringBuilder(a.Length);
            foreach (Byte b in a)
                s.Append(Convert.ToChar(b));
            return s.ToString();
        }

        public static int CopyStream(Stream input, Stream output)
        {
            const int bufferSize = 0x4000;
            byte[] buffer = new byte[bufferSize];
            int bytesCopied = 0;
            int bytesRead;
            do
            {
                bytesRead = input.Read(buffer, 0, bufferSize);
                output.Write(buffer, 0, bytesRead);
                bytesCopied += bytesRead;
            } while (bytesRead > 0);
            output.Flush();
            input.Position = 0;
            output.Position = 0;
            return bytesCopied;
        }

        public static string FilterNonprintableChars(string value)
        {
            StringBuilder sb = new StringBuilder(value);
            int i = 0;
            while (i < sb.Length)
                if (sb[i] < ' ')
                    sb.Remove(i, 1);
                else
                    ++i;
            return sb.ToString();
        }

        public static string GetAssemblyPath()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static string GetClrVersion()
        {
            Version v = Environment.Version;
            v = new Version(v.Major, v.Minor, v.Build, 0);
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
                return GetClrVersion();

            Version clrVersion;
            Version currentVersion = Environment.Version;
            Version frameworkVersion = GetTargetFrameworkVersion(targetFrameworkVersion);

            // for FX 4.0 or above use the current version.
            if (frameworkVersion != null && (frameworkVersion.Major >= currentVersion.Major))
            {
                clrVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build, 0);
            }
            else
            {
                clrVersion = new Version(s_clrVersion2[0], s_clrVersion2[1], s_clrVersion2[2], s_clrVersion2[3]);
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
                if (targetFramework.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    Version.TryParse(targetFramework.Substring(1), out frameworkVersion);
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
            StreamReader r = new StreamReader(s);
            return r.ReadToEnd();
        }

        public static Stream GetEmbeddedResourceStream(string name)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            Stream s = a.GetManifestResourceStream(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", typeof(Util).Namespace, name));
            Debug.Assert(s != null, String.Format(CultureInfo.CurrentCulture, "EmbeddedResource '{0}' not found", name));
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

        [SuppressMessage("Microsoft.Security.Cryptography", "CA5354: SHA1CannotBeUsed.", Justification = ".NET 4.0 and earlier versions cannot parse SHA-2.")]
        private static void GetFileInfoImpl(string path, string targetFrameWorkVersion, out string hash, out long length)
        {
            FileInfo fi = new FileInfo(path);
            length = fi.Length;

            Stream s = null;
            try
            {
                s = fi.OpenRead();
                HashAlgorithm hashAlg;

                if (string.IsNullOrEmpty(targetFrameWorkVersion) || CompareFrameworkVersions(targetFrameWorkVersion, Constants.TargetFrameworkVersion40) <= 0)
                {
                    hashAlg = new SHA1CryptoServiceProvider();
                }
                else
                {
                    hashAlg = new SHA256CryptoServiceProvider();
                }
                byte[] hashBytes = hashAlg.ComputeHash(s);
                hash = Convert.ToBase64String(hashBytes);
            }
            finally
            {
                s?.Close();
            }
        }

        private static string GetLogPath()
        {
            if (!logging) return null;
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\VisualStudio\8.0\VSPLOG");
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);
            return logPath;
        }

        public static string GetRegisteredOrganization()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", false);
            if (key != null)
            {
                string org = (string)key.GetValue("RegisteredOrganization");
                org = org?.Trim();
                if (!String.IsNullOrEmpty(org))
                    return org;
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
                return true; // "neutral" is valid in a manifest but not in CultureInfo class
            if (String.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
                return true; // "*" is same as "neutral"
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
                return false;
            if (octets >= 2 && version.Minor < 0)
                return false;
            if (octets >= 3 && version.Build < 0)
                return false;
            if (octets >= 4 && version.Revision < 0)
                return false;

            return true;
        }

        internal static bool IsValidFrameworkVersion(string value)
        {
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return IsValidVersion(value.Substring(1), 2);
            return IsValidVersion(value, 2);
        }

        public static string PlatformToProcessorArchitecture(string platform)
        {
            for (int i = 0; i < s_platforms.Length; ++i)
                if (String.Compare(platform, s_platforms[i], StringComparison.OrdinalIgnoreCase) == 0)
                    return s_processorArchitectures[i];
            return null;
        }

        private static ITaskItem[] RemoveDuplicateItems(ITaskItem[] items)
        {
            if (items == null)
                return null;
            if (items.Length <= 1)
                return items;
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
                Array.Sort(outputItems, s_itemComparer);
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
            StreamReader r = new StreamReader(s);
            WriteFile(path, r.ReadToEnd());
        }

        public static void WriteLog(string text)
        {
            if (!logging)
                return;
            if (s_logFileWriter == null)
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
            s_logFileWriter.WriteLine(text);
            s_logFileWriter.Flush();
        }

        public static void WriteLogFile(string filename, Stream s)
        {
            if (!logging)
                return;
            string path = Path.Combine(logPath, filename);
            StreamReader r = new StreamReader(s);
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
                return;
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
                return;
            WriteLogFile(filename, element.OuterXml);
        }

        public static string WriteTempFile(Stream s)
        {
            // May throw IO-related exceptions
            string path = FileUtilities.GetTemporaryFile();

            WriteFile(path, s);
            return path;
        }

        public static string WriteTempFile(string s)
        {
            // May throw IO-related exceptions
            string path = FileUtilities.GetTemporaryFile();

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
                return new Version(version.Substring(1));
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
