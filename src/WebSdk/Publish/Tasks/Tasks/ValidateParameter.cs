// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class ValidateParameter : Build.Utilities.Task
    {
        [Required]
        public string ParameterName { get; set; }

        public string ParameterValue { get; set; }

        public override bool Execute()
        {
            if (String.IsNullOrEmpty(ParameterValue))
            {
                Log.LogError(String.Format(CultureInfo.CurrentCulture, Resources.ValidateParameter_ArgumentNullError, ParameterName));
                return false;
            }

            return true;
        }
    }
}
