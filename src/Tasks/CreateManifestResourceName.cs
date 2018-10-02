// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for task that determines the appropriate manifest resource name to 
    /// assign to a given resx or other resource.
    /// </summary>
    public abstract class CreateManifestResourceName : TaskExtension
    {
        #region Properties

        private ITaskItem[] _resourceFiles;

        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Taskitem", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        protected Dictionary<string, ITaskItem> itemSpecToTaskitem = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Should the culture name be prepended to the manifest resource name as a directory?
        /// This is true by default.
        /// </summary>
        public bool PrependCultureAsDirectory { get; set; } = true;

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
        protected abstract string CreateManifestName
        (
            string fileName,
            string linkFileName,
            string rootNamespaceName,
            string dependentUponFileName,
            Stream binaryStream
        );

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
        internal bool Execute
        (
            CreateFileStream createFileStream
        )
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
                    string dependentUpon = resourceFile.GetMetadata(ItemMetadataNames.dependentUpon);

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
                        manifestName = CreateManifestName
                            (
                                fileName,
                                resourceFile.GetMetadata(ItemMetadataNames.targetPath),
                                RootNamespace,
                                isDependentOnSourceFile ? dependentUpon : null,
                                binaryStream
                            );
                    }

                    // Emit an item with our manifest name.
                    ManifestResourceNames[i] = new TaskItem(resourceFile) { ItemSpec = manifestName };

                    // Emit a new item preserving the itemSpec of the resourceFile, but with new metadata for manifest resource name
                    ResourceFilesWithManifestResourceNames[i] = new TaskItem(resourceFile);
                    ResourceFilesWithManifestResourceNames[i].SetMetadata("ManifestResourceName", manifestName);

                    // Add a LogicalName metadata to Non-Resx resources
                    // LogicalName isn't used for Resx resources because the ManifestResourceName metadata determines the filename of the 
                    // .resources file which then is used as the embedded resource manifest name                    
                    if (String.IsNullOrEmpty(ResourceFilesWithManifestResourceNames[i].GetMetadata("LogicalName")) &&
                        String.Equals(ResourceFilesWithManifestResourceNames[i].GetMetadata("Type"), "Non-Resx", StringComparison.OrdinalIgnoreCase))
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
        private static string MakeValidEverettSubFolderIdentifier(string subName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(subName, nameof(subName));

            if (subName.Length == 0)
            {
                return subName;
            }

            // give string length to avoid reallocations; +1 since the resulting string may be one char longer than the
            // original - if the first character is an invalid first identifier character but a valid subsequent one,
            // we prepend an underscore to it.
            var everettId = new StringBuilder(subName.Length + 1);

            // the first character has stronger restrictions than the rest
            if (!IsValidEverettIdFirstChar(subName[0]))
            {
                // if the first character is not even a valid subsequent character, replace it with an underscore
                if (!IsValidEverettIdChar(subName[0]))
                {
                    everettId.Append('_');
                }
                // if it is a valid subsequent character, prepend an underscore to it
                else
                {
                    everettId.Append('_');
                    everettId.Append(subName[0]);
                }
            }
            else
            {
                everettId.Append(subName[0]);
            }

            // process the rest of the subname
            for (int i = 1; i < subName.Length; i++)
            {
                if (!IsValidEverettIdChar(subName[i]))
                {
                    everettId.Append('_');
                }
                else
                {
                    everettId.Append(subName[i]);
                }
            }

            return everettId.ToString();
        }

        /// <summary>
        /// Make a folder name into an Everett-compatible identifier
        /// </summary>
        internal static string MakeValidEverettFolderIdentifier(string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(name, nameof(name));

            // give string length to avoid reallocations; +1 since the resulting string may be one char longer than the
            // original - if the name is a single underscore we add another underscore to it
            var everettId = new StringBuilder(name.Length + 1);

            // split folder name into subnames separated by '.', if any
            string[] subNames = name.Split('.');

            // convert each subname separately
            everettId.Append(MakeValidEverettSubFolderIdentifier(subNames[0]));

            for (int i = 1; i < subNames.Length; i++)
            {
                everettId.Append('.');
                everettId.Append(MakeValidEverettSubFolderIdentifier(subNames[i]));
            }

            // folder name cannot be a single underscore - add another underscore to it
            if (everettId.ToString() == "_")
            {
                everettId.Append('_');
            }

            return everettId.ToString();
        }

        /// <summary>
        /// This method is provided for compatibility with Everett which used to convert parts of resource names into
        /// valid identifiers
        /// </summary>
        public static string MakeValidEverettIdentifier(string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(name, nameof(name));

            var everettId = new StringBuilder(name.Length);

            // split the name into folder names
            string[] subNames = name.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // convert every folder name
            everettId.Append(MakeValidEverettFolderIdentifier(subNames[0]));

            for (int i = 1; i < subNames.Length; i++)
            {
                everettId.Append('.');
                everettId.Append(MakeValidEverettFolderIdentifier(subNames[i]));
            }

            return everettId.ToString();
        }

        #endregion
    }
}
