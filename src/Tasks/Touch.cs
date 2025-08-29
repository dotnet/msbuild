﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class defines the touch task.
    /// </summary>
    public class Touch : TaskExtension, IIncrementalTask
    {
        private MessageImportance messageImportance;

        /// <summary>
        /// Forces a touch even if the file to be touched is read-only.
        /// </summary>
        public bool ForceTouch { get; set; }

        /// <summary>
        /// Creates the file if it doesn't exist.
        /// </summary>
        public bool AlwaysCreate { get; set; }

        /// <summary>
        /// Specifies a specific time other than current.
        /// </summary>
        public string Time { get; set; }

        /// <summary>
        /// File(s) to touch.
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// Output of this task - which files were touched.
        /// </summary>
        [Output]
        public ITaskItem[] TouchedFiles { get; set; }

        /// <summary>
        /// Importance: high, normal, low (default normal)
        /// </summary>
        public string Importance { get; set; }

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>When Question is true, skip touching the disk to avoid causing incremental issue.
        /// Unless the file doesn't exists, in which case, error out.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Implementation of the execute method.
        /// </summary>
        /// <returns></returns>
        internal bool ExecuteImpl(
            FileExists fileExists,
            FileCreate fileCreate,
            GetAttributes fileGetAttributes,
            SetAttributes fileSetAttributes,
            SetLastAccessTime fileSetLastAccessTime,
            SetLastWriteTime fileSetLastWriteTime)
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
            var touchedItems = new List<ITaskItem>();
            var touchedFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    TouchFile(
                        path,
                        touchDateTime,
                        fileExists,
                        fileCreate,
                        fileGetAttributes,
                        fileSetAttributes,
                        fileSetLastAccessTime,
                        fileSetLastWriteTime))
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
            TouchedFiles = touchedItems.ToArray();
            return retVal;
        }

        /// <summary>
        /// Run the task
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Importance))
            {
                messageImportance = MessageImportance.Normal;
            }
            else
            {
                if (!Enum.TryParse(Importance, ignoreCase: true, out messageImportance))
                {
                    Log.LogErrorWithCodeFromResources("Message.InvalidImportance", Importance);
                    return false;
                }
            }

            return ExecuteImpl(
                File.Exists,
                File.Create,
                File.GetAttributes,
                File.SetAttributes,
                File.SetLastAccessTime,
                File.SetLastWriteTime);
        }

        /// <summary>
        /// Helper method creates a file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileCreate"></param>
        /// <returns>"true" if the file was created.</returns>
        private bool CreateFile(
            string file,
            FileCreate fileCreate)
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
        /// <returns>"True" if the file was touched.</returns>
        private bool TouchFile(
            string file,
            DateTime dt,
            FileExists fileExists,
            FileCreate fileCreate,
            GetAttributes fileGetAttributes,
            SetAttributes fileSetAttributes,
            SetLastAccessTime fileSetLastAccessTime,
            SetLastWriteTime fileSetLastWriteTime)
        {
            if (!fileExists(file))
            {
                // If the file does not exist then we check if we need to create it.
                if (AlwaysCreate)
                {
                    if (FailIfNotIncremental)
                    {
                        Log.LogErrorFromResources("Touch.CreatingFile", file, "AlwaysCreate");
                        return false;
                    }
                    else
                    {
                        Log.LogMessageFromResources(messageImportance, "Touch.CreatingFile", file, "AlwaysCreate");
                    }

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

            // Ignore touching the disk when FailIfNotIncremental.
            if (FailIfNotIncremental)
            {
                Log.LogErrorFromResources("Touch.Touching", file);
                return false;
            }
            else
            {
                Log.LogMessageFromResources(messageImportance, "Touch.Touching", file);
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
            if (string.IsNullOrEmpty(Time))
            {
                return DateTime.Now;
            }

            return DateTime.Parse(Time, DateTimeFormatInfo.InvariantInfo);
        }
    }
}
