﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
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
    public abstract class CreateManifestResourceName : TaskExtension
    {
        #region Properties
        internal const string resxFileExtension = ".resx";
        internal const string restextFileExtension = ".restext";
        internal const string resourcesFileExtension = ".resources";

        private ITaskItem[] _resourceFiles;

        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Taskitem", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        protected Dictionary<string, ITaskItem> itemSpecToTaskitem = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Should the culture name be prepended to the manifest resource name as a directory?
        /// This is true by default.
        /// </summary>
        public bool PrependCultureAsDirectory { get; set; } = true;

        public bool UseDependentUponConvention { get; set; }

        protected abstract string SourceFileExtension { get; }

        /// <summary>
        /// The possibly dependent resource files.
        /// </summary>
        [Required]
        public ITaskItem[] ResourceFiles
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_resourceFiles, nameof(ResourceFiles));
                return _resourceFiles;
            }
            set => _resourceFiles = value;
        }

        /// <summary>
        /// Rootnamespace to use for naming.
        /// </summary>
        public string RootNamespace { get; set; } = null;

        /// <summary>
        /// The resulting manifest names.
        /// </summary>
        /// <value></value>
        [Output]
        public ITaskItem[] ManifestResourceNames { get; private set; }

        /// <summary>
        /// The initial list of resource names, with additional metadata for manifest resource names
        /// </summary>
        [Output]
        public ITaskItem[] ResourceFilesWithManifestResourceNames { get; set; }

        #endregion

        /// <summary>
        /// Method in the derived class that composes the manifest name.
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The name of the file specified by the Link attribute.</param>
        /// <param name="rootNamespaceName">The root namespace (usually from the project file). May be null</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency. May be null</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <returns>Returns the manifest name</returns>
        protected abstract string CreateManifestName(
            string fileName,
            string linkFileName,
            string rootNamespaceName,
            string dependentUponFileName,
            Stream binaryStream);

        /// <summary>
        /// The derived class chooses whether this is a valid source file to work against.
        /// Usually, this is just a matter of looking at the file's extension.
        /// </summary>
        /// <param name="fileName">Name of the candidate source file.</param>
        /// <returns>True, if this is a validate source file.</returns>
        protected abstract bool IsSourceFile(string fileName);

        /// <summary>
        /// Given a file path, return a stream on top of that path.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">Access type</param>
        /// <returns>The FileStream</returns>
        private static Stream CreateFileStreamOverNewFileStream(string path, FileMode mode, FileAccess access)
        {
            return new FileStream(path, mode, access);
        }

        #region ITask Members
        /// <summary>
        /// Execute the task with delegate handlers.
        /// </summary>
        /// <param name="createFileStream">CreateFileStream delegate</param>
        /// <returns>True if task succeeded.</returns>
        internal bool Execute(
            CreateFileStream createFileStream)
        {
            ManifestResourceNames = new ITaskItem[ResourceFiles.Length];
            ResourceFilesWithManifestResourceNames = new ITaskItem[ResourceFiles.Length];

            bool success = true;
            int i = 0;

            // If Rootnamespace was null, then it wasn't set from the project resourceFile.
            // Empty namespaces are allowed.
            if (RootNamespace != null)
            {
                Log.LogMessageFromResources(MessageImportance.Low, "CreateManifestResourceName.RootNamespace", RootNamespace);
            }
            else
            {
                Log.LogMessageFromResources(MessageImportance.Low, "CreateManifestResourceName.NoRootNamespace");
            }


            foreach (ITaskItem resourceFile in ResourceFiles)
            {
                try
                {
                    string fileName = resourceFile.ItemSpec;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string dependentUpon = resourceFile.GetMetadata(ItemMetadataNames.dependentUpon);

                    string fileType = resourceFile.GetMetadata("Type");

                    // If it has "type" metadata and the value is "Resx"
                    // This value can be specified by the user, if not it will have been automatically assigned by the SplitResourcesByCulture target.
                    bool isResxFile = (!string.IsNullOrEmpty(fileType) && fileType == "Resx");

                    // If not, fall back onto the extension.
                    if (string.IsNullOrEmpty(fileType))
                    {
                        isResxFile = Path.GetExtension(fileName) == resxFileExtension;
                    }

                    // If opted into convention and no DependentUpon metadata and is a resx file, reference "<filename>.<ext>" (.cs or .vb) if it exists.
                    if (isResxFile && UseDependentUponConvention && string.IsNullOrEmpty(dependentUpon))
                    {
                        // Assume that by convention the expected file name is "<filename>.<ext>"
                        string conventionDependentUpon = Path.ChangeExtension(Path.GetFileName(fileName), SourceFileExtension);

                        // Verify that the file name didn't have a culture associated with it. Ex: "<filename>.<culture>.resx" If we don't strip the culture we look for TestComponent.de.cs, which we don't want.
                        if (resourceFile.GetMetadata("WithCulture") == "true")
                        {
                            string culture = resourceFile.GetMetadata("Culture");
                            if (!string.IsNullOrEmpty(culture))
                            {
                                int indexJustBeforeCulture = fileNameWithoutExtension.Length - culture.Length - 1;

                                // Strip the culture from the name, append the appropriate extension, now we have "<filename>.<ext>", this is the file resourceFile is dependent upon
                                conventionDependentUpon = fileNameWithoutExtension.Substring(0, indexJustBeforeCulture) + SourceFileExtension;
                            }
                        }

                        if (File.Exists(Path.Combine(Path.GetDirectoryName(fileName), conventionDependentUpon)))
                        {
                            dependentUpon = conventionDependentUpon;
                        }
                    }

                    // Pre-log some information.
                    bool isDependentOnSourceFile = !string.IsNullOrEmpty(dependentUpon) && IsSourceFile(dependentUpon);

                    if (isDependentOnSourceFile)
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "CreateManifestResourceName.DependsUpon", fileName, dependentUpon);
                    }
                    else
                    {
                        Log.LogMessageFromResources(MessageImportance.Low, "CreateManifestResourceName.DependsUponNothing", fileName);
                    }

                    // Create the manifest name.
                    Stream binaryStream = null;
                    string manifestName;

                    if (isDependentOnSourceFile)
                    {
                        string pathToDependent = Path.Combine(Path.GetDirectoryName(fileName), dependentUpon);
                        binaryStream = createFileStream(pathToDependent, FileMode.Open, FileAccess.Read);
                    }

                    // Put the task item into a dictionary so we can access it from a derived class quickly.
                    itemSpecToTaskitem[resourceFile.ItemSpec] = resourceFile;

                    // This "using" statement ensures that the "binaryStream" will be disposed once
                    // we're done with it.
                    using (binaryStream)
                    {
                        manifestName = CreateManifestName(
                                fileName,
                                resourceFile.GetMetadata(ItemMetadataNames.targetPath),
                                RootNamespace,
                                isDependentOnSourceFile ? dependentUpon : null,
                                binaryStream);
                    }

                    // Emit an item with our manifest name.
                    ManifestResourceNames[i] = new TaskItem(resourceFile) { ItemSpec = manifestName };

                    // Emit a new item preserving the itemSpec of the resourceFile, but with new metadata for manifest resource name
                    ResourceFilesWithManifestResourceNames[i] = new TaskItem(resourceFile);
                    ResourceFilesWithManifestResourceNames[i].SetMetadata("ManifestResourceName", manifestName);

                    // Add a LogicalName metadata to Non-Resx resources
                    // LogicalName isn't used for Resx resources because the ManifestResourceName metadata determines the filename of the 
                    // .resources file which then is used as the embedded resource manifest name                    
                    if (string.IsNullOrEmpty(ResourceFilesWithManifestResourceNames[i].GetMetadata("LogicalName")) &&
                        string.Equals(ResourceFilesWithManifestResourceNames[i].GetMetadata("Type"), "Non-Resx", StringComparison.OrdinalIgnoreCase))
                    {
                        ResourceFilesWithManifestResourceNames[i].SetMetadata("LogicalName", manifestName);
                    }

                    // Post-logging
                    Log.LogMessageFromResources(MessageImportance.Low, "CreateManifestResourceName.AssignedName", fileName, manifestName);
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    Log.LogErrorWithCodeFromResources("CreateManifestResourceName.Error", resourceFile.ItemSpec, e.Message);
                    success = false;
                }

                ++i;
            }

            return success;
        }

        /// <summary>
        /// Do the task's work.
        /// </summary>
        /// <returns>True if succeeded.</returns>
        public override bool Execute()
        {
            return Execute(CreateFileStreamOverNewFileStream);
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Is the character a valid first Everett identifier character?
        /// </summary>
        private static bool IsValidEverettIdFirstChar(char c)
        {
            return
                char.IsLetter(c) ||
                CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.ConnectorPunctuation;
        }

        /// <summary>
        /// Is the character a valid Everett identifier character?
        /// </summary>
        private static bool IsValidEverettIdChar(char c)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);

            return
                char.IsLetterOrDigit(c) ||
                cat == UnicodeCategory.ConnectorPunctuation ||
                cat == UnicodeCategory.NonSpacingMark ||
                cat == UnicodeCategory.SpacingCombiningMark ||
                cat == UnicodeCategory.EnclosingMark;
        }

        /// <summary>
        /// Make a folder subname into an Everett-compatible identifier 
        /// </summary>
        private static void MakeValidEverettSubFolderIdentifier(StringBuilder builder, string subName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(subName, nameof(subName));

            if (string.IsNullOrEmpty(subName)) { return; }

            // the first character has stronger restrictions than the rest
            if (IsValidEverettIdFirstChar(subName[0]))
            {
                builder.Append(subName[0]);
            }
            else
            {
                builder.Append('_');
                if (IsValidEverettIdChar(subName[0]))
                {
                    // if it is a valid subsequent character, prepend an underscore to it
                    builder.Append(subName[0]);
                }
            }

            // process the rest of the subname
            for (int i = 1; i < subName.Length; i++)
            {
                if (!IsValidEverettIdChar(subName[i]))
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(subName[i]);
                }
            }
        }

        /// <summary>
        /// Make a folder name into an Everett-compatible identifier
        /// </summary>
        internal static void MakeValidEverettFolderIdentifier(StringBuilder builder, string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(name, nameof(name));

            if (string.IsNullOrEmpty(name)) { return; }

            // store the original length for use later
            int length = builder.Length;

            // split folder name into subnames separated by '.', if any
            string[] subNames = name.Split(MSBuildConstants.DotChar);

            // convert each subname separately
            MakeValidEverettSubFolderIdentifier(builder, subNames[0]);

            for (int i = 1; i < subNames.Length; i++)
            {
                builder.Append('.');
                MakeValidEverettSubFolderIdentifier(builder, subNames[i]);
            }

            // folder name cannot be a single underscore - add another underscore to it
            if ((builder.Length - length) == 1 && builder[length] == '_')
            {
                builder.Append('_');
            }
        }

        /// <summary>
        /// This method is provided for compatibility with Everett which used to convert parts of resource names into
        /// valid identifiers
        /// </summary>
        public static string MakeValidEverettIdentifier(string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(name, nameof(name));
            if (string.IsNullOrEmpty(name)) { return name; }

            var everettId = new StringBuilder(name.Length);

            // split the name into folder names
            string[] subNames = name.Split(MSBuildConstants.ForwardSlashBackslash);

            // convert every folder name
            MakeValidEverettFolderIdentifier(everettId, subNames[0]);

            for (int i = 1; i < subNames.Length; i++)
            {
                everettId.Append('.');
                MakeValidEverettFolderIdentifier(everettId, subNames[i]);
            }

            return everettId.ToString();
        }

        #endregion
    }
}
