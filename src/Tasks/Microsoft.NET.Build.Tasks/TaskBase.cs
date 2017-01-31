// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public abstract class TaskBase : Task
    {
        //  Our tasks depend on both the NuGet APIs and Microsoft.Extensions.DependencyModel
        //  For .NET Framework, the NuGet APIs depend on version 6.0.4 of Newtonsoft.Json.
        //  However, Microsoft.Extensions.DependencyModel depends on version 9.0.1.
        //  This is a problem because MSBuild tasks can't supply binding redirects, so we
        //  can't redirect the reference that the NuGet APIs have on version 6.0.4 of
        //  Newtonsoft.Json to a higher version.
        //
        //  To fix this issue, we ship version 6.0.4 of Newtonsoft.Json in a subfolder and
        //  explicitly load it from there before any of the NuGet APIs are used.  This means
        //  that when the framework tries to bind the NuGet API references to Newtonsoft.Json,
        //  it will resolve to the 6.0.4 version that has already been loaded, instead of trying
        //  to load the 9.0.1 version that is in the base tasks folder and failing due to a version
        //  mismatch.  So the 9.0.1 and 6.0.4 versions will load side-by-side, and everything
        //  should work as long as we don't try to exchange types between the two.
#if NET46
        static TaskBase()
        {
            string assemblyFolder = Path.GetDirectoryName(typeof(TaskBase).Assembly.Location);
            string newtonSoft604Path = Path.Combine(assemblyFolder, "Newtonsoft.Json.6.0.4", "Newtonsoft.Json.dll");
            Assembly.LoadFrom(newtonSoft604Path);
        }
#endif

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
