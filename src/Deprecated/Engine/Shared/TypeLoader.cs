// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class is used to load types from their assemblies.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal class TypeLoader
    {
        private Dictionary<AssemblyLoadInfo, List<Type>> cacheOfAllDesiredTypesInAnAssembly = new Dictionary<AssemblyLoadInfo, List<Type>>();

        private TypeFilter isDesiredType;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isDesiredType"></param>
        /// <owner>RGoel</owner>
        internal TypeLoader(TypeFilter isDesiredType)
        {
            ErrorUtilities.VerifyThrow(isDesiredType != null, "need a type filter");

            this.isDesiredType = isDesiredType;
        }

        /// <summary>
        /// Loads the specified type if it exists in the given assembly. If the type name is fully qualified, then a match (if
        /// any) is unambiguous; otherwise, if there are multiple types with the same name in different namespaces, the first type
        /// found will be returned.
        /// </summary>
        /// <remarks>This method throws exceptions -- it is the responsibility of the caller to handle them.</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="typeName">Can be empty string.</param>
        /// <param name="assembly"></param>
        /// <returns>The loaded type, or null if the type was not found.</returns>
        internal LoadedType Load
        (
            string typeName,
            AssemblyLoadInfo assembly
        )
        {
            Type type = null;

            // Maybe we've already cracked open this assembly before.  If so, just grab the list
            // of public desired types that we found last time.
            List<Type> desiredTypesInAssembly;
            cacheOfAllDesiredTypesInAnAssembly.TryGetValue(assembly, out desiredTypesInAssembly);

            // If we have the assembly name (strong or weak), and we haven't cracked this assembly open
            // before to discover all the public desired types.
            if ((assembly.AssemblyName != null) && (typeName.Length > 0) && (desiredTypesInAssembly == null))
            {
                try
                {
                    // try to load the type using its assembly qualified name
                    type = Type.GetType(typeName + "," + assembly.AssemblyName, false /* don't throw on error */, true /* case-insensitive */);
                }
                catch (ArgumentException)
                {
                    // Type.GetType() will throw this exception if the type name is invalid -- but we have no idea if it's the
                    // type or the assembly name that's the problem -- so just ignore the exception, because we're going to
                    // check the existence/validity of the assembly and type respectively, below anyway
                }
            }

            // if we found the type, it means its assembly qualified name was also its fully qualified name
            if (type != null)
            {
                // if it's not the right type, bail out -- there's no point searching further since we already matched on the
                // fully qualified name
                if (!isDesiredType(type, null))
                {
                    return null;
                }
            }
            // if the type name was not fully qualified, or if we only have the assembly file/path
            else
            {
                if (desiredTypesInAssembly == null)
                {
                    // we need to search the assembly for the type...
                    Assembly loadedAssembly;

                    try
                    {
                        if (assembly.AssemblyName != null)
                        {
                            loadedAssembly = Assembly.Load(assembly.AssemblyName);
                        }
                        else
                        {
                            loadedAssembly = Assembly.UnsafeLoadFrom(assembly.AssemblyFile);
                        }
                    }
                    // Assembly.Load() and Assembly.LoadFrom() will throw an ArgumentException if the assembly name is invalid
                    catch (ArgumentException e)
                    {
                        // convert to a FileNotFoundException because it's more meaningful
                        // NOTE: don't use ErrorUtilities.VerifyThrowFileExists() here because that will hit the disk again
                        throw new FileNotFoundException(null, assembly.ToString(), e);
                    }

                    // only look at public types
                    Type[] allPublicTypesInAssembly = loadedAssembly.GetExportedTypes();
                    desiredTypesInAssembly = new List<Type>();

                    foreach (Type publicType in allPublicTypesInAssembly)
                    {
                        if (isDesiredType(publicType, null))
                        {
                            desiredTypesInAssembly.Add(publicType);
                        }
                    }

                    // Save the list of desired types into our cache, so that we don't have to crack it
                    // open again.
                    cacheOfAllDesiredTypesInAnAssembly[assembly] = desiredTypesInAssembly;
                }

                foreach (Type desiredTypeInAssembly in desiredTypesInAssembly)
                {
                    // if type matches partially on its name
                    if ((typeName.Length == 0) || IsPartialTypeNameMatch(desiredTypeInAssembly.FullName, typeName))
                    {
                        type = desiredTypeInAssembly;
                        break;
                    }
                }
            }

            if (type != null)
            {
                return new LoadedType(type, assembly);
            }

            return null;
        }

        /// <summary>
        /// Given two type names, looks for a partial match between them. A partial match is considered valid only if it occurs on
        /// the right side (tail end) of the name strings, and at the start of a class or namespace name.
        /// </summary>
        /// <remarks>
        /// 1) Matches are case-insensitive.
        /// 2) .NET conventions regarding namespaces and nested classes are respected, including escaping of reserved characters.
        /// </remarks>
        /// <example>
        /// "Csc" and "csc"                                                 ==> exact match
        /// "Microsoft.Build.Tasks.Csc" and "Microsoft.Build.Tasks.Csc"     ==> exact match
        /// "Microsoft.Build.Tasks.Csc" and "Csc"                           ==> partial match
        /// "Microsoft.Build.Tasks.Csc" and "Tasks.Csc"                     ==> partial match
        /// "MyTasks.ATask+NestedTask" and "NestedTask"                     ==> partial match
        /// "MyTasks.ATask\\+NestedTask" and "NestedTask"                   ==> partial match
        /// "MyTasks.CscTask" and "Csc"                                     ==> no match
        /// "MyTasks.MyCsc" and "Csc"                                       ==> no match
        /// "MyTasks.ATask\.Csc" and "Csc"                                  ==> no match
        /// "MyTasks.ATask\\\.Csc" and "Csc"                                ==> no match
        /// </example>
        /// <owner>SumedhK</owner>
        /// <param name="typeName1"></param>
        /// <param name="typeName2"></param>
        /// <returns>true, if the type names match exactly or partially; false, if there is no match at all</returns>
        internal static bool IsPartialTypeNameMatch(string typeName1, string typeName2)
        {
            bool isPartialMatch = false;

            // if the type names are the same length, a partial match is impossible
            if (typeName1.Length != typeName2.Length)
            {
                string longerTypeName;
                string shorterTypeName;

                // figure out which type name is longer
                if (typeName1.Length > typeName2.Length)
                {
                    longerTypeName = typeName1;
                    shorterTypeName = typeName2;
                }
                else
                {
                    longerTypeName = typeName2;
                    shorterTypeName = typeName1;
                }

                // if the shorter type name matches the end of the longer one
                if (longerTypeName.EndsWith(shorterTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    int matchIndex = longerTypeName.Length - shorterTypeName.Length;

                    // if the matched sub-string looks like the start of a namespace or class name
                    if ((longerTypeName[matchIndex - 1] == '.') || (longerTypeName[matchIndex - 1] == '+'))
                    {
                        int precedingBackslashes = 0;

                        // confirm there are zero, or an even number of \'s preceding it...
                        for (int i = matchIndex - 2; i >= 0; i--)
                        {
                            if (longerTypeName[i] == '\\')
                            {
                                precedingBackslashes++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if ((precedingBackslashes % 2) == 0)
                        {
                            isPartialMatch = true;
                        }
                    }
                }
            }
            // check if the type names match exactly
            else
            {
                isPartialMatch = (String.Equals(typeName1, typeName2, StringComparison.OrdinalIgnoreCase));
            }

            return isPartialMatch;
        }
    }
}
