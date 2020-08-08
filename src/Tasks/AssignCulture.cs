// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// <para>Class: AssignCulture</para>
    /// <para>
    /// This task takes a list of resource file names and sets an attribute that
    /// contains the culture name embedded in the file name:
    /// </para>
    /// <para>     MyResources.fr.resx     ==> Culture='fr'</para>
    /// <para>The task can also return a list of "Culture-neutral" file names, like:</para>
    /// <para>     MyGlyph.fr.bmp          ==> MyGlyph.bmp [Culture='fr']</para>
    /// <para>This is because embedded resources are referred to this way.</para>
    /// <para>
    /// There are plenty of corner cases with this task. See the unit test for
    /// more details.
    /// </para>
    /// </summary>
    public class AssignCulture : TaskExtension
    {
        #region Properties

        /// <summary>
        /// The incoming list of files to assign a culture to.
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// <para>
        /// This outgoing list of files is exactly the same as the incoming Files
        /// list except that an attribute name "Culture" will have been added if 
        /// the particular file name is in the form:
        /// </para>
        /// <para>     MyResource.&lt;any-valid-culture-id&gt;.resx</para>
        /// <para>The value of Culture will be "&lt;any-valid-culture-id&gt;".</para>
        /// <para>
        /// If the incoming item from Files already has a Culture attribute then
        /// that original attribute is used instead.
        /// </para>
        /// </summary>
        [Output]
        public ITaskItem[] AssignedFiles { get; private set; }

        /// <summary>
        /// <para>
        /// This is a subset of AssignedFiles that has all of the items that
        /// ended up have a Culture assigned to them. This includes items that
        /// already had a Culture in the incoming Files list as well as items
        /// that were assigned a Culture because they had a valid culture ID
        /// embedded in their file name.
        /// </para>
        /// <para>The following is always true:</para>
        /// <para>     AssignedFiles = AssignedFilesWithCulture + AssignedFilesWithNoCulture</para>
        /// </summary>
        [Output]
        public ITaskItem[] AssignedFilesWithCulture { get; private set; }

        /// <summary>
        /// <para>
        /// This is a subset of AssignedFiles that has all of the items that
        /// ended up with no Culture assigned to them. 
        /// </para>
        /// <para>The following is always true:</para>
        /// <para>     AssignedFiles = AssignedFilesWithCulture + AssignedFilesWithNoCulture</para>
        /// </summary>
        [Output]
        public ITaskItem[] AssignedFilesWithNoCulture { get; private set; }

        /// <summary>
        /// <para>
        /// This list has the same number of items as the Files list or the
        /// AssignedFiles list.
        /// </para>
        /// <para>
        /// Items in this list have the file name from Files or AssignedFiles
        /// but with the culture stripped if it was embedded in the file name.
        /// </para>
        /// <para>So for example, if the incoming item in Files was:</para>
        /// <para>     MyBitmap.fr.bmp</para>
        /// <para>then the corresponding file in CultureNeutralAssignedFiles will be:</para>
        /// <para>     MyBitmap.bmp</para>
        /// <para>
        /// The culture will only be stripped if it is a valid culture identifier.
        /// So for example,
        /// </para>
        /// <para>     MyCrazyFile.XX.txt</para>
        /// <para>will result in exactly the same file name:</para>
        /// <para>     MyCrazyFile.XX.txt</para>
        /// <para>because 'XX' is not a valid culture identifier.</para>
        /// </summary>
        [Output]
        public ITaskItem[] CultureNeutralAssignedFiles { get; private set; }

        #endregion

        #region ITask Members
        
        /// <summary>
        /// Execute.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            AssignedFiles = new ITaskItem[Files.Length];
            CultureNeutralAssignedFiles = new ITaskItem[Files.Length];
            var cultureList = new List<ITaskItem>();
            var noCultureList = new List<ITaskItem>();

            bool retValue = true;

            for (int i = 0; i < Files.Length; ++i)
            {
                try
                {
                    AssignedFiles[i] = new TaskItem(Files[i]);

                    string dependentUpon = AssignedFiles[i].GetMetadata(ItemMetadataNames.dependentUpon);
                    Culture.ItemCultureInfo info = Culture.GetItemCultureInfo
                        (
                            AssignedFiles[i].ItemSpec,
                            dependentUpon
                        );

                    if (!string.IsNullOrEmpty(info.culture))
                    {
                        AssignedFiles[i].SetMetadata("Culture", info.culture);
                        AssignedFiles[i].SetMetadata("WithCulture", "true");
                        cultureList.Add(AssignedFiles[i]);
                    }
                    else
                    {
                        noCultureList.Add(AssignedFiles[i]);
                        AssignedFiles[i].SetMetadata("WithCulture", "false");
                    }

                    CultureNeutralAssignedFiles[i] =
                        new TaskItem(AssignedFiles[i]) { ItemSpec = info.cultureNeutralFilename };

                    Log.LogMessageFromResources
                    (
                        MessageImportance.Low,
                        "AssignCulture.Comment",
                        AssignedFiles[i].GetMetadata("Culture"),
                        AssignedFiles[i].ItemSpec
                    );
                }
                catch (ArgumentException e)
                {
                    Log.LogErrorWithCodeFromResources("AssignCulture.CannotExtractCulture", Files[i].ItemSpec, e.Message);
                    retValue = false;
                }
#if DEBUG
                catch (Exception e)
                {
                    Debug.Assert(false, "Unexpected exception in AssignCulture.Execute. " + 
                        "Please log a MSBuild bug specifying the steps to reproduce the problem. " + 
                        e.Message);
                    throw;
                }
#endif
            }

            AssignedFilesWithCulture = cultureList.ToArray();
            AssignedFilesWithNoCulture = noCultureList.ToArray();

            return retValue;
        }

        #endregion
    }
}
