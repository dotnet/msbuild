// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Build.Framework
{
    public class TaskExecutionContext
    {
        public TaskExecutionContext(string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            StartupDirectory = startupDirectory;
            BuildProcessEnvironment = buildProcessEnvironment;
            Culture = culture;
            UICulture = uiCulture;
        }

        public string StartupDirectory { get; }
        public Dictionary<string, string> BuildProcessEnvironment { get; }
        public CultureInfo Culture { get; }
        public CultureInfo UICulture { get; }
    }

    // TODO: move to own file
    public interface IConcurrentTask
    {
        void ConfigureForConcurrentExecution(TaskExecutionContext executionContext);
    }
}
