// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// A replacement for AssemblyName that optimizes calls to FullName which is expensive.
    /// The assembly name is represented internally by an AssemblyName and a string, conversion
    /// between the two is done lazily on demand.
    /// </summary>
    [Serializable]
    sealed internal class AssemblyNameExtension
    {
        private AssemblyName asAssemblyName = null;
        private string asString = null;

        static private AssemblyNameExtension unnamedAssembly = new AssemblyNameExtension();

        /// <summary>
        /// Construct an unnamed assembly.
        /// Private because we want only one of these.
        /// </summary>
        private AssemblyNameExtension()
        {
        }

        /// <summary>
        /// Construct with AssemblyName.
        /// </summary>
        /// <param name="assemblyName"></param>
        internal AssemblyNameExtension(AssemblyName assemblyName)
        {
            asAssemblyName = assemblyName;
        }

        /// <summary>
        /// Construct with string.
        /// </summary>
        /// <param name="assemblyName"></param>
        internal AssemblyNameExtension(string assemblyName)
        {
            asString = assemblyName;
        }

        /// <summary>
        /// Construct from a string, but immediately construct a real AssemblyName.
        /// This will cause an exception to be thrown up front if the assembly name 
        /// isn't well formed.
        /// </summary>
        /// <param name="assemblyName">
        /// The string version of the assembly name.
        /// </param>
        /// <param name="validate">
        /// Used when the assembly name comes from a user-controlled source like a project file or config file.
        /// Does extra checking on the assembly name and will throw exceptions if something is invalid.
        /// </param>
        internal AssemblyNameExtension(string assemblyName, bool validate) 
        {
            asString = assemblyName;

            if (validate)
            {
                // This will throw...
                CreateAssemblyName();
            }
        }

        /// <summary>
        /// To be used as a delegate. Gets the AssemblyName of the given file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static AssemblyNameExtension GetAssemblyNameEx(string path)
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(path);
            if (assemblyName == null)
            {
                return null;
            }
            return new AssemblyNameExtension(assemblyName);
        }
        
        /// <summary>
        /// Assume there is a string version, create the AssemblyName version.
        /// </summary>
        private void CreateAssemblyName()
        {
            if (asAssemblyName == null)
            {
                asAssemblyName = GetAssemblyNameFromDisplayName(asString);
            }
        }

        /// <summary>
        /// Assume there is a string version, create the AssemblyName version.
        /// </summary>
        private void CreateFullName()
        {
            if (asString == null)
            {
                asString = asAssemblyName.FullName;
            }
        }

        /// <summary>
        /// The base name of the assembly.
        /// </summary>
        /// <value></value>
        internal string Name
        {
            get
            {
                // Is there a string?
                CreateAssemblyName();
                return asAssemblyName.Name;
            }
        }

        /// <summary>
        /// The assembly's version number.
        /// </summary>
        /// <value></value>
        internal Version Version
        {
            get
            {
                // Is there a string?
                CreateAssemblyName();
                return asAssemblyName.Version;
            }
        }

        /// <summary>
        /// Replace the current version with a new version.
        /// </summary>
        /// <param name="version"></param>
        internal void ReplaceVersion(Version version)
        {
            CreateAssemblyName();
            if (asAssemblyName.Version != version)
            {
                asAssemblyName.Version = version;

                // String would now be invalid.
                asString = null;
            }
        }

        /// <summary>
        /// The assembly's version number.
        /// </summary>
        /// <value></value>
        internal CultureInfo CultureInfo
        {
            get
            {
                // Is there a string?
                CreateAssemblyName();
                return asAssemblyName.CultureInfo;
            }
        }

        /// <summary>
        /// As an AssemblyName
        /// </summary>
        /// <value></value>
        internal AssemblyName AssemblyName
        {
            get
            {
                // Is there a string?
                CreateAssemblyName();
                return asAssemblyName;
            }
        }

        /// <summary>
        /// The assembly's full name.
        /// </summary>
        /// <value></value>
        internal string FullName
        {
            get
            {
                // Is there a string?
                CreateFullName();
                return asString;
            }
        }

        /// <summary>
        /// Get the assembly's public key token.
        /// </summary>
        /// <returns></returns>
        internal byte[] GetPublicKeyToken()
        {
            // Is there a string?
            CreateAssemblyName();
            return asAssemblyName.GetPublicKeyToken();
        }

        /// <summary>
        /// A special "unnamed" instance of AssemblyNameExtension.
        /// </summary>
        /// <value></value>
        internal static AssemblyNameExtension UnnamedAssembly
        {
            get
            {
                return unnamedAssembly;
            }
        }

        /// <summary>
        /// Compare one assembly name to another.
        /// </summary>
        /// <param name="that"></param>
        /// <returns></returns>
        internal int CompareTo(AssemblyNameExtension that)
        {
            // Are they identical?
            if (this.Equals(that))
            {
                return 0;
            }

            // Are the base names not identical?
            int result = CompareBaseNameTo(that);
            if (result != 0)
            {
                return result;
            }

            // We need some collating order for these, alphabetical by FullName seems as good as any.
            return String.Compare(this.FullName, that.FullName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get a hash code for this assembly name.
        /// </summary>
        /// <returns></returns>
        new internal int GetHashCode()
        {
            // Ok, so this isn't a great hashing algorithm. However, basenames with different 
            // versions or PKTs are relatively uncommon and so collisions should be low.
            // Hashing on FullName is wrong because the order of tuple fields is undefined.
            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name);
            return hash;
        }

        /// <summary>
        /// Compare two base names as quickly as possible.
        /// </summary>
        /// <param name="that"></param>
        /// <returns></returns>
        internal int CompareBaseNameTo(AssemblyNameExtension that)
        {
            int result = CompareBaseNameToImpl(that);
#if DEBUG
            // Now, compare to the real value to make sure the result was accurate.
            AssemblyName a1 = this.asAssemblyName;
            AssemblyName a2 = that.asAssemblyName;
            if (a1 == null)
            {
                a1 = new AssemblyName(this.asString);
            }
            if (a2 == null)
            {
                a2 = new AssemblyName(that.asString);
            }

            int baselineResult = String.Compare(a1.Name, a2.Name, StringComparison.OrdinalIgnoreCase);
            Debug.Assert(result == baselineResult, "Optimized version of CompareBaseNameTo didn't return the same result as the baseline.");
#endif
            return result;
        }

        /// <summary>
        /// An implementation of compare that compares two base 
        /// names as quickly as possible.
        /// </summary>
        /// <param name="that"></param>
        /// <returns></returns>
        private int CompareBaseNameToImpl(AssemblyNameExtension that)
        {
            // Pointer compare, if identical then base names are
            // equal.
            if (this == that)
            {
                return 0;
            }
            // Do both have assembly names?
            if (this.asAssemblyName != null && that.asAssemblyName != null)
            {
                // Pointer compare.
                if (this.asAssemblyName == that.asAssemblyName)
                {
                    return 0;
                }

                // Base name compare.
                return String.Compare(this.asAssemblyName.Name, that.asAssemblyName.Name, StringComparison.OrdinalIgnoreCase);
            }

            // Do both have strings?
            if (this.asString != null && that.asString != null)
            {
                // If we have two random-case strings, then we need to compare case sensitively.
                return CompareBaseNamesStringWise(this.asString, that.asString);
            }

            // Fall back to comparing by name. This is the slow path.
            return String.Compare(this.Name, that.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compare two basenames.
        /// </summary>
        /// <param name="asString1"></param>
        /// <param name="asString2"></param>
        /// <returns></returns>
        private static int CompareBaseNamesStringWise(string asString1, string asString2)
        {
            // Identical strings just match.
            if (asString1 == asString2)
            {
                return 0;
            }

            // Get the lengths of base names to compare.
            int baseLenThis = asString1.IndexOf(',');
            int baseLenThat = asString2.IndexOf(',');
            if (baseLenThis == -1)
            {
                baseLenThis = asString1.Length;
            }
            if (baseLenThat == -1)
            {
                baseLenThat = asString2.Length;
            }

            // If the lengths are the same then we can compare without copying.
            if (baseLenThis == baseLenThat) 
            {
                return String.Compare(asString1, 0, asString2, 0, baseLenThis, StringComparison.OrdinalIgnoreCase);
            }

            // Lengths are different, so string copy is required.
            string nameThis = asString1.Substring(0, baseLenThis);
            string nameThat = asString2.Substring(0, baseLenThat);
            return String.Compare(nameThis, nameThat, StringComparison.OrdinalIgnoreCase);   
        }

        /// <summary>
        /// Compare two assembly names for equality.
        /// </summary>
        /// <param name="that"></param>
        /// <returns></returns>
        internal bool Equals(AssemblyNameExtension that)
        {
            // Pointer compare.
            if (this == that)
            {
                return true;
            }

            // Do both have assembly names?
            if (this.asAssemblyName != null && that.asAssemblyName != null)
            {
                // Pointer compare.
                if (this.asAssemblyName == that.asAssemblyName)
                {
                    return true;
                }
            }

            // Do both have strings that equal each-other?
            if (this.asString != null && that.asString != null)
            {
                if (this.asString == that.asString)
                {
                    return true;
                }

                // If they weren't identical then they might still differ only by
                // case. So we can't assume that they don't match. So fall through...
                
            }

            // Do the names match?
            if (!String.Equals(Name, that.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Do the versions match?
            if (Version != that.Version)
            {
                return false;
            }

            // Do the Cultures match?
            CultureInfo aCulture = CultureInfo;
            CultureInfo bCulture = that.CultureInfo;
            if (aCulture == null)
            {
                aCulture = CultureInfo.InvariantCulture;
            }
            if (bCulture == null)
            {
                bCulture = CultureInfo.InvariantCulture;
            }
            if (aCulture.LCID != bCulture.LCID)
            {
                return false;
            }

            // Do the PKTs match?
            byte[] aPKT = GetPublicKeyToken();
            byte[] bPKT = that.GetPublicKeyToken();

            // Some assemblies (real case was interop assembly) may have null PKTs.
            if (aPKT == null)
            {
                aPKT = new byte[0];
            }
            if (bPKT == null)
            {
                bPKT = new byte[0];
            }

            if (aPKT.Length != bPKT.Length)
            {
                return false;
            }
            for (int i = 0; i < aPKT.Length; ++i)
            {
                if (aPKT[i] != bPKT[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Only the unnamed assembly has both null assemblyname and null string.
        /// </summary>
        /// <returns></returns>
        internal bool IsUnnamedAssembly
        {
            get
            {
                return asAssemblyName == null && asString == null;
            }
        }

        /// <summary>
        /// Given a display name, construct an assembly name.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The assembly name.</returns>
        private static AssemblyName GetAssemblyNameFromDisplayName(string displayName)
        {
            AssemblyName assemblyName = new AssemblyName(displayName);
            return assemblyName;
        }

        /// <summary>
        /// Return a string that has AssemblyName special characters escaped. 
        /// Those characters are Equals(=), Comma(,), Quote("), Apostrophe('), Backslash(\).
        /// </summary>
        /// <remarks>
        /// WARNING! This method is not meant as a general purpose escaping method for assembly names.
        /// Use only if you really know that this does what you need.
        /// </remarks>
        /// <param name="displayName"></param>
        /// <returns></returns>
        internal static string EscapeDisplayNameCharacters(string displayName)
        {
            StringBuilder sb = new StringBuilder(displayName);
            sb = sb.Replace("\\", "\\\\");
            sb = sb.Replace("=", "\\=");
            sb = sb.Replace(",", "\\,");
            sb = sb.Replace("\"", "\\\"");
            sb = sb.Replace("'", "\\'");
            return sb.ToString();
        }

        /// <summary>
        /// Convert to a string for display.
        /// </summary>
        /// <returns></returns>
        override public string ToString()
        {
            CreateFullName();
            return this.asString;
        }
    }
}
