// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Microsoft.Build.Framework
{
    public class TaskExecutionContext
    {
        public string StartupDirectory { get; }
        public Dictionary<string, string> BuildProcessEnvironment { get; }
        public CultureInfo Culture { get; }
        public CultureInfo UICulture { get; }

        public TaskExecutionContext()
        {
            StartupDirectory = null;
            BuildProcessEnvironment = null;
            Culture = null;
            UICulture = null;
        }

        public TaskExecutionContext(string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            StartupDirectory = startupDirectory;
            BuildProcessEnvironment = buildProcessEnvironment;
            Culture = culture;
            UICulture = uiCulture;
        }
    }

    // TODO: move to own file
    public interface IConcurrentTask
    {
        void ConfigureForConcurrentExecution(TaskExecutionContext executionContext);
    }
}
