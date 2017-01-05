// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public abstract class TaskBase : Task
    {
        private readonly DiagnosticsHelper _diagnostics;

        public DiagnosticsHelper Diagnostics
        {
            get { return _diagnostics; }
        }

        [Output]
        public ITaskItem[] DiagnosticMessages
        {
            get { return _diagnostics.GetDiagnosticMessages(); }
        }

        // no reason for outside classes to derive from this class.
        internal TaskBase()
        {
            _diagnostics = new DiagnosticsHelper(Log);
        }

        public override bool Execute()
        {
            try
            {
                ExecuteCore();
            }
            catch (BuildErrorException e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        protected abstract void ExecuteCore();
    }
}
