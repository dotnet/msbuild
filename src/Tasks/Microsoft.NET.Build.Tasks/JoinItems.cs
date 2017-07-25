using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    public class JoinItems : TaskBase
    {
        [Required]
        public ITaskItem[] Left { get; set; }

        [Required]
        public ITaskItem[] Right { get; set; }


        //  LeftKey and RightKey: The metadata to join on.  If not set, then use the ItemSpec
        public string LeftKey { get; set; }

        public string RightKey { get; set; }


        //  LeftMetadata and RightMetadata: The metadata names to include in the result.  If not specified, all will be included
        public string[] LeftMetadata { get; set; }

        public string[] RightMetadata { get; set; }


        [Output]
        public ITaskItem[] JoinResult
        {
            get; private set;
        }

        protected override void ExecuteCore()
        {
            bool useAllLeftMetadata = LeftMetadata.Length == 1 && LeftMetadata[0] == "*";
            bool useAllRightMetadata = RightMetadata.Length == 1 && RightMetadata[0] == "*";

            JoinResult = Left.Join(Right,
                item => GetKeyValue(LeftKey, item),
                item => GetKeyValue(RightKey, item),
                (left, right) =>
                {
                    var ret = new TaskItem(GetKeyValue(LeftKey, left));


                    //  Weird ordering here is to prefer left metadata in all cases, as CopyToMetadata doesn't overwrite any existing metadata
                    if (useAllLeftMetadata)
                    {
                        left.CopyMetadataTo(ret);
                    }

                    if (!useAllRightMetadata)
                    {
                        foreach (string name in RightMetadata)
                        {
                            ret.SetMetadata(name, right.GetMetadata(name));
                        }
                    }

                    if (!useAllLeftMetadata)
                    {
                        foreach (string name in LeftMetadata)
                        {
                            ret.SetMetadata(name, left.GetMetadata(name));
                        }
                    }

                    if (useAllRightMetadata)
                    {
                        right.CopyMetadataTo(ret);
                    }

                    return (ITaskItem)ret;
                },
                StringComparer.OrdinalIgnoreCase).ToArray();            
        }

        static string GetKeyValue(string key, ITaskItem item)
        {
            if (string.IsNullOrEmpty(key))
            {
                return item.ItemSpec;
            }
            else
            {
                return item.GetMetadata(key);
            }
        }
    }
}
