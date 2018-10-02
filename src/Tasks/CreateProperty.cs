// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Just a straight pass-through of the inputs through to the outputs.
    /// </summary>
    public class CreateProperty : TaskExtension
    {
        /// <summary>
        /// The in/out property value.
        /// </summary>
        /// <remarks>
        /// So ... why is this a string[] instead of a string?
        /// Basically if the project author passed in:
        /// 
        ///         CreateProperty Value="Clean;Build"
        ///             Output TaskParameter="Value" PropertyName="MyTargetsToBuild"
        ///         /CreateProperty
        /// 
        /// We need to respect the semicolon that he put in the value, and need to treat
        /// this exactly as if he had done:
        /// 
        ///         PropertyGroup
        ///             MyTargetsToBuild="Clean;Build"
        ///         /PropertyGroup
        /// 
        /// If we make this parameter a "string", then the engine will escape the 
        /// value on the way out from the task back to the engine, creating a property
        /// that is set to "Clean%3BBuild", which is not what the user wanted.
        /// </remarks>
        [Output]
        public string[] Value { get; set; }

        /// <summary>
        /// This is to fool MSBuild into not doing its little TLDA trick whereby even if 
        /// a target is up-to-date, it will still set the properties that were meant to
        /// be set using the CreateProperty task.  This is because MSBuild is smart enough
        /// to figure out the value of the output property without running the task.
        /// But if the input parameter is differently named than the output parameter,
        /// MSBuild can't be smart enough to do that.  This is an important scenario
        /// for people who want to know whether a particular target was up-to-date or not.
        /// </summary>
        [Output]
        public string[] ValueSetByTask => Value;

        /// <summary>
        /// Create the property. Since the input property is the same as the
        /// output property, this is rather easy.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (Value == null)
            {
                Value = Array.Empty<string>();
            }

            return true;
        }
    }
}
