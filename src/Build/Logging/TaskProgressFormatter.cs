// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Describes how much of a task progress operation is done.
    /// </summary>
    /// <remarks>
    /// Terminal Logger and the classic console logger show progress very differently, but both must
    /// name the unit in the user's language. Sharing the text keeps the two consistent and keeps the
    /// unit names to one set of translations.
    /// </remarks>
    internal static class TaskProgressFormatter
    {
        /// <summary>
        /// Formats <paramref name="completed"/>, and <paramref name="total"/> when it is known, as
        /// localized text that names <paramref name="unit"/>.
        /// </summary>
        internal static string FormatAmount(long completed, long? total, TaskProgressUnit unit)
        {
            string completedText = completed.ToString("N0", CultureInfo.CurrentCulture);

            if (total is not long totalValue)
            {
                return unit switch
                {
                    TaskProgressUnit.Bytes => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskProgressBytes", completedText),
                    TaskProgressUnit.Items => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskProgressItems", completedText),
                    _ => completedText,
                };
            }

            string totalText = totalValue.ToString("N0", CultureInfo.CurrentCulture);

            return unit switch
            {
                TaskProgressUnit.Bytes => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskProgressBytesOfTotal", completedText, totalText),
                TaskProgressUnit.Items => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskProgressItemsOfTotal", completedText, totalText),
                _ => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TaskProgressOfTotal", completedText, totalText),
            };
        }
    }
}
