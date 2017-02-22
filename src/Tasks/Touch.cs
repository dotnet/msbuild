// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class defines the touch task.
    /// </summary>
    public class Touch : TaskExtension
    {
        private bool _forceTouch;
        private bool _alwaysCreate;
        private string _specificTime;
        private ITaskItem[] _files;
        private ITaskItem[] _touchedFiles;

        //-----------------------------------------------------------------------------------
        // Constructor
        //-----------------------------------------------------------------------------------
        public Touch()
        {
            _alwaysCreate = false;
            _forceTouch = false;
        }

        //-----------------------------------------------------------------------------------
        // Property:  force touch even if the file to be touched is read-only
        //-----------------------------------------------------------------------------------
        public bool ForceTouch
        {
            get { return _forceTouch; }
            set { _forceTouch = value; }
        }

        //-----------------------------------------------------------------------------------
        // Property:  create the file if it doesn't exist
        //-----------------------------------------------------------------------------------
        public bool AlwaysCreate
        {
            get { return _alwaysCreate; }
            set { _alwaysCreate = value; }
        }

        //-----------------------------------------------------------------------------------
        // Property:  specifies a specific time other than current 
        //-----------------------------------------------------------------------------------
        public string Time
        {
            get { return _specificTime; }
            set { _specificTime = value; }
        }

        //-----------------------------------------------------------------------------------
        // Property:  file(s) to touch
        //-----------------------------------------------------------------------------------
        [Required]
        public ITaskItem[] Files
        {
            get { return _files; }
            set { _files = value; }
        }

        //-----------------------------------------------------------------------------------
        // Output of this task -- which files were touched
        //-----------------------------------------------------------------------------------
        [Output]
        public ITaskItem[] TouchedFiles
        {
            get { return _touchedFiles; }
            set { _touchedFiles = value; }
        }

        /// <summary>
        /// Implementation of the execute method.
        /// </summary>
        /// <returns></returns>
        internal bool ExecuteImpl
        (
            FileExists fileExists,
            FileCreate fileCreate,
            GetAttributes fileGetAttributes,
            SetAttributes fileSetAttributes,
            SetLastAccessTime fileSetLastAccessTime,
            SetLastWriteTime fileSetLastWriteTime

        )
        {
            // See what time we are touching all files to
            DateTime touchDateTime;
            try
            {
                touchDateTime = GetTouchDateTime();
            }
            catch (FormatException e)
            {
                Log.LogErrorWithCodeFromResources("Touch.TimeSyntaxIncorrect", e.Message);
                return false;
            }

            // Go through all files and touch 'em
            bool retVal = true;
            ArrayList touchedItems = new ArrayList();
            HashSet<string> touchedFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ITaskItem file in Files)
            {
                string path = FileUtilities.FixFilePath(file.ItemSpec);
                // For speed, eliminate duplicates caused by poor targets authoring
                if (touchedFilesSet.Contains(path))
                {
                    continue;
                }

                // Touch the file.  If the file was touched successfully then add it to our array of 
                // touched items. 
                if
                (
                    TouchFile
                    (
                        path,
                        touchDateTime,
                        fileExists,
                        fileCreate,
                        fileGetAttributes,
                        fileSetAttributes,
                        fileSetLastAccessTime,
                        fileSetLastWriteTime

                    )
                )
                {
                    touchedItems.Add(file);
                }
                else
                {
                    retVal = false;
                }

                // Add even on failure to avoid reattempting
                touchedFilesSet.Add(path);
            }

            // Now, set the property that indicates which items we touched.  Note that we
            // touch all the items 
            TouchedFiles = (ITaskItem[])touchedItems.ToArray(typeof(ITaskItem));
            return retVal;
        }

        /// <summary>
        /// Run the task
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            return ExecuteImpl
            (
                new FileExists(File.Exists),
                new FileCreate(File.Create),
                new GetAttributes(File.GetAttributes),
                new SetAttributes(File.SetAttributes),
                new SetLastAccessTime(File.SetLastAccessTime),
                new SetLastWriteTime(File.SetLastWriteTime)
            );
        }

        /// <summary>
        /// Helper method creates a file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileCreate"></param>
        /// <returns>"true" if the file was created.</returns>
        private bool CreateFile
        (
            string file,
            FileCreate fileCreate
        )
        {
            try
            {
                using (FileStream fs = fileCreate(file))
                {
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("Touch.CannotCreateFile", file, e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Helper method touches a file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="dt"></param>
        /// <param name="fileExists"></param>
        /// <param name="fileCreate"></param>
        /// <param name="fileGetAttributes"></param>
        /// <param name="fileSetAttributes"></param>
        /// <param name="fileSetLastAccessTime"></param>
        /// <param name="fileSetLastWriteTime"></param>
        /// <returns>"True" if the file was touched.</returns>
        private bool TouchFile
        (
            string file,
            DateTime dt,
            FileExists fileExists,
            FileCreate fileCreate,
            GetAttributes fileGetAttributes,
            SetAttributes fileSetAttributes,
            SetLastAccessTime fileSetLastAccessTime,
            SetLastWriteTime fileSetLastWriteTime
        )
        {
            if (!fileExists(file))
            {
                // If the file does not exist then we check if we need to create it.
                if (AlwaysCreate)
                {
                    Log.LogMessageFromResources(MessageImportance.Normal, "Touch.CreatingFile", file, "AlwaysCreate");
                    if (!CreateFile(file, fileCreate))
                    {
                        return false;
                    }
                }
                else
                {
                    Log.LogErrorWithCodeFromResources("Touch.FileDoesNotExist", file);
                    return false;
                }
            }
            else
            {
                Log.LogMessageFromResources(MessageImportance.Normal, "Touch.Touching", file);
            }

            // If the file is read only then we must either issue an error, or, if the user so 
            // specified, make the file temporarily not read only.
            bool needToRestoreAttributes = false;
            FileAttributes faOriginal = fileGetAttributes(file);
            if ((faOriginal & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                if (ForceTouch)
                {
                    try
                    {
                        FileAttributes faNew = (faOriginal & ~FileAttributes.ReadOnly);
                        fileSetAttributes(file, faNew);
                        needToRestoreAttributes = true;
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        Log.LogErrorWithCodeFromResources("Touch.CannotMakeFileWritable", file, e.Message);
                        return false;
                    }
                }
            }

            // Do the actual touch operation
            bool retVal = true;
            try
            {
                fileSetLastAccessTime(file, dt);
                fileSetLastWriteTime(file, dt);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                Log.LogErrorWithCodeFromResources("Touch.CannotTouch", file, e.Message);
                return false;
            }
            finally
            {
                if (needToRestoreAttributes)
                {
                    // Attempt to restore the attributes.  If we fail here, then there is 
                    // not much we can do.
                    try
                    {
                        fileSetAttributes(file, faOriginal);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        Log.LogErrorWithCodeFromResources("Touch.CannotRestoreAttributes", file, e.Message);
                        retVal = false;
                    }
                }
            }

            return retVal;
        }

        //-----------------------------------------------------------------------------------
        // Helper methods
        //-----------------------------------------------------------------------------------
        private DateTime GetTouchDateTime()
        {
            // If we have a specified time to which files need to be built then attempt
            // to parse it from the Time property.  Otherwise, we get the current time.
            if (Time == null || Time.Length == 0)
                return DateTime.Now;
            else
                return DateTime.Parse(Time, DateTimeFormatInfo.InvariantInfo);
        }
    }
}
