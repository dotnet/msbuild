using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// This task was created for https://github.com/microsoft/msbuild/issues/2036
    /// </summary>
    public class ReturnFailureWithoutLoggingErrorTask : Task
    {
        public override bool Execute()
        {
            return false;
        }
    }
}
