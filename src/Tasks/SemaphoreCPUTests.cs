using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Tasks
{
    class SemaphoreCPUTests : Task
    {
        public override bool Execute()
        {
            Log.LogMessageFromText($"Got {BuildEngine7.RequestCores(this, 3123890)} cores", Framework.MessageImportance.High);

            BuildEngine7.ReleaseCores(this, 50);
            Log.LogMessageFromText("Released some number of cores", Framework.MessageImportance.High);

            Log.LogMessageFromText($"Got {BuildEngine7.RequestCores(this, 10)} cores", Framework.MessageImportance.High);

            Log.LogMessageFromText($"Got {BuildEngine7.RequestCores(this, 30)} cores", Framework.MessageImportance.High);

            BuildEngine7.ReleaseCores(this, 2);
            Log.LogMessageFromText("Released some number of cores", Framework.MessageImportance.High);

            Log.LogMessageFromText($"Got {BuildEngine7.RequestCores(this, 12)} cores", Framework.MessageImportance.High);

            return !Log.HasLoggedErrors;
        }
    }
}
