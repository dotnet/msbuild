// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class provides utilities for RFC1766 culture strings.  
    /// </summary>
    internal static class CultureStringUtilities
    {
        static private string[] s_cultureInfoStrings;

        /// <summary>
        ///      Validates a RFC1766 culture string.
        /// </summary>
        /// <param name='cultureString'>
        ///     the culture string to be validated
        /// </param>
        /// <returns>
        ///      returns true if the culture string is valid
        /// </returns>
        internal static bool IsValidCultureString(string cultureString)
        {
            // Get the supported cultures
            PopulateCultureInfoArray();

            // Note, it does not matter what kind of comparer we use as long as the comparer
            // for Array.Sort() [see PopulateCultureInfoArray()] and Array.BinarySearch() is 
            // the same.  
            bool valid = true;

            if (Array.BinarySearch(s_cultureInfoStrings, cultureString, StringComparer.OrdinalIgnoreCase) < 0)
            {
                valid = false;
            }

            return valid;
        }

        /// <summary>
        /// Populate the array of culture strings.
        /// </summary>
        internal static void PopulateCultureInfoArray()
        {
            if (s_cultureInfoStrings == null)
            {
                CultureInfo[] cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures);


                s_cultureInfoStrings = new string[cultureInfos.Length];
                for (int i = 0; i < cultureInfos.Length; i++)
                {
                    s_cultureInfoStrings[i] = cultureInfos[i].Name;
                }

                // Note, it does not matter what kind of comparer we use as long as the comparer
                // for Array.BinarySearch() [see ValidateCultureInfoString()] and Array.Sort() is 
                // the same.  
                Array.Sort(s_cultureInfoStrings, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
