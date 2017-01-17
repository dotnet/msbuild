// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging an error from the SDK targets.
    /// </summary>
    public class NETSdkError : TaskBase
    {
        /// <summary>
        /// The name of the resource in Strings.resx that contains the desired error message.
        /// </summary>
        [Required]
        public string ResourceName { get; set; }

        /// <summary>
        /// The arguments provided to <see cref="string.Format"/> along with the retrieved resource as the format.
        /// </summary>
        public string[] FormatArguments { get; set; }

        public bool WarningOnly { get; set; }

        protected override void ExecuteCore()
        {
            string format = Strings.ResourceManager.GetString(ResourceName, Strings.Culture);
            string[] arguments = FormatArguments ?? Array.Empty<string>();
            string message = string.Format(CultureInfo.CurrentCulture, format, arguments);

            if (WarningOnly)
            {
                Log.LogWarning(message);
            }
            else
            {
                Log.LogError(message);
            }
        }
    }
}
