using System;
using System.IO;
using System.Text;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    internal static class ManifestNameCreator
    {
        public static string CreateNameForResource
        (
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            Func<Stream, ExtractedClassName> getFirstClassNameFullyQualified, // may be null if binaryStream is null. must not be null when stream is not
            TaskLoggingHelper log
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
            if (!String.IsNullOrEmpty(culture))
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
                    manifestName.Append(result.Name);

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
                string everettCompatibleDirectoryName = CreateManifestResourceName.MakeValidEverettIdentifier(Path.GetDirectoryName(info.cultureNeutralFilename));

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
                    if (String.Equals(sourceExtension, ".resources", StringComparison.OrdinalIgnoreCase))
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
