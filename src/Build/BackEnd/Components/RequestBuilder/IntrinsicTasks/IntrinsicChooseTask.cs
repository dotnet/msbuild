using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.BackEnd.Components.RequestBuilder.IntrinsicTasks
{
    internal class IntrinsicChooseTask : IntrinsicTask
    {
        private readonly ProjectChooseTaskInstance _chooseTaskInstance;

        public IntrinsicChooseTask(ProjectChooseTaskInstance chooseTaskInstance, TargetLoggingContext loggingContext, ProjectInstance projectInstance, bool logTaskInputs) : base(loggingContext, projectInstance, logTaskInputs)
        {
            _chooseTaskInstance = chooseTaskInstance;
        }

        internal override void ExecuteTask(Lookup lookup)
        {
            List<ItemBucket> buckets = null;
            try
            {
                var stringsForBatching = new List<string>();
                foreach (var whenInstance in _chooseTaskInstance.WhenInstances)
                {
                    if (!string.IsNullOrEmpty(whenInstance.Condition))
                    {
                        stringsForBatching.Add(whenInstance.Condition);
                    }
                }

                buckets = BatchingEngine.PrepareBatchingBuckets(stringsForBatching, lookup,
                    _chooseTaskInstance.Location);

                foreach (var bucket in buckets)
                {
                    bool foundMatchingWhen = false;
                    foreach (var whenInstance in _chooseTaskInstance.WhenInstances)
                    {
                        bool condition = ConditionEvaluator.EvaluateCondition
                        (
                            whenInstance.Condition,
                            ParserOptions.AllowAll,
                            bucket.Expander,
                            ExpanderOptions.ExpandAll,
                            Project.Directory,
                            whenInstance.ConditionLocation,
                            LoggingContext.LoggingService,
                            LoggingContext.BuildEventContext,
                            FileSystems.Default);

                        if (condition)
                        {
                            foundMatchingWhen = true;
                            ExecuteChildren(bucket, whenInstance.Children);
                            break;
                        }
                    }

                    if (!foundMatchingWhen && _chooseTaskInstance.Otherwise != null)
                    {
                        ExecuteChildren(bucket, _chooseTaskInstance.Otherwise.Children);
                    }
                }
            }
            finally
            {
                if (buckets != null)
                {
                    foreach (var bucket in buckets)
                    {
                        bucket.LeaveScope();
                    }
                }
            }
        }

        private void ExecuteChildren(ItemBucket bucket, ICollection<ProjectTargetInstanceChild> children)
        {
            foreach (var child in children)
            {
                var task = IntrinsicTask.InstantiateTask(child, LoggingContext, Project, LogTaskInputs);
                task.ExecuteTask(bucket.Lookup);
            }
        }
    }
}
