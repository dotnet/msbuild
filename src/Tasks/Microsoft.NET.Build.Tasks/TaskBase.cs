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
        //  For .NET Framework, the NuGet APIs depend on a lower version of Newtonsoft.Json.
        //  However, Microsoft.Extensions.DependencyModel depends on a higher version.
        //  This is a problem because MSBuild tasks can't supply binding redirects, so we
        //  can't redirect the reference that the NuGet APIs have on the lower version of
        //  Newtonsoft.Json to a higher version.
        //
        //  To fix this issue, we ship the lower version of Newtonsoft.Json in a subfolder and
        //  explicitly load it from there before any of the NuGet APIs are used.  This means
        //  that when the framework tries to bind the NuGet API references to Newtonsoft.Json,
        //  it will resolve to the lower version that has already been loaded, instead of trying
        //  to load the higher version that is in the base tasks folder and failing due to a version
        //  mismatch.  So the higher and lower versions will load side-by-side, and everything
        //  should work as long as we don't try to exchange types between the two.
#if NET46
        static TaskBase()
        {
            string assemblyFolder = Path.GetDirectoryName(typeof(TaskBase).Assembly.Location);
            string newtonSoftSideBySidePath = Path.Combine(assemblyFolder, "SideBySide", "Newtonsoft.Json.dll");
            Assembly.LoadFrom(newtonSoftSideBySidePath);
        }
#endif

        private readonly DiagnosticsHelper _diagnostics;

        internal DiagnosticsHelper Diagnostics
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
