// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Formats a version by combining version and revision.
    /// </summary>
    /// <comment>
    ///  Case #1: Input: Version=&lt;undefined&gt;  Revision=&lt;don't care&gt;   Output: OutputVersion="1.0.0.0"
    ///  Case #2: Input: Version="1.0.0.*"    Revision="5"            Output: OutputVersion="1.0.0.5"
    ///  Case #3: Input: Version="1.0.0.0"    Revision=&lt;don't care&gt;   Output: OutputVersion="1.0.0.0"
    /// </comment>
    public sealed class FormatVersion : TaskExtension
    {
        private enum _FormatType { Version, Path }

        private _FormatType _formatType = _FormatType.Version;

        [Output]
        public string OutputVersion { get; set; }

        public string FormatType { get; set; }

        public int Revision { get; set; }

        public string Version { get; set; }

        public override bool Execute()
        {
            if (!ValidateInputs())
            {
                return false;
            }

            if (String.IsNullOrEmpty(Version))
            {
                OutputVersion = "1.0.0.0";
            }
            else if (Version.EndsWith("*", StringComparison.Ordinal))
            {
                OutputVersion = Version.Substring(0, Version.Length - 1) + Revision.ToString("G", CultureInfo.InvariantCulture);
            }
            else
            {
                OutputVersion = Version;
            }

            if (_formatType == _FormatType.Path)
            {
                OutputVersion = OutputVersion.Replace('.', '_');
            }
            return true;
        }

        private bool ValidateInputs()
        {
            if (FormatType != null)
            {
                try
                {
                    _formatType = (_FormatType)Enum.Parse(typeof(_FormatType), FormatType, true);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("General.InvalidValue", "FormatType", "FormatVersion");
                    return false;
                }
            }
            return true;
        }
    }
}
