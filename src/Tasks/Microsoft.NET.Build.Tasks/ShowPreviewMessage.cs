// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Provides a localizable mechanism for logging messages with different levels of importance from the SDK targets.
    /// </summary>
    public class ShowPreviewMessage : TaskBase
    {
        protected override void ExecuteCore()
        {
            const string previewMessageKey = "Microsoft.NET.Build.Tasks.DisplayPreviewMessageKey";

            object messageDisplayed = 
                BuildEngine4.GetRegisteredTaskObject(previewMessageKey, RegisteredTaskObjectLifetime.Build);
            if (messageDisplayed == null)
            {
                Log.LogMessage(MessageImportance.High, Strings.UsingPreviewSdk);

                BuildEngine4.RegisterTaskObject(
                    previewMessageKey,
                    new object(),
                    RegisteredTaskObjectLifetime.Build,
                    true);
            }
        }
    }
}
