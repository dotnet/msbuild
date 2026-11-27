// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    internal sealed class MockTask : ITask
    {
        internal MockTask()
            : this(true)
        {
        }

        internal MockTask(bool registerResources)
        {
            Log = new TaskLoggingHelper(this);

            if (registerResources)
            {
                RegisterResources();
            }
        }

        public IBuildEngine BuildEngine { get; set; }

        public ITaskHost HostObject { get; set; }

        public TaskLoggingHelper Log { get; }

        private void RegisterResources() => Log.TaskResources = new ResourceManager("Microsoft.Build.Framework.UnitTests.strings", typeof(MockTask).Assembly);

        public bool Execute() => true;
    }
}
