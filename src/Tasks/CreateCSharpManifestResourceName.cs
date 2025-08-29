// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for task that determines the appropriate manifest resource name to
    /// assign to a given resx or other resource.
    /// </summary>
    public class CreateCSharpManifestResourceName : CreateManifestResourceName
    {
        protected override string SourceFileExtension => ".cs";

        /// <summary>
        /// Utility function for creating a C#-style manifest name from
        /// a resource name.
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .cs file). May be null</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <returns>Returns the manifest name</returns>
        protected override string CreateManifestName(
            string fileName,
            string linkFileName,
            string rootNamespace,
            string dependentUponFileName,
            Stream binaryStream)
        {
            string culture = null;
            bool treatAsCultureNeutral = false;
            if (fileName != null && itemSpecToTaskitem.TryGetValue(fileName, out ITaskItem item))
            {
                culture = item.GetMetadata("Culture");
                // If 'WithCulture' is explicitly set to false, treat as 'culture-neutral' and keep the original name of the resource.
                // https://github.com/dotnet/msbuild/issues/3064
                treatAsCultureNeutral = ConversionUtilities.ValidBooleanFalse(item.GetMetadata("WithCulture"));
            }

            /*
                Actual implementation is in a static method called CreateManifestNameImpl.
                The reason is that CreateManifestName can't be static because it is an 
                override of a method declared in the base class, but its convenient 
                to expose a static version anyway for unittesting purposes.
            */
            return CreateManifestNameImpl(
                fileName,
                linkFileName,
                PrependCultureAsDirectory,
                rootNamespace,
                dependentUponFileName,
                culture,
                binaryStream,
                Log,
                treatAsCultureNeutral);
        }

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
        /// <param name="treatAsCultureNeutral">Whether to treat the current file as 'culture-neutral' and retain the culture in the name.</param>
        /// <returns>Returns the manifest name</returns>
        internal static string CreateManifestNameImpl(
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            TaskLoggingHelper log,
            bool treatAsCultureNeutral = false)
        {
            // Use the link file name if there is one, otherwise, fall back to file name.
            string embeddedFileName = FileUtilities.FixFilePath(linkFileName);
            if (string.IsNullOrEmpty(embeddedFileName))
            {
                embeddedFileName = FileUtilities.FixFilePath(fileName);
            }

            dependentUponFileName = FileUtilities.FixFilePath(dependentUponFileName);
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo(embeddedFileName, dependentUponFileName, treatAsCultureNeutral);

            // If the item has a culture override, respect that. 
            if (!string.IsNullOrEmpty(culture))
            {
                info.culture = culture;
            }

            var manifestName = StringBuilderCache.Acquire();
            if (binaryStream != null)
            {
                // Resource depends on a form. Now, get the form's class name fully 
                // qualified with a namespace.
                ExtractedClassName result = CSharpParserUtilities.GetFirstClassNameFullyQualified(binaryStream);

                if (result.IsInsideConditionalBlock)
                {
                    log?.LogWarningWithCodeFromResources("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective", dependentUponFileName, embeddedFileName);
                }

                if (!string.IsNullOrEmpty(result.Name))
                {
                    manifestName.Append(result.Name);

                    // Append the culture if there is one.        
                    if (!string.IsNullOrEmpty(info.culture))
                    {
                        manifestName.Append('.').Append(info.culture);
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
                    manifestName.Append(rootNamespace).Append('.');
                }

                // only strip extension for .resx and .restext files
                string sourceExtension = Path.GetExtension(info.cultureNeutralFilename);
                string directoryName = Path.GetDirectoryName(info.cultureNeutralFilename);

                // append the directory name
                manifestName.Append(MakeValidEverettIdentifier(directoryName));
                if (
                        string.Equals(sourceExtension, resxFileExtension, StringComparison.OrdinalIgnoreCase)
                        ||
                        string.Equals(sourceExtension, restextFileExtension, StringComparison.OrdinalIgnoreCase)
                        ||
                        string.Equals(sourceExtension, resourcesFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        manifestName.Append('.');
                    }

                    // append the file name without extension
                    manifestName.Append(Path.GetFileNameWithoutExtension(info.cultureNeutralFilename));

                    // Replace all '\' with '.'
                    manifestName.Replace(Path.DirectorySeparatorChar, '.');
                    manifestName.Replace(Path.AltDirectorySeparatorChar, '.');

                    // Append the culture if there is one.        
                    if (!string.IsNullOrEmpty(info.culture))
                    {
                        manifestName.Append('.').Append(info.culture);
                    }

                    // If the original extension was .resources, add it back
                    if (string.Equals(sourceExtension, resourcesFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        manifestName.Append(sourceExtension);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        manifestName.Append('.');
                    }

                    manifestName.Append(Path.GetFileName(info.cultureNeutralFilename));

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

            return StringBuilderCache.GetStringAndRelease(manifestName);
        }

        /// <summary>
        /// Return 'true' if this is a C# source file.
        /// </summary>
        /// <param name="fileName">Name of the candidate source file.</param>
        /// <returns>True, if this is a validate source file.</returns>
        protected override bool IsSourceFile(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return string.Equals(extension, SourceFileExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
