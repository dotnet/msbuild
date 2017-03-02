//-----------------------------------------------------------------------
// <copyright file="SimpleTaskHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Simple Task implementation which will be used by the tests.</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Build.Framework;

    /// <summary>
    /// Simple Task implementation which will be used by the tests.
    /// </summary>
    public class SimpleTaskHelper : ITask
    {
        /// <summary>
        /// Initializes a new instance of the SimpleTaskHelper class.
        /// </summary>
        public SimpleTaskHelper()
        {
            this.TaskShouldError = false;
            this.TaskShouldThrowException = false;
            this.TaskOutput = null;
        }

        /// <summary>
        /// Gets or sets the BuildEngine callback.
        /// </summary>
        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the TaskHost callback.
        /// </summary>
        public ITaskHost HostObject
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the expected output to populate.
        /// </summary>
        public string ExpectedOutput
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether task should fail.
        /// </summary>
        public bool TaskShouldError
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether task should throw an exception.
        /// </summary>
        public bool TaskShouldThrowException
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets Gets or sets a value indicating whether task should sleep before exiting.
        /// </summary>
        public int SleepTime
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the output from the task which is set to the expected output.
        /// </summary>
        [Output]
        public string TaskOutput
        {
            get;
            set;
        }

        /// <summary>
        /// Execution of the task.
        /// </summary>
        /// <returns>True if the task succeeded else false.</returns>
        public bool Execute()
        {
            if (this.TaskShouldThrowException)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "Test exception from SimpleTaskHelper."));
            }

            if (this.TaskShouldError)
            {
                BuildErrorEventArgs eventArgs = new BuildErrorEventArgs("Subcategory", "666", "foo.cs", 1, 1, 1, 1, String.Format(CultureInfo.InvariantCulture, "Test error from SimpleTaskHelper."), "Helpme", "SimpleTaskHelper");
                this.BuildEngine.LogErrorEvent(eventArgs);
                return false;
            }

            if (this.SleepTime > 0)
            {
                System.Threading.Thread.Sleep(this.SleepTime);
            }

            return true;
        }
    }
}