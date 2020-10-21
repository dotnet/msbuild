// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Win32;
using System.IO;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// The Intrinsic class provides static methods that can be accessed from MSBuild's
    /// property functions using $([MSBuild]::Function(x,y))
    /// </summary>
    internal static class IntrinsicFunctions
    {
        /// <summary>
        /// Add two doubles
        /// </summary>
        internal static double Add(double a, double b)
        {
            return a + b;
        }

        /// <summary>
        /// Add two longs
        /// </summary>
        internal static long Add(long a, long b)
        {
            return a + b;
        }

        /// <summary>
        /// Subtract two doubles
        /// </summary>
        internal static double Subtract(double a, double b)
        {
            return a - b;
        }

        /// <summary>
        /// Subtract two longs
        /// </summary>
        internal static long Subtract(long a, long b)
        {
            return a - b;
        }

        /// <summary>
        /// Multiply two doubles
        /// </summary>
        internal static double Multiply(double a, double b)
        {
            return a * b;
        }

        /// <summary>
        /// Multiply two longs
        /// </summary>
        internal static long Multiply(long a, long b)
        {
            return a * b;
        }

        /// <summary>
        /// Divide two doubles
        /// </summary>
        internal static double Divide(double a, double b)
        {
            return a / b;
        }

        /// <summary>
        /// Divide two longs
        /// </summary>
        internal static long Divide(long a, long b)
        {
            return a / b;
        }

        /// <summary>
        /// Modulo two doubles
        /// </summary>
        internal static double Modulo(double a, double b)
        {
            return a % b;
        }

        /// <summary>
        /// Modulo two longs
        /// </summary>
        internal static long Modulo(long a, long b)
        {
            return a % b;
        }

        /// <summary>
        /// Escape the string according to MSBuild's escaping rules
        /// </summary>
        internal static string Escape(string unescaped)
        {
            return EscapingUtilities.Escape(unescaped);
        }

        /// <summary>
        /// Unescape the string according to MSBuild's escaping rules
        /// </summary>
        internal static string Unescape(string escaped)
        {
            return EscapingUtilities.UnescapeAll(escaped);
        }

        /// <summary>
        /// Perform a bitwise OR on the first and second (first | second) 
        /// </summary>
        internal static int BitwiseOr(int first, int second)
        {
            return first | second;
        }

        /// <summary>
        /// Perform a bitwise AND on the first and second (first &amp; second) 
        /// </summary>
        internal static int BitwiseAnd(int first, int second)
        {
            return first & second;
        }

        /// <summary>
        /// Perform a bitwise XOR on the first and second (first ^ second) 
        /// </summary>
        internal static int BitwiseXor(int first, int second)
        {
            return first ^ second;
        }

        /// <summary>
        /// Perform a bitwise NOT on the first and second (~first) 
        /// </summary>
        internal static int BitwiseNot(int first)
        {
            return ~first;
        }

        /// <summary>
        /// Get the value of the registry key and value, default value is null
        /// </summary>
        internal static object GetRegistryValue(string keyName, string valueName)
        {
            return Registry.GetValue(keyName, valueName, null /* null to match the $(Regsitry:XYZ@ZBC) behaviour */);
        }

        /// <summary>
        /// Get the value of the registry key and value
        /// </summary>
        internal static object GetRegistryValue(string keyName, string valueName, object defaultValue)
        {
            return Registry.GetValue(keyName, valueName, defaultValue);
        }

        /// <summary>
        /// Get the value of the registry key from one of the RegistryView's specified
        /// </summary>
        internal static object GetRegistryValueFromView(string keyName, string valueName, object defaultValue, params object[] views)
        {
            string subKeyName;

            // We will take on handing of default value
            // A we need to act on the null return from the GetValue call below
            // so we can keep searching other registry views
            object result = defaultValue;

            // If we haven't been passed any views, then we'll just use the default view
            if (views == null || views.Length == 0)
            {
                views = new object[] { RegistryView.Default };
            }

            foreach (object viewObject in views)
            {
                string viewAsString = viewObject as string;

                if (viewAsString != null)
                {
                    string typeLeafName = typeof(RegistryView).Name + ".";
                    string typeFullName = typeof(RegistryView).FullName + ".";

                    // We'll allow the user to specify the leaf or full type name on the RegistryView enum
                    viewAsString = viewAsString.Replace(typeFullName, "").Replace(typeLeafName, "");

                    // This may throw - and that's fine as the user will receive a controlled version
                    // of that error.
                    RegistryView view = (RegistryView)Enum.Parse(typeof(RegistryView), viewAsString, true);

                    using (RegistryKey key = GetBaseKeyFromKeyName(keyName, view, out subKeyName))
                    {
                        if (key != null)
                        {
                            using (RegistryKey subKey = key.OpenSubKey(subKeyName, false))
                            {
                                // If we managed to retrieve the subkey, then move onto locating the value
                                if (subKey != null)
                                {
                                    result = subKey.GetValue(valueName);
                                }

                                // We've found a value, so stop looking
                                if (result != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // We will have either found a result or defaultValue if one wasn't found at this point
            return result;
        }

        /// <summary>
        /// Given the absolute location of a file, and a disc location, returns relative file path to that disk location. 
        /// Throws UriFormatException.
        /// </summary>
        /// <param name="basePath">
        /// The base path we want to relativize to. Must be absolute.  
        /// Should <i>not</i> include a filename as the last segment will be interpreted as a directory.
        /// </param>
        /// <param name="path">
        /// The path we need to make relative to basePath.  The path can be either absolute path or a relative path in which case it is relative to the base path.
        /// If the path cannot be made relative to the base path (for example, it is on another drive), it is returned verbatim.
        /// </param>
        /// <returns>relative path (can be the full path)</returns>
        internal static string MakeRelative(string basePath, string path)
        {
            string result = FileUtilities.MakeRelative(basePath, path);

            return result;
        }
        
        /// <summary>
        /// Locate a file in either the directory specified or a location in the
        /// direcorty structure above that directory.
        /// </summary>
        internal static string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName)
        {
            // Canonicalize our starting location
            string lookInDirectory = Path.GetFullPath(startingDirectory);

            do
            {
                // Construct the path that we will use to test against
                string possibleFileDirectory = Path.Combine(lookInDirectory, fileName);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (File.Exists(possibleFileDirectory))
                {
                    // We've found the file, return the directory we found it in
                    return lookInDirectory;
                }
                else
                {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }
            }
            while (lookInDirectory != null);

            // When we didn't find the location, then return an empty string
            return String.Empty;
        }

        /// <summary>
        /// Return the string in parameter 'defaultValue' only if parameter 'conditionValue' is empty
        /// else, return the value conditionValue
        /// </summary>
        internal static string ValueOrDefault(string conditionValue, string defaultValue)
        {
            if (String.IsNullOrEmpty(conditionValue))
            {
                return defaultValue;
            }
            else
            {
                return conditionValue;
            }
        }

        /// <summary>
        /// Returns true if a task host exists that can service the requested runtime and architecture
        /// values, and false otherwise. 
        /// </summary>
        /// <comments>
        /// The old engine ignores the concept of the task host entirely, so it shouldn't really
        /// matter what we return.  So we return "true" because regardless of the task host parameters, 
        /// the task will be successfully run (in-proc).
        /// </comments>
        internal static bool DoesTaskHostExist(string runtime, string architecture)
        {
            return true;
        }

        #region Debug only intrinsics

        /// <summary>
        /// returns if the string contains escaped wildcards
        /// </summary>
        internal static List<string> __GetListTest()
        {
            return new List<string> { "A", "B", "C", "D" };
        }

        #endregion

        /// <summary>
        /// Following function will parse a keyName and returns the basekey for it.
        /// It will also store the subkey name in the out parameter.
        /// If the keyName is not valid, we will throw ArgumentException.
        /// The return value shouldn't be null.
        /// Taken from: \ndp\clr\src\BCL\Microsoft\Win32\Registry.cs
        /// </summary>
        private static RegistryKey GetBaseKeyFromKeyName(string keyName, RegistryView view, out string subKeyName)
        {
            if (keyName == null)
            {
                throw new ArgumentNullException(nameof(keyName));
            }

            string basekeyName;
            int i = keyName.IndexOf('\\');
            if (i != -1)
            {
                basekeyName = keyName.Substring(0, i).ToUpper(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                basekeyName = keyName.ToUpper(System.Globalization.CultureInfo.InvariantCulture);
            }

            RegistryKey basekey = null;

            switch (basekeyName)
            {
                case "HKEY_CURRENT_USER":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                    break;
                case "HKEY_LOCAL_MACHINE":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    break;
                case "HKEY_CLASSES_ROOT":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
                    break;
                case "HKEY_USERS":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.Users, view);
                    break;
                case "HKEY_PERFORMANCE_DATA":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.PerformanceData, view);
                    break;
                case "HKEY_CURRENT_CONFIG":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.CurrentConfig, view);
                    break;
                case "HKEY_DYN_DATA":
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.DynData, view);
                    break;
                default:
                    ErrorUtilities.ThrowArgument(keyName);
                    break;
            }

            if (i == -1 || i == keyName.Length)
            {
                subKeyName = string.Empty;
            }
            else
            {
                subKeyName = keyName.Substring(i + 1, keyName.Length - i - 1);
            }

            return basekey;
        }
    }
}
