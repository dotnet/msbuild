// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a single &lt;bindingRedirect&gt; from the app.config file.
    /// </summary>
    internal sealed class BindingRedirect
    {
        /// <summary>
        /// The low end of the old version range.
        /// </summary>
        internal Version OldVersionLow { set; get; }

        /// <summary>
        /// The high end of the old version range.
        /// </summary>
        internal Version OldVersionHigh { set; get; }

        /// <summary>
        /// The new version number.
        /// </summary>
        internal Version NewVersion { set; get; }

        /// <summary>
        /// The reader is positioned on a &lt;bindingRedirect&gt; element--read it.
        /// </summary>
        internal void Read(XmlReader reader)
        {
            string oldVersion = reader.GetAttribute("oldVersion");

            // A badly formed assembly name.
            ErrorUtilities.VerifyThrowArgument(!String.IsNullOrEmpty(oldVersion), "AppConfig.BindingRedirectMissingOldVersion");

            int dashPosition = oldVersion.IndexOf('-');

            try
            {
                if (dashPosition >= 0)
                {
                    // This is a version range.
#if NET
                    OldVersionLow = Version.Parse(oldVersion.AsSpan(0, dashPosition));
                    OldVersionHigh = Version.Parse(oldVersion.AsSpan(dashPosition + 1));
#else
                    OldVersionLow = Version.Parse(oldVersion.Substring(0, dashPosition));
                    OldVersionHigh = Version.Parse(oldVersion.Substring(dashPosition + 1));
#endif
                }
                else
                {
                    // This is a single version.
                    OldVersionLow = OldVersionHigh = new Version(oldVersion);
                }
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                ErrorUtilities.ThrowArgument(e, "AppConfig.InvalidOldVersionAttribute", e.Message);
            }

            string newVersionAttribute = reader.GetAttribute("newVersion");

            // A badly formed assembly name.
            ErrorUtilities.VerifyThrowArgument(!String.IsNullOrEmpty(newVersionAttribute), "AppConfig.BindingRedirectMissingNewVersion");

            try
            {
                NewVersion = new Version(newVersionAttribute);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                ErrorUtilities.ThrowArgument(e, "AppConfig.InvalidNewVersionAttribute", e.Message);
            }
        }
    }
}
