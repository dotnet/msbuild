// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
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
        private Version _oldVersionLow = null;

        /// <summary>
        /// The high end of the old version range.
        /// </summary>
        private Version _oldVersionHigh = null;

        /// <summary>
        /// The new version number.
        /// </summary>
        private Version _newVersion = null;

        /// <summary>
        /// The low end of the old version range.
        /// </summary>
        internal Version OldVersionLow
        {
            set { _oldVersionLow = value; }
            get { return _oldVersionLow; }
        }

        /// <summary>
        /// The high end of the old version range.
        /// </summary>
        internal Version OldVersionHigh
        {
            set { _oldVersionHigh = value; }
            get { return _oldVersionHigh; }
        }

        /// <summary>
        /// The new version number.
        /// </summary>
        internal Version NewVersion
        {
            set { _newVersion = value; }
            get { return _newVersion; }
        }

        /// <summary>
        /// The reader is positioned on a &lt;bindingRedirect&gt; element--read it.
        /// </summary>
        /// <param name="reader"></param>
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
                    _oldVersionLow = new Version(oldVersion.Substring(0, dashPosition));
                    _oldVersionHigh = new Version(oldVersion.Substring(dashPosition + 1));
                }
                else
                {
                    // This is a single version.
                    _oldVersionLow = new Version(oldVersion);
                    _oldVersionHigh = new Version(oldVersion);
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
                _newVersion = new Version(newVersionAttribute);
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
