﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class represents a PropertyGroup intrinsic task.
    /// </summary>
    internal class PropertyGroupIntrinsicTask : IntrinsicTask
    {
        /// <summary>
        /// The original task instance data.
        /// </summary>
        private ProjectPropertyGroupTaskInstance _taskInstance;

        private readonly PropertyTrackingSetting _propertyTrackingSettings;

        /// <summary>
        /// Create a new PropertyGroup task.
        /// </summary>
        /// <param name="taskInstance">The task instance data</param>
        /// <param name="loggingContext">The logging context</param>
        /// <param name="projectInstance">The project instance</param>
        /// <param name="logTaskInputs">Flag to determine whether or not to log task inputs.</param>
        public PropertyGroupIntrinsicTask(ProjectPropertyGroupTaskInstance taskInstance, TargetLoggingContext loggingContext, ProjectInstance projectInstance, bool logTaskInputs)
            : base(loggingContext, projectInstance, logTaskInputs)
        {
            _taskInstance = taskInstance;
            _propertyTrackingSettings = (PropertyTrackingSetting)Traits.Instance.LogPropertyTracking;
        }

        /// <summary>
        /// Execute a PropertyGroup element, including each child property
        /// </summary>
        /// <param name="lookup">The lookup use for evaluation and as a destination for these properties.</param>
        internal override void ExecuteTask(Lookup lookup)
        {
            foreach (ProjectPropertyGroupTaskPropertyInstance property in _taskInstance.Properties)
            {
                List<ItemBucket> buckets = null;

                try
                {
                    // Find all the metadata references in order to create buckets
                    List<string> parameterValues = new List<string>();
                    GetBatchableValuesFromProperty(parameterValues, property);
                    buckets = BatchingEngine.PrepareBatchingBuckets(parameterValues, lookup, property.Location, LoggingContext);

                    // "Execute" each bucket
                    foreach (ItemBucket bucket in buckets)
                    {
                        bool condition = ConditionEvaluator.EvaluateCondition(
                            property.Condition,
                            ParserOptions.AllowAll,
                            bucket.Expander,
                            ExpanderOptions.ExpandAll,
                            Project.Directory,
                            property.ConditionLocation,
                            FileSystems.Default,
                            LoggingContext);

                        if (condition)
                        {
                            // Check for a reserved name now, so it fails right here instead of later when the property eventually reaches
                            // the outer scope.
                            ProjectErrorUtilities.VerifyThrowInvalidProject(
                                !ReservedPropertyNames.IsReservedProperty(property.Name),
                                property.Location,
                                "CannotModifyReservedProperty",
                                property.Name);

                            bucket.Expander.PropertiesUseTracker.CurrentlyEvaluatingPropertyElementName = property.Name;
                            bucket.Expander.PropertiesUseTracker.PropertyReadContext =
                                PropertyReadContext.PropertyEvaluation;

                            string evaluatedValue = bucket.Expander.ExpandIntoStringLeaveEscaped(property.Value, ExpanderOptions.ExpandAll, property.Location);
                            bucket.Expander.PropertiesUseTracker.CheckPreexistingUndefinedUsage(property, evaluatedValue, LoggingContext);

                            PropertyTrackingUtils.LogPropertyAssignment(
                                _propertyTrackingSettings,
                                property.Name,
                                evaluatedValue,
                                property.Location,
                                Project.GetProperty(property.Name)?.EvaluatedValue ?? null,
                                LoggingContext);

                            if (LogTaskInputs && !LoggingContext.LoggingService.OnlyLogCriticalEvents)
                            {
                                LoggingContext.LogComment(MessageImportance.Low, "PropertyGroupLogMessage", property.Name, evaluatedValue);
                            }

                            bucket.Lookup.SetProperty(ProjectPropertyInstance.Create(property.Name, evaluatedValue, property.Location, Project.IsImmutable));
                            LoggingContext.ProcessPropertyWrite(new PropertyWriteInfo(property.Name, string.IsNullOrEmpty(evaluatedValue), property.Location));
                        }
                    }
                }
                finally
                {
                    if (buckets != null)
                    {
                        // Propagate the property changes to the bucket above
                        foreach (ItemBucket bucket in buckets)
                        {
                            bucket.LeaveScope();
                            // We are now done processing this property - so no need to pop its previous context.
                            bucket.Expander.PropertiesUseTracker.ResetPropertyReadContext(pop: false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds batchable parameters from a property element into the list. If the property element was
        /// a task, these would be its raw parameter values.
        /// </summary>
        /// <param name="parameterValues">The list which will contain the batchable values.</param>
        /// <param name="property">The property from which to take the values.</param>
        private void GetBatchableValuesFromProperty(List<string> parameterValues, ProjectPropertyGroupTaskPropertyInstance property)
        {
            AddIfNotEmptyString(parameterValues, property.Value);
            AddIfNotEmptyString(parameterValues, property.Condition);
        }
    }
}
