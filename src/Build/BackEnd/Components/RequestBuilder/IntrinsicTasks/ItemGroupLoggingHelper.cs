// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Reflection;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Helper class to convert ItemGroup parameters to a string value for logging.
    /// </summary>
    internal static class ItemGroupLoggingHelper
    {
        /// <summary>
        /// Gets a text serialized value of a parameter for logging.
        /// </summary>
        internal static string GetParameterText(string prefix, string parameterName, params object[] parameterValues)
        {
            return GetParameterText(prefix, parameterName, (IList)parameterValues);
        }

        /// <summary>
        /// Gets a text serialized value of a parameter for logging.
        /// </summary>
        internal static string GetParameterText(string prefix, string parameterName, IList parameterValue)
        {
            if (parameterValue == null || parameterValue.Count == 0)
            {
                return parameterName;
            }

            using (var sb = new ReuseableStringBuilder())
            {
                sb.Append(prefix);

                bool firstEntryIsTaskItemWithSomeCustomMetadata = false;
                var firstItem = parameterValue[0] as ITaskItem;
                if (firstItem != null)
                {
                    if (firstItem.CloneCustomMetadata().Count > 0)
                    {
                        firstEntryIsTaskItemWithSomeCustomMetadata = true;
                    }
                }

                // If it's just one entry in the list, and it's not a task item with metadata, keep it on one line like a scalar
                bool specialTreatmentForSingle = (parameterValue.Count == 1 && !firstEntryIsTaskItemWithSomeCustomMetadata);

                if (!specialTreatmentForSingle)
                {
                    sb.Append("\n    ");
                }

                sb.Append(parameterName + "=");

                if (!specialTreatmentForSingle)
                {
                    sb.Append("\n");
                }

                for (int i = 0; i < parameterValue.Count; i++)
                {
                    if (parameterValue[i] == null)
                    {
                        continue;
                    }

                    if (!specialTreatmentForSingle)
                    {
                        sb.Append("        ");
                    }

                    sb.Append(GetStringFromParameterValue(parameterValue[i]));

                    if (!specialTreatmentForSingle && i < parameterValue.Count - 1)
                    {
                        sb.Append("\n");
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Given an object wrapping a scalar value that will be set on a task,
        /// returns a suitable string to log its value, with a trailing newline.
        /// First line is already indented.
        /// Indent of any subsequent line should be 12 spaces.
        /// </summary>
        internal static string GetStringFromParameterValue(object parameterValue)
        {
            var type = parameterValue.GetType();

            ErrorUtilities.VerifyThrow(!type.IsArray, "scalars only");

            if (type == typeof(string))
            {
                return (string)parameterValue;
            }
            else if (type.GetTypeInfo().IsValueType)
            {
                return (string)Convert.ChangeType(parameterValue, typeof(string), CultureInfo.CurrentCulture);
            }
            else if (typeof(ITaskItem).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
            {
                var item = ((ITaskItem)parameterValue);
                string result = item.ItemSpec;

                var customMetadata = item.CloneCustomMetadata();

                if (customMetadata.Count > 0)
                {
                    result += "\n";
                    var names = new List<string>();

                    foreach (string name in customMetadata.Keys)
                    {
                        names.Add(name);
                    }

                    names.Sort();

                    for (int i = 0; i < names.Count; i++)
                    {
                        result += "                " + names[i] + "=" + customMetadata[names[i]];

                        if (i < names.Count - 1)
                        {
                            result += "\n";
                        }
                    }
                }

                return result;
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();
            return null;
        }
    }
}
