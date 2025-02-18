// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class PathUtil
    {
        public static string CanonicalizePath(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                path = path.TrimEnd(Path.DirectorySeparatorChar);
            }
            return path;
        }

        public static string CanonicalizeUrl(string url)
        {
            Uri uri = new Uri(url);
            return uri.AbsoluteUri;
        }

        // REVIEW: Can we use System.Uri segments instead
        public static string[] GetPathSegments(string path)
        {
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Split(MSBuildConstants.DirectorySeparatorChar);
        }

        // Resolves the path, and if path is a url also canonicalizes it.
        public static string Format(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }

            string resolvedPath = Resolve(path);
            Uri u = new Uri(resolvedPath);
            //
            // GB18030: Uri class does not correctly encode chars in the PUA range for implicit 
            // file paths (paths without explicit scheme):
            // https://github.com/dotnet/runtime/issues/89538
            // Workaround is to use UriBuilder with the file scheme specified explicitly to 
            // correctly encode the PUA chars.
            //
            if (Uri.UriSchemeFile.Equals(u.Scheme, StringComparison.OrdinalIgnoreCase) &&
                !IsAsciiString(resolvedPath))
            {
                UriBuilder builder = new UriBuilder()
                {
                    Scheme = Uri.UriSchemeFile,
                    Host = string.Empty,
                    Path = resolvedPath,
                };
                u = builder.Uri;
            }
            return u.AbsoluteUri;
        }

        public static bool IsAssembly(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (String.Equals(Path.GetExtension(path), ".application", StringComparison.Ordinal))
            {
                return true;
            }

            if (String.Equals(Path.GetExtension(path), ".manifest", StringComparison.Ordinal))
            {
                return true;
            }

            if (!IsProgramFile(path))
            {
                return false; // optimization, don't want to sniff every every kind of file -- just dll's or exe's
            }

            if (IsManagedAssembly(path))
            {
                return true;
            }

            if (IsNativeAssembly(path))
            {
                return true;
            }

            return false;
        }

        // This function must be kept in sync with \dd\vb\publish\design\baseprovider\pathutil.vb
        public static bool IsDataFile(string path)
        {
            return path.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ldf", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                ;
        }

        public static bool IsEqualPath(string path1, string path2)
        {
            return String.Compare(CanonicalizePath(path1), CanonicalizePath(path2), true, System.Globalization.CultureInfo.CurrentCulture) == 0;
        }

        public static bool IsLocalPath(string path)
        {
            Uri u = new Uri(path, UriKind.RelativeOrAbsolute);
            if (!u.IsAbsoluteUri)
            {
                return true;
            }

            return String.IsNullOrEmpty(u.Host);
        }

        public static bool IsManagedAssembly(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            using (MetadataReader r = MetadataReader.Create(path))
            {
                return r != null;
            }
        }

        public static bool IsNativeAssembly(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (String.Equals(Path.GetExtension(path), ".manifest", StringComparison.Ordinal))
            {
                return true;
            }

            return EmbeddedManifestReader.Read(path) != null;
        }

        public static bool IsPEFile(string path)
        {
            byte[] buffer = new byte[2];
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
#pragma warning disable CA2022 // Avoid inexact read with 'Stream.Read'
                s.Read(buffer, 0, 2);
#pragma warning restore CA2022 // Avoid inexact read with 'Stream.Read'
            }

            // if first two bytes are "MZ" then we're looking at an .exe or a .dll not a .manifest
            return (buffer[0] == 0x4D) && (buffer[1] == 0x5A);
        }

        public static bool IsProgramFile(string path)
        {
            return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ;
        }

        public static bool IsUncPath(string path)
        {
            Uri u;
            if (!Uri.TryCreate(path, UriKind.Absolute, out u) || u == null)
            {
                return false;
            }

            return u.IsUnc;
        }

        public static bool IsUrl(string path)
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out Uri u) || u == null)
            {
                return false;
            }

            return !u.IsUnc && !String.IsNullOrEmpty(u.Host);
        }

        // If path is a url and starts with "localhost", resolves to machine name.
        // If path is a relative path, resolves to a full path.
        public static string Resolve(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }

            if (IsUncPath(path))
            {
                return path; // if it's UNC then do nothing
            }

            if (IsUrl(path)) // if it's a URL then need to check for "localhost"...
            {
                // Replace "localhost" with the actual machine name...
                const string localHost = "localhost";
                var u = new Uri(path);
                if (String.Equals(u.Host, localHost, StringComparison.OrdinalIgnoreCase))
                {
                    // Unfortunately Uri.Host is read-only, so we need to reconstruct it manually...
                    int i = path.IndexOf(localHost, StringComparison.OrdinalIgnoreCase);
                    return i >= 0 ?
#if NET
                        $"{path.AsSpan(0, i)}{Environment.MachineName.ToLowerInvariant()}{path.AsSpan(i + localHost.Length)}" :
#else
                        $"{path.Substring(0, i)}{Environment.MachineName.ToLowerInvariant()}{path.Substring(i + localHost.Length)}" :
#endif
                        path;
                }
                return path;
            }

            // if not unc or url then it must be a local disk path...
            return Path.GetFullPath(path); // make sure it's a full path
        }

        private static bool IsAsciiString(string str) =>
#if NET
            Ascii.IsValid(str);
#else
            str.All(c => c <= 127);
#endif
    }
}
