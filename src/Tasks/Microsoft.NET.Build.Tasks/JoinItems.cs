// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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

        //  Set to "Left" or "Right" to use that itemspec in results.  Leave blank to set itemspec based on the joined key value
        public string ItemSpecToUse { get; set; }


        //  LeftMetadata and RightMetadata: The metadata names to include in the result.  Specify "*" to include all metadata
        public string[] LeftMetadata { get; set; }

        public string[] RightMetadata { get; set; }


        [Output]
        public ITaskItem[] JoinResult
        {
            get; private set;
        }

        protected override void ExecuteCore()
        {
            bool useLeftItemSpec = false;
            bool useRightItemSpec = false;
            if (string.Equals(ItemSpecToUse, "Left", StringComparison.OrdinalIgnoreCase))
            {
                useLeftItemSpec = true;
            }
            else if (string.Equals(ItemSpecToUse, "Right", StringComparison.OrdinalIgnoreCase))
            {
                useRightItemSpec = true;
            }
            else if (!string.IsNullOrEmpty(ItemSpecToUse))
            {
                throw new BuildErrorException(Strings.InvalidItemSpecToUse, ItemSpecToUse);
            }

            bool useAllLeftMetadata = LeftMetadata != null && LeftMetadata.Length == 1 && LeftMetadata[0] == "*";
            bool useAllRightMetadata = RightMetadata != null && RightMetadata.Length == 1 && RightMetadata[0] == "*";

            JoinResult = Left.Join(Right,
                item => GetKeyValue(LeftKey, item),
                item => GetKeyValue(RightKey, item),
                (left, right) =>
                {
                    //  If including all metadata from left items and none from right items, just return left items directly
                    if (useAllLeftMetadata &&
                        (string.IsNullOrEmpty(LeftKey) || useLeftItemSpec) &&
                        (RightMetadata == null || RightMetadata.Length == 0))
                    {
                        return left;
                    }

                    //  If including all metadata from right items and none from left items, just return the right items directly
                    if (useAllRightMetadata &&
                        (string.IsNullOrEmpty(RightKey) || useRightItemSpec) &&
                        (LeftMetadata == null || LeftMetadata.Length == 0))
                    {
                        return right;
                    }

                    string resultItemSpec;
                    if (useLeftItemSpec)
                    {
                        resultItemSpec = left.ItemSpec;
                    }
                    else if (useRightItemSpec)
                    {
                        resultItemSpec = right.ItemSpec;
                    }
                    else
                    {
                        resultItemSpec = GetKeyValue(LeftKey, left);
                    }

                    var ret = new TaskItem(resultItemSpec);

                    //  Weird ordering here is to prefer left metadata in all cases, as CopyToMetadata doesn't overwrite any existing metadata
                    if (useAllLeftMetadata)
                    {
                        //  CopyMetadata adds an OriginalItemSpec, which we don't want.  So we subsequently remove it
                        left.CopyMetadataTo(ret);
                        ret.RemoveMetadata(MetadataKeys.OriginalItemSpec);
                    }

                    if (!useAllRightMetadata && RightMetadata != null)
                    {
                        foreach (string name in RightMetadata)
                        {
                            ret.SetMetadata(name, right.GetMetadata(name));
                        }
                    }

                    if (!useAllLeftMetadata && LeftMetadata != null)
                    {
                        foreach (string name in LeftMetadata)
                        {
                            ret.SetMetadata(name, left.GetMetadata(name));
                        }
                    }

                    if (useAllRightMetadata)
                    {
                        //  CopyMetadata adds an OriginalItemSpec, which we don't want.  So we subsequently remove it
                        right.CopyMetadataTo(ret);
                        ret.RemoveMetadata(MetadataKeys.OriginalItemSpec);
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
