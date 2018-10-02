// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using Microsoft.Build.Shared;

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
                if (dashPosition != -1)
                {
                    // This is a version range.
                    OldVersionLow = new Version(oldVersion.Substring(0, dashPosition));
                    OldVersionHigh = new Version(oldVersion.Substring(dashPosition + 1));
                }
                else
                {
                    // This is a single version.
                    OldVersionLow = new Version(oldVersion);
                    OldVersionHigh = new Version(oldVersion);
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                ErrorUtilities.VerifyThrowArgument(false, e, "AppConfig.InvalidOldVersionAttribute", e.Message);
            }

            string newVersionAttribute = reader.GetAttribute("newVersion");

            // A badly formed assembly name.
            ErrorUtilities.VerifyThrowArgument(!String.IsNullOrEmpty(newVersionAttribute), "AppConfig.BindingRedirectMissingNewVersion");

            try
            {
                NewVersion = new Version(newVersionAttribute);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                ErrorUtilities.VerifyThrowArgument(false, e, "AppConfig.InvalidNewVersionAttribute", e.Message);
            }
        }
    }
}
