using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class LoadAssetsFile : TaskBase
    {
        /// <summary>
        /// The assets file to process
        /// </summary>
        public string ProjectAssetsFile
        {
            get; set;
        }

        protected override void ExecuteCore()
        {
            var lockFile = new LockFileCache(this).GetLockFile(ProjectAssetsFile);
        }
    }
}
