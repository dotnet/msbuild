// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A class containing an extension to BuildEventArgs
    /// </summary>
    internal static class BuildEventArgsExtension
    {
        /// <summary>
        /// Extension method to help our tests without adding shipping code.
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="args">The 'this' object</param>
        /// <param name="other">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(this BuildEventArgs args, BuildEventArgs other)
        {
            if (Object.ReferenceEquals(args, other))
            {
                return true;
            }

            if (Object.ReferenceEquals(other, null) || Object.ReferenceEquals(args, null))
            {
                return false;
            }

            if (args.GetType() != other.GetType())
            {
                return false;
            }

            if (args.Timestamp.Ticks != other.Timestamp.Ticks)
            {
                return false;
            }

            if (args.BuildEventContext != other.BuildEventContext)
            {
                return false;
            }

            if (!String.Equals(args.HelpKeyword, other.HelpKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.IsNullOrEmpty(args.Message))
            {
                // Just in case we're matching chk against ret or vice versa, make sure the message still registers as the same
                string fixedArgsMessage = args.Message.Replace("\r\nThis is an unhandled exception from a task -- PLEASE OPEN A BUG AGAINST THE TASK OWNER.", String.Empty);
                string fixedOtherMessage = other.Message.Replace("\r\nThis is an unhandled exception from a task -- PLEASE OPEN A BUG AGAINST THE TASK OWNER.", String.Empty);

                if (!String.Equals(fixedArgsMessage, fixedOtherMessage, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!String.Equals(args.SenderName, other.SenderName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (args.ThreadId != other.ThreadId)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Extension method to help our tests without adding shipping code.
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="args">The 'this' object</param>
        /// <param name="other">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(this BuildFinishedEventArgs args, BuildFinishedEventArgs other)
        {
            if (args.Succeeded != other.Succeeded)
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compares the value fields in the class to the passed in object and check to see if they are the same.
        /// </summary>
        /// <param name="obj">Object to compare to this instance</param>
        /// <returns>True if the value fields are identical, false if otherwise</returns>
        public static bool IsEquivalent(this BuildMessageEventArgs args, BuildMessageEventArgs other)
        {
            if (args.Importance != other.Importance)
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(this BuildErrorEventArgs args, BuildErrorEventArgs other)
        {
            if (args.ColumnNumber != other.ColumnNumber)
            {
                return false;
            }

            if (args.EndColumnNumber != other.EndColumnNumber)
            {
                return false;
            }

            if (args.LineNumber != other.LineNumber)
            {
                return false;
            }

            if (args.EndLineNumber != other.EndLineNumber)
            {
                return false;
            }

            if (!String.Equals(args.File, other.File, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.Code, other.Code, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.Subcategory, other.Subcategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(this BuildWarningEventArgs args, BuildWarningEventArgs other)
        {
            if (args.ColumnNumber != other.ColumnNumber)
            {
                return false;
            }

            if (args.EndColumnNumber != other.EndColumnNumber)
            {
                return false;
            }

            if (args.LineNumber != other.LineNumber)
            {
                return false;
            }

            if (args.EndLineNumber != other.EndLineNumber)
            {
                return false;
            }

            if (!String.Equals(args.File, other.File, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.Code, other.Code, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.Subcategory, other.Subcategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(ProjectStartedEventArgs args, ProjectStartedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetNames, other.TargetNames, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(ExternalProjectStartedEventArgs args, ExternalProjectStartedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetNames, other.TargetNames, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(ProjectFinishedEventArgs args, ProjectFinishedEventArgs other)
        {
            if (args.Succeeded != other.Succeeded)
            {
                return false;
            }

            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(ExternalProjectFinishedEventArgs args, ExternalProjectFinishedEventArgs other)
        {
            if (args.Succeeded != other.Succeeded)
            {
                return false;
            }

            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(TargetStartedEventArgs args, TargetStartedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetFile, other.TargetFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetName, other.TargetName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.ParentTarget, other.ParentTarget, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(TargetFinishedEventArgs args, TargetFinishedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetFile, other.TargetFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TargetName, other.TargetName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }


            if (!Object.ReferenceEquals(args.TargetOutputs, other.TargetOutputs))
            {
                // See if one is null, if so they are not equal
                if (args.TargetOutputs == null || other.TargetOutputs == null)
                {
                    return false;
                }

                List<string> argItemIncludes = new List<string>();
                foreach (TaskItem item in args.TargetOutputs)
                {
                    argItemIncludes.Add(item.ToString());
                }

                List<string> otherItemIncludes = new List<string>();
                foreach (TaskItem item in other.TargetOutputs)
                {
                    otherItemIncludes.Add(item.ToString());
                }

                argItemIncludes.Sort();
                otherItemIncludes.Sort();

                if (argItemIncludes.Count != otherItemIncludes.Count)
                {
                    return false;
                }

                // Since the lists are sorted each include must match
                for (int i = 0; i < argItemIncludes.Count; i++)
                {
                    if (!argItemIncludes[i].Equals(otherItemIncludes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(TaskStartedEventArgs args, TaskStartedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TaskFile, other.TaskFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TaskName, other.TaskName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }

        /// <summary>
        /// Compare this build event context with another object to determine 
        /// equality. This means the values inside the object are identical.
        /// </summary>
        /// <param name="obj">Object to compare to this object</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool IsEquivalent(TaskFinishedEventArgs args, TaskFinishedEventArgs other)
        {
            if (!String.Equals(args.ProjectFile, other.ProjectFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TaskFile, other.TaskFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(args.TaskName, other.TaskName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (args.Succeeded != other.Succeeded)
            {
                return false;
            }

            return ((BuildEventArgs)args).IsEquivalent(other);
        }
    }
}
