using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    public  class FailingBuilderTask : Task
    {
        public FailingBuilderTask()
            : base(null)
        { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public override bool Execute()
        {
            try
            {
                BuildEngine.BuildProjectFile(CurrentProject, new string[] { "CoreCompile" }, new Hashtable(), null);
            }
            catch
            {
            }
            return false;
        }

        [Required]
        public string CurrentProject { get; set; }
    }
}
