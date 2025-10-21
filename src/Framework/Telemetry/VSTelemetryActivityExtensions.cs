// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// Extension methods for <see cref="Activity"/>. usage in VS OpenTelemetry.
    /// </summary>
    internal static class VSTelemetryActivityExtensions
    {     

        /// <summary>
        /// Add tags to the activity from a <see cref="IActivityTelemetryDataHolder"/>.
        /// </summary>
        public static VsTelemetryActivity WithTags(this VsTelemetryActivity activity, string key, IActivityTelemetryDataHolder? dataHolder)
        {
            if (dataHolder != null)
            {
                activity.WithTag(key, dataHolder.GetActivityProperties());
            }

            return activity;
        }

        /// <summary>
        /// Add tags to the activity from a <see cref="IActivityTelemetryDataHolder"/>.
        /// </summary>
        public static VsTelemetryActivity WithTags(this VsTelemetryActivity activity, string key, BuildTelemetry? buildTelemetry)
        {
            if (buildTelemetry != null)
            {
                TelemetryComplexProperty dataHolder = new VSBuildTelemetry(buildTelemetry).GetActivityProperties();
                if (dataHolder != null)
                {
                    activity.WithTag(key, dataHolder);
                }
            }

            return activity;
        }

        /// <summary>
        /// Add a tag to the activity from a <see cref="TelemetryItem"/>.
        /// </summary>
        public static VsTelemetryActivity WithTag(this VsTelemetryActivity activity, string key, TelemetryComplexProperty telemetryComplexProperty)
        {
            activity.AddComplexProperty($"{TelemetryConstants.PropertyPrefix}{key}", telemetryComplexProperty);

            return activity;
        }

        /// <summary>
        /// Depending on the platform, hash the value using an available mechanism.
        /// </summary>
        internal static string GetHashed(object value)
        {
            return Sha256Hasher.Hash(value.ToString() ?? "");
        }

        // https://github.com/dotnet/sdk/blob/8bd19a2390a6bba4aa80d1ac3b6c5385527cc311/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs + workaround for netstandard2.0
        private static class Sha256Hasher
        {
            /// <summary>
            /// The hashed mac address needs to be the same hashed value as produced by the other distinct sources given the same input. (e.g. VsCode)
            /// </summary>
            public static string Hash(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
#if NET
                byte[] hash = SHA256.HashData(bytes);
#if NET9_0_OR_GREATER
                return Convert.ToHexStringLower(hash);
#else
                return Convert.ToHexString(hash).ToLowerInvariant();
#endif

#else
                // Create the SHA256 object and compute the hash
                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(bytes);

                    // Convert the hash bytes to a lowercase hex string (manual loop approach)
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                    {
                        sb.AppendFormat("{0:x2}", b);
                    }

                    return sb.ToString();
                }
#endif
            }

            public static string HashWithNormalizedCasing(string text) => Hash(text.ToUpperInvariant());
        }
    }
}

#endif
