// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Helper class that contains manifest resource name creating functions,
    /// from a generic name-creation function to minimize code differences.
    /// </summary>
    internal static class ManifestNameCreator
    {
        /// <summary>
        /// Utility function for creating a C#-style manifest name from 
        /// a resource name. Note that this function attempts to emulate the
        /// Everret implementation of this code which can be found by searching for
        /// ComputeNonWFCResourceName() or ComputeWFCResourceName() in
        /// \vsproject\langproj\langbldmgrsite.cpp
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="prependCultureAsDirectory">should the culture name be prepended to the manifest name as a path</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .cs file). May be null</param>
        /// <param name="culture">The override culture of this resource, if any</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <param name="log">Task's TaskLoggingHelper, for logging warnings or errors</param>
        /// <returns>Returns the manifest name</returns>
        public static string CreateCSharpManifestResourceName
        (
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            TaskLoggingHelper log // Should not be null
        )
        {
            return CreateNameForResource
            (
                fileName,
                linkFileName,
                prependCultureAsDirectory,
                rootNamespace,
                dependentUponFileName,
                culture,
                binaryStream,
                CSharpParserUtilities.GetFirstClassNameFullyQualified,
                log,
                includeRootNamespace: false,
                includeSubFolder: true
            );
        }


        /// <summary>
        /// Utility function for creating a VB-style manifest name from 
        /// a resource name. Note that this function attempts to emulate the
        /// Everret implementation of this code which can be found by searching for
        /// ComputeNonWFCResourceName() or ComputeWFCResourceName() in
        /// \vsproject\langproj\langbldmgrsite.cpp
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="prependCultureAsDirectory">should the culture name be prepended to the manifest name as a path</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .vb file). May be null</param>
        /// <param name="culture">The override culture of this resource, if any</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <param name="log">Task's TaskLoggingHelper, for logging warnings or errors</param>
        /// <returns>Returns the manifest name</returns>
        public static string CreateVisualBasicManifestResourceName
        (
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            TaskLoggingHelper log // Should not be null
        )
        {
            return CreateNameForResource
            (
                fileName,
                linkFileName,
                prependCultureAsDirectory,
                rootNamespace,
                dependentUponFileName,
                culture,
                binaryStream,
                VisualBasicParserUtilities.GetFirstClassNameFullyQualified,
                log,
                includeRootNamespace: true,
                includeSubFolder: false
            );
        }

        /// <summary>
        /// Need to take a look at "ComputeNonWFCResourceName() or ComputeWFCResourceName()"
        /// to relate what this function is trying to emulate?
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="prependCultureAsDirectory">should the culture name be prepended to the manifest name as a path</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .cs/.vb file). May be null</param>
        /// <param name="culture">The override culture of this resource, if any</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <param name="getFirstClassNameFullyQualified">
        /// A function that takes a parameter of (text) stream and returns an ExtractedClassName
        /// object. It should extract according to the dependentUponFile language type, i.e.:
        /// CSharpParserUtilities.GetFirstClassNameFullyQualified should be used if file type is .cs
        /// Nullability relates to binaryStream, nullable if binaryStream is null, unnullable otherwise.
        /// </param>
        /// <param name="log">Task's TaskLoggingHelper, for logging warnings or errors</param>
        /// <param name="includeRootNamespace">Option to include root namespace as part of the manifest name</param>
        /// <param name="includeSubFolder">Option to include subfolder as part of the manifest name</param>
        /// <returns>Returns the manifest name</returns>
        private static string CreateNameForResource
        (
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            Func<Stream, ExtractedClassName> getFirstClassNameFullyQualified, // may be null if binaryStream is null. must not be null when stream is not
            TaskLoggingHelper log,
            bool includeRootNamespace,
            bool includeSubFolder
        )
        {
            // (C#/VBParserUtilities).GetFirstClassNameFullyQualified() is required to extract the
            // name from binary stream, hence it cannot be null when binary stream is present. Nullable otherwise.
            if (binaryStream != null && getFirstClassNameFullyQualified == null)
            {
                throw new ArgumentNullException(nameof(getFirstClassNameFullyQualified));
            }


            // Use the link file name if there is one, otherwise, fall back to file name.
            string embeddedFileName = FileUtilities.FixFilePath(linkFileName);
            if (string.IsNullOrEmpty(embeddedFileName))
            {
                embeddedFileName = FileUtilities.FixFilePath(fileName);
            }

            dependentUponFileName = FileUtilities.FixFilePath(dependentUponFileName);
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo(embeddedFileName, dependentUponFileName);

            // If the item has a culture override, respect that. 
            if (!string.IsNullOrEmpty(culture))
            {
                info.culture = culture;
            }

            var manifestName = new StringBuilder();
            if (binaryStream != null)
            {
                // Resource depends on a form. Now, get the form's class name fully 
                // qualified with a namespace.
                ExtractedClassName result = getFirstClassNameFullyQualified(binaryStream);

                if (result.IsInsideConditionalBlock)
                {
                    log?.LogWarningWithCodeFromResources("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective", dependentUponFileName, embeddedFileName);
                }

                if (!string.IsNullOrEmpty(result.Name))
                {
                    if (includeRootNamespace && !string.IsNullOrEmpty(rootNamespace))
                    {
                        manifestName.Append(rootNamespace).Append(".").Append(result.Name);
                    }
                    else
                    {
                        manifestName.Append(result.Name);
                    }

                    // Append the culture if there is one.        
                    if (!string.IsNullOrEmpty(info.culture))
                    {
                        manifestName.Append(".").Append(info.culture);
                    }
                }
            }

            // If there's no manifest name at this point, then fall back to using the
            // RootNamespace+Filename_with_slashes_converted_to_dots         
            if (manifestName.Length == 0)
            {
                // If Rootnamespace was null, then it wasn't set from the project resourceFile.
                // Empty namespaces are allowed.
                if (!string.IsNullOrEmpty(rootNamespace))
                {
                    manifestName.Append(rootNamespace).Append(".");
                }

                // Replace spaces in the directory name with underscores. Needed for compatibility with Everett.
                // Note that spaces in the file name itself are preserved.
                string everettCompatibleDirectoryName =
                    includeSubFolder ? CreateManifestResourceName.MakeValidEverettIdentifier(Path.GetDirectoryName(info.cultureNeutralFilename)) : string.Empty;

                // only strip extension for .resx and .restext files

                string sourceExtension = Path.GetExtension(info.cultureNeutralFilename);
                if (
                        (0 == string.Compare(sourceExtension, ".resx", StringComparison.OrdinalIgnoreCase))
                        ||
                        (0 == string.Compare(sourceExtension, ".restext", StringComparison.OrdinalIgnoreCase))
                        ||
                        (0 == string.Compare(sourceExtension, ".resources", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    manifestName.Append(Path.Combine(everettCompatibleDirectoryName, Path.GetFileNameWithoutExtension(info.cultureNeutralFilename)));

                    // Replace all '\' with '.'
                    manifestName.Replace(Path.DirectorySeparatorChar, '.');
                    manifestName.Replace(Path.AltDirectorySeparatorChar, '.');

                    // Append the culture if there is one.        
                    if (!string.IsNullOrEmpty(info.culture))
                    {
                        manifestName.Append(".").Append(info.culture);
                    }

                    // If the original extension was .resources, add it back
                    if (string.Equals(sourceExtension, ".resources", StringComparison.OrdinalIgnoreCase))
                    {
                        manifestName.Append(sourceExtension);
                    }
                }
                else
                {
                    manifestName.Append(Path.Combine(everettCompatibleDirectoryName, Path.GetFileName(info.cultureNeutralFilename)));

                    // Replace all '\' with '.'
                    manifestName.Replace(Path.DirectorySeparatorChar, '.');
                    manifestName.Replace(Path.AltDirectorySeparatorChar, '.');

                    if (prependCultureAsDirectory)
                    {
                        // Prepend the culture as a subdirectory if there is one.        
                        if (!string.IsNullOrEmpty(info.culture))
                        {
                            manifestName.Insert(0, Path.DirectorySeparatorChar);
                            manifestName.Insert(0, info.culture);
                        }
                    }
                }
            }

            return manifestName.ToString();
        }
    }
}
