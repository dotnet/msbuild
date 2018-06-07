// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Evaluates a ProjectRootElement into a Project.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using ObjectModel = System.Collections.ObjectModel;
using Microsoft.Build.Collections;
using Microsoft.Build.BackEnd;
using System.Globalization;
using System.Threading;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Evaluation.Context;
#if MSBUILDENABLEVSPROFILING 
using Microsoft.VisualStudio.Profiler;
#endif

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using Constants = Microsoft.Build.Internal.Constants;
using EngineFileUtilities = Microsoft.Build.Internal.EngineFileUtilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;
#endif

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Evaluates a ProjectRootElement, updating the fresh Project.Data passed in.
    /// Handles evaluating conditions, expanding expressions, and building up the 
    /// lists of applicable properties, items, and itemdefinitions, as well as gathering targets and tasks
    /// and creating a TaskRegistry from the using tasks.
    /// </summary>
    /// <typeparam name="P">The type of properties to produce.</typeparam>
    /// <typeparam name="I">The type of items to produce.</typeparam>
    /// <typeparam name="M">The type of metadata on those items.</typeparam>
    /// <typeparam name="D">The type of item definitions to be produced.</typeparam>
    /// <remarks>
    /// This class could be improved to do partial (minimal) reevaluation: at present we wipe all state and start over.
    /// </remarks>
    internal class Evaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        /// <summary>
        /// Character used to split InitialTargets and DefaultTargets lists
        /// </summary>
        private static readonly char[] s_splitter = new char[] { ';' };

        /// <summary>
        /// Locals types names. We only have these because 'Built In' has a space,
        /// else we would use LocalsTypes enum names.
        /// Note: This should match LocalsTypes enum.
        /// </summary>
        private static readonly string[] s_localsTypesNames = new string[]
        {
                "Project",
                "Built In",
                "Environment",
                "Toolset",
                "SubToolset",
                "Global",
                "EvaluateExpression",
                "EvaluateCondition",
                "ToolsVersion",
                "Properties",
                "ItemDefinitions",
                "Items"
        };

        /// <summary>
        /// Expander for evaluating conditions
        /// </summary>
        private readonly Expander<P, I> _expander;

        /// <summary>
        /// Data containing the ProjectRootElement to evaluate and the slots for
        /// items, properties, etc originating from the evaluation.
        /// </summary>
        private readonly IEvaluatorData<P, I, M, D> _data;

        /// <summary>
        /// List of ProjectItemElement's traversing into imports.
        /// Gathered during the first pass to avoid traversing again.
        /// </summary>
        private readonly List<ProjectItemGroupElement> _itemGroupElements;

        /// <summary>
        /// List of ProjectItemDefinitionElement's traversing into imports.
        /// Gathered during the first pass to avoid traversing again.
        /// </summary>
        private readonly List<ProjectItemDefinitionGroupElement> _itemDefinitionGroupElements;

        /// <summary>
        /// List of ProjectUsingTaskElement's traversing into imports.
        /// Gathered during the first pass to avoid traversing again.
        /// Key is the directory of the file importing the usingTask, which is needed
        /// to handle any relative paths in the usingTask.
        /// </summary>
        private readonly List<Pair<string, ProjectUsingTaskElement>> _usingTaskElements;

        /// <summary>
        /// List of ProjectTargetElement's traversing into imports. 
        /// Gathered during the first pass to avoid traversing again.
        /// </summary>
        private readonly List<ProjectTargetElement> _targetElements;

        /// <summary>
        /// Paths to imports already seen and where they were imported from; used to flag duplicate imports
        /// </summary>
        private readonly Dictionary<string, ProjectImportElement> _importsSeen;

        /// <summary>
        /// Depth first collection of InitialTargets strings declared in the main 
        /// Project and all its imported files, split on semicolons.
        /// </summary>
        private readonly List<string> _initialTargetsList;

        /// <summary>
        /// Dictionary of project full paths and a boolean that indicates whether at least one 
        /// of their targets has the "Returns" attribute set.  
        /// </summary>
        private readonly Dictionary<ProjectRootElement, NGen<bool>> _projectSupportsReturnsAttribute;

        /// <summary>
        /// The Project Xml to be evaluated.
        /// </summary>
        private readonly ProjectRootElement _projectRootElement;

        /// <summary>
        /// The item factory used to create items from Xml.
        /// </summary>
        private readonly IItemFactory<I, I> _itemFactory;

        /// <summary>
        /// Load settings, such as whether to ignore missing imports.
        /// </summary>
        private readonly ProjectLoadSettings _loadSettings;

        /// <summary>
        /// The maximum number of nodes to report for evaluation.
        /// </summary>
        private readonly int _maxNodeCount;

        /// <summary>
        /// The <see cref="ISdkResolverService"/> to use.
        /// </summary>
        private readonly ISdkResolverService _sdkResolverService;

        /// <summary>
        /// The current build submission ID.
        /// </summary>
        private readonly int _submissionId;
        
        private readonly EvaluationContext _evaluationContext;

        /// <summary>
        /// The environment properties with which evaluation should take place.
        /// </summary>
        private readonly PropertyDictionary<ProjectPropertyInstance> _environmentProperties;

        /// <summary>
        /// The cache to consult for any imports that need loading.
        /// </summary>
        private readonly ProjectRootElementCache _projectRootElementCache;

        /// <summary>
        /// The logging context to be used and piped down throughout evaluation
        /// </summary>
        private EvaluationLoggingContext _evaluationLoggingContext;

        private bool _logProjectImportedEvents = true;

        /// <summary>
        /// The search paths are machine specific and should not change during builds
        /// </summary>
        private static readonly EngineFileUtilities.IOCache _fallbackSearchPathsCache = new EngineFileUtilities.IOCache();

        private readonly EvaluationProfiler _evaluationProfiler;

        /// <summary>
        /// Private constructor called by the static Evaluate method.
        /// </summary>
        private Evaluator(
            IEvaluatorData<P, I, M, D> data,
            ProjectRootElement projectRootElement,
            ProjectLoadSettings loadSettings,
            int maxNodeCount,
            PropertyDictionary<ProjectPropertyInstance> environmentProperties,
            IItemFactory<I, I> itemFactory,
            IToolsetProvider toolsetProvider,
            ProjectRootElementCache projectRootElementCache,
            ISdkResolverService sdkResolverService,
            int submissionId,
            EvaluationContext evaluationContext,
            bool profileEvaluation)
        {
            ErrorUtilities.VerifyThrowInternalNull(data, nameof(data));
            ErrorUtilities.VerifyThrowInternalNull(projectRootElementCache, nameof(projectRootElementCache));

            // Create containers for the evaluation results
            data.InitializeForEvaluation(toolsetProvider);

            _expander = new Expander<P, I>(data, data);

            // This setting may change after the build has started, therefore if the user has not set the property to true on the build parameters we need to check to see if it is set to true on the environment variable.
            _expander.WarnForUninitializedProperties = BuildParameters.WarnOnUninitializedProperty || Traits.Instance.EscapeHatches.WarnOnUninitializedProperty;
            _data = data;
            _itemGroupElements = new List<ProjectItemGroupElement>();
            _itemDefinitionGroupElements = new List<ProjectItemDefinitionGroupElement>();
            _usingTaskElements = new List<Pair<string, ProjectUsingTaskElement>>();
            _targetElements = new List<ProjectTargetElement>();
            _importsSeen = new Dictionary<string, ProjectImportElement>(StringComparer.OrdinalIgnoreCase);
            _initialTargetsList = new List<string>();
            _projectSupportsReturnsAttribute = new Dictionary<ProjectRootElement, NGen<bool>>();
            _projectRootElement = projectRootElement;
            _loadSettings = loadSettings;
            _maxNodeCount = maxNodeCount;
            _environmentProperties = environmentProperties;
            _itemFactory = itemFactory;
            _projectRootElementCache = projectRootElementCache;
            _sdkResolverService = sdkResolverService;
            _submissionId = submissionId;
            _evaluationContext = evaluationContext ?? EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated);
            _evaluationProfiler = new EvaluationProfiler(profileEvaluation);
        }

        /// <summary>
        /// Delegate passed to methods to provide basic expression evaluation
        /// ability, without having a language service.
        /// </summary>
        internal delegate string ExpandExpression(string unexpandedString);

        /// <summary>
        /// Delegate passed to methods to provide basic expression evaluation
        /// ability, without having a language service.
        /// </summary>
        internal delegate bool EvaluateConditionalExpression(string unexpandedExpression);

        /// <summary>
        /// Enumeration for locals types
        /// Note: This should match LocalsTypesNames
        /// </summary>
        private enum LocalsTypes : int
        {
            /// <summary>
            /// Project,
            /// </summary>
            Project,

            /// <summary>
            /// BuiltIn,
            /// </summary>
            BuiltIn,

            /// <summary>
            /// Environment,
            /// </summary>
            Environment,

            /// <summary>
            /// Toolset,
            /// </summary>
            Toolset,

            /// <summary>
            /// SubToolset,
            /// </summary>
            SubToolset,

            /// <summary>
            /// Global,
            /// </summary>
            Global,

            /// <summary>
            /// EvaluateExpression,
            /// </summary>
            EvaluateExpression,

            /// <summary>
            /// EvaluateCondition,
            /// </summary>
            EvaluateCondition,

            /// <summary>
            /// ToolsVersion,
            /// </summary>
            ToolsVersion,

            /// <summary>
            /// Properties,
            /// </summary>
            Properties,

            /// <summary>
            /// ItemDefinitions,
            /// </summary>
            ItemDefinitions,

            /// <summary>
            /// Items
            /// </summary>
            Items
        }

        /// <summary>
        /// Evaluates the project data passed in.
        /// </summary>
        /// <remarks>
        /// This is the only non-private member of this class.
        /// This is a helper static method so that the caller can just do "Evaluator.Evaluate(..)" without
        /// newing one up, yet the whole class need not be static.
        /// </remarks>
        internal static void Evaluate(
            IEvaluatorData<P, I, M, D> data,
            ProjectRootElement root,
            ProjectLoadSettings loadSettings,
            int maxNodeCount,
            PropertyDictionary<ProjectPropertyInstance> environmentProperties,
            ILoggingService loggingService,
            IItemFactory<I, I> itemFactory,
            IToolsetProvider toolsetProvider,
            ProjectRootElementCache projectRootElementCache,
            BuildEventContext buildEventContext,
            ISdkResolverService sdkResolverService,
            int submissionId,
            EvaluationContext evaluationContext = null)
        {
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectEvaluateBegin, CodeMarkerEvent.perfMSBuildProjectEvaluateEnd))
#endif
            {
#if MSBUILDENABLEVSPROFILING
            try
            {
                string projectFile = String.IsNullOrEmpty(root.ProjectFileLocation.File) ? "(null)" : root.ProjectFileLocation.File;
                string beginProjectEvaluate = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - Begin", projectFile);
                DataCollection.CommentMarkProfile(8812, beginProjectEvaluate);
#endif
                var profileEvaluation = (loadSettings & ProjectLoadSettings.ProfileEvaluation) != 0 || loggingService.IncludeEvaluationProfile;
                var evaluator = new Evaluator<P, I, M, D>(
                    data,
                    root,
                    loadSettings,
                    maxNodeCount,
                    environmentProperties,
                    itemFactory,
                    toolsetProvider,
                    projectRootElementCache,
                    sdkResolverService,
                    submissionId,
                    evaluationContext,
                    profileEvaluation);

                evaluator.Evaluate(loggingService, buildEventContext);
#if MSBUILDENABLEVSPROFILING 
            }
            finally
            {
                string projectFile = String.IsNullOrEmpty(root.ProjectFileLocation.File) ? "(null)" : root.ProjectFileLocation.File;
                string beginProjectEvaluate = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End", projectFile);
                DataCollection.CommentMarkProfile(8813, beginProjectEvaluate);
            }
#endif
            }
        }

        /// <summary>
        /// Helper that creates a list of ProjectItem's given an unevaluated Include and a ProjectRootElement.
        /// Used by both Evaluator.EvaluateItemElement and by Project.AddItem.
        /// </summary>
        internal static List<I> CreateItemsFromInclude(string rootDirectory, ProjectItemElement itemElement, IItemFactory<I, I> itemFactory, string unevaluatedIncludeEscaped, Expander<P, I> expander)
        {
            ErrorUtilities.VerifyThrowArgumentLength(unevaluatedIncludeEscaped, "unevaluatedIncludeEscaped");

            List<I> items = new List<I>();
            itemFactory.ItemElement = itemElement;

            // STEP 1: Expand properties in Include
            string evaluatedIncludeEscaped = expander.ExpandIntoStringLeaveEscaped(unevaluatedIncludeEscaped, ExpanderOptions.ExpandProperties, itemElement.IncludeLocation);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedIncludeEscaped.Length > 0)
            {
                var includeSplitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedIncludeEscaped);

                foreach (string includeSplitEscaped in includeSplitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool throwaway;
                    IList<I> itemsFromSplit = expander.ExpandSingleItemVectorExpressionIntoItems(includeSplitEscaped, itemFactory, ExpanderOptions.ExpandItems, false /* do not include null expansion results */, out throwaway, itemElement.IncludeLocation);

                    if (itemsFromSplit != null)
                    {
                        // Expression is in form "@(X)"
                        foreach (I item in itemsFromSplit)
                        {
                            items.Add(item);
                        }
                    }
                    else
                    {
                        // The expression is not of the form "@(X)". Treat as string
                        string[] includeSplitFilesEscaped = EngineFileUtilities.Default.GetFileListEscaped(rootDirectory, includeSplitEscaped);

                        if (includeSplitFilesEscaped.Length > 0)
                        {
                            foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                            {
                                items.Add(itemFactory.CreateItem(includeSplitFileEscaped, includeSplitEscaped, itemElement.ContainingProject.FullPath));
                            }
                        }
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Read the task into an instance.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private static ProjectTaskInstance ReadTaskElement(ProjectTaskElement taskElement)
        {
            List<ProjectTaskInstanceChild> taskOutputs = new List<ProjectTaskInstanceChild>(taskElement.Count);

            foreach (ProjectOutputElement output in taskElement.Outputs)
            {
                if (output.IsOutputItem)
                {
                    ProjectTaskOutputItemInstance outputItem = new ProjectTaskOutputItemInstance
                        (
                        output.ItemType,
                        output.TaskParameter,
                        output.Condition,
                        output.Location,
                        output.ItemTypeLocation,
                        output.TaskParameterLocation,
                        output.ConditionLocation
                        );

                    taskOutputs.Add(outputItem);
                }
                else
                {
                    ProjectTaskOutputPropertyInstance outputItem = new ProjectTaskOutputPropertyInstance
                        (
                        output.PropertyName,
                        output.TaskParameter,
                        output.Condition,
                        output.Location,
                        output.PropertyNameLocation,
                        output.TaskParameterLocation,
                        output.ConditionLocation
                        );

                    taskOutputs.Add(outputItem);
                }
            }

            ProjectTaskInstance task = new ProjectTaskInstance(taskElement, taskOutputs);
            return task;
        }

        /// <summary>
        /// Read the property-group-under-target into an instance.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private static ProjectPropertyGroupTaskInstance ReadPropertyGroupUnderTargetElement(ProjectPropertyGroupElement propertyGroupElement)
        {
            List<ProjectPropertyGroupTaskPropertyInstance> properties = new List<ProjectPropertyGroupTaskPropertyInstance>(propertyGroupElement.Count);

            foreach (ProjectPropertyElement propertyElement in propertyGroupElement.Properties)
            {
                ProjectPropertyGroupTaskPropertyInstance property = new ProjectPropertyGroupTaskPropertyInstance(propertyElement.Name, propertyElement.Value, propertyElement.Condition, propertyElement.Location, propertyElement.ConditionLocation);
                properties.Add(property);
            }

            ProjectPropertyGroupTaskInstance propertyGroup = new ProjectPropertyGroupTaskInstance(propertyGroupElement.Condition, propertyGroupElement.Location, propertyGroupElement.ConditionLocation, properties);

            return propertyGroup;
        }

        /// <summary>
        /// Read an onError tag.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private static ProjectOnErrorInstance ReadOnErrorElement(ProjectOnErrorElement projectOnErrorElement)
        {
            ProjectOnErrorInstance onError = new ProjectOnErrorInstance(projectOnErrorElement.ExecuteTargetsAttribute, projectOnErrorElement.Condition, projectOnErrorElement.Location, projectOnErrorElement.ExecuteTargetsLocation, projectOnErrorElement.ConditionLocation);

            return onError;
        }

        /// <summary>
        /// Read the item-group-under-target into an instance.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private static ProjectItemGroupTaskInstance ReadItemGroupUnderTargetElement(ProjectItemGroupElement itemGroupElement)
        {
            List<ProjectItemGroupTaskItemInstance> items = new List<ProjectItemGroupTaskItemInstance>(itemGroupElement.Count);

            foreach (ProjectItemElement itemElement in itemGroupElement.Items)
            {
                List<ProjectItemGroupTaskMetadataInstance> metadata = null;

                foreach (ProjectMetadataElement metadataElement in itemElement.Metadata)
                {
                    if (metadata == null)
                    {
                        metadata = new List<ProjectItemGroupTaskMetadataInstance>();
                    }

                    ProjectItemGroupTaskMetadataInstance metadatum = new ProjectItemGroupTaskMetadataInstance
                        (
                        metadataElement.Name,
                        metadataElement.Value,
                        metadataElement.Condition,
                        metadataElement.Location,
                        metadataElement.ConditionLocation
                        );

                    metadata.Add(metadatum);
                }

                ProjectItemGroupTaskItemInstance item = new ProjectItemGroupTaskItemInstance
                    (
                    itemElement.ItemType,
                    itemElement.Include,
                    itemElement.Exclude,
                    itemElement.Remove,
                    itemElement.KeepMetadata,
                    itemElement.RemoveMetadata,
                    itemElement.KeepDuplicates,
                    itemElement.Condition,
                    itemElement.Location,
                    itemElement.IncludeLocation,
                    itemElement.ExcludeLocation,
                    itemElement.RemoveLocation,
                    itemElement.KeepMetadataLocation,
                    itemElement.RemoveMetadataLocation,
                    itemElement.KeepDuplicatesLocation,
                    itemElement.ConditionLocation,
                    metadata
                    );

                items.Add(item);
            }

            ProjectItemGroupTaskInstance itemGroup = new ProjectItemGroupTaskInstance(itemGroupElement.Condition, itemGroupElement.Location, itemGroupElement.ConditionLocation, items);

            return itemGroup;
        }

        /// <summary>
        /// Read the provided target into a target instance.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private static ProjectTargetInstance ReadNewTargetElement(ProjectTargetElement targetElement, bool parentProjectSupportsReturnsAttribute, EvaluationProfiler evaluationProfiler)
        {
            List<ProjectTargetInstanceChild> targetChildren = new List<ProjectTargetInstanceChild>(targetElement.Count);
            List<ProjectOnErrorInstance> targetOnErrorChildren = new List<ProjectOnErrorInstance>();

            foreach (ProjectElement targetChildElement in targetElement.Children)
            {
                using (evaluationProfiler.TrackElement(targetChildElement))
                {
                    ProjectTaskElement task = targetChildElement as ProjectTaskElement;

                    if (task != null)
                    {
                        ProjectTaskInstance taskInstance = ReadTaskElement(task);

                        targetChildren.Add(taskInstance);
                        continue;
                    }

                    ProjectPropertyGroupElement propertyGroup = targetChildElement as ProjectPropertyGroupElement;

                    if (propertyGroup != null)
                    {
                        ProjectPropertyGroupTaskInstance propertyGroupInstance = ReadPropertyGroupUnderTargetElement(propertyGroup);

                        targetChildren.Add(propertyGroupInstance);
                        continue;
                    }

                    ProjectItemGroupElement itemGroup = targetChildElement as ProjectItemGroupElement;

                    if (itemGroup != null)
                    {
                        ProjectItemGroupTaskInstance itemGroupInstance = ReadItemGroupUnderTargetElement(itemGroup);

                        targetChildren.Add(itemGroupInstance);
                        continue;
                    }

                    ProjectOnErrorElement onError = targetChildElement as ProjectOnErrorElement;

                    if (onError != null)
                    {
                        ProjectOnErrorInstance onErrorInstance = ReadOnErrorElement(onError);

                        targetOnErrorChildren.Add(onErrorInstance);
                        continue;
                    }

                    ErrorUtilities.ThrowInternalError("Unexpected child");
                }
            }

            // ObjectModel.ReadOnlyCollection is actually a poorly named ReadOnlyList

            // UNDONE: (Cloning.) This should be cloning these collections, but it isn't. ProjectTargetInstance will be able to see modifications.
            ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild> readOnlyTargetChildren = new ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild>(targetChildren);
            ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance> readOnlyTargetOnErrorChildren = new ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance>(targetOnErrorChildren);

            ProjectTargetInstance targetInstance = new ProjectTargetInstance
                (
                targetElement.Name,
                targetElement.Condition,
                targetElement.Inputs,
                targetElement.Outputs,
                targetElement.Returns,
                targetElement.KeepDuplicateOutputs,
                targetElement.DependsOnTargets,
                targetElement.Location,
                targetElement.ConditionLocation,
                targetElement.InputsLocation,
                targetElement.OutputsLocation,
                targetElement.ReturnsLocation,
                targetElement.KeepDuplicateOutputsLocation,
                targetElement.DependsOnTargetsLocation,
                targetElement.BeforeTargetsLocation,
                targetElement.AfterTargetsLocation,
                readOnlyTargetChildren,
                readOnlyTargetOnErrorChildren,
                parentProjectSupportsReturnsAttribute
                );

            targetElement.TargetInstance = targetInstance;
            return targetInstance;
        }

        /// <summary>
        /// Do the evaluation.
        /// Called by the static helper method.
        /// </summary>
        private void Evaluate(ILoggingService loggingService, BuildEventContext buildEventContext)
        {
            string projectFile;
            using (_evaluationProfiler.TrackPass(EvaluationPass.TotalEvaluation))
            {
                ErrorUtilities.VerifyThrow(_data.EvaluationId == BuildEventContext.InvalidEvaluationId, "There is no prior evaluation ID. The evaluator data needs to be reset at this point");

                _logProjectImportedEvents = Traits.Instance.EscapeHatches.LogProjectImports;

                ICollection<P> builtInProperties;
                ICollection<P> environmentProperties;
                ICollection<P> toolsetProperties;
                ICollection<P> globalProperties;

                using (_evaluationProfiler.TrackPass(EvaluationPass.InitialProperties))
                {
                    // Pass0: load initial properties
                    // Follow the order of precedence so that Global properties overwrite Environment properties
                    builtInProperties = AddBuiltInProperties();
                    environmentProperties = AddEnvironmentProperties();
                    toolsetProperties = AddToolsetProperties();
                    globalProperties = AddGlobalProperties();
                }

#if (!STANDALONEBUILD)
            CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildProjectEvaluatePass0End);
#endif
                projectFile = String.IsNullOrEmpty(_projectRootElement.ProjectFileLocation.File) ? "(null)" : _projectRootElement.ProjectFileLocation.File;

                _evaluationLoggingContext = new EvaluationLoggingContext(loggingService, buildEventContext, projectFile);
                _data.EvaluationId = _evaluationLoggingContext.BuildEventContext.EvaluationId;

                _evaluationLoggingContext.LogProjectEvaluationStarted();

                ErrorUtilities.VerifyThrow(_data.EvaluationId != BuildEventContext.InvalidEvaluationId, "Evaluation should produce an evaluation ID");

#if MSBUILDENABLEVSPROFILING
        string endPass0 = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End Pass 0 (Initial properties)", projectFile);
        DataCollection.CommentMarkProfile(8816, endPass0);
#endif

                // Pass1: evaluate properties, load imports, and gather everything else
                using (_evaluationProfiler.TrackPass(EvaluationPass.Properties))
                {
                    PerformDepthFirstPass(_projectRootElement);
                }

                List<string> initialTargets = new List<string>(_initialTargetsList.Count);
                foreach (var initialTarget in _initialTargetsList)
                {
                    initialTargets.Add(EscapingUtilities.UnescapeAll(initialTarget.Trim()));
                }

                _data.InitialTargets = initialTargets;
#if (!STANDALONEBUILD)
        CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildProjectEvaluatePass1End);
#endif
#if MSBUILDENABLEVSPROFILING
        string endPass1 = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End Pass 1 (Properties and Imports)", projectFile);
        DataCollection.CommentMarkProfile(8817, endPass1);
#endif
                // Pass2: evaluate item definitions
                // Don't box via IEnumerator and foreach; cache count so not to evaluate via interface each iteration
                using (_evaluationProfiler.TrackPass(EvaluationPass.ItemDefinitionGroups))
                {
                    foreach (var itemDefinitionGroupElement in _itemDefinitionGroupElements)
                    {
                        using (_evaluationProfiler.TrackElement(itemDefinitionGroupElement))
                        {
                            EvaluateItemDefinitionGroupElement(itemDefinitionGroupElement);
                        }
                    }
                }
#if (!STANDALONEBUILD)
        CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildProjectEvaluatePass2End);
#endif
#if MSBUILDENABLEVSPROFILING
        string endPass2 = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End Pass 2 (Item Definitions)", projectFile);
        DataCollection.CommentMarkProfile(8818, endPass2);
#endif
                LazyItemEvaluator<P, I, M, D> lazyEvaluator = null;
                using (_evaluationProfiler.TrackPass(EvaluationPass.Items))
                {
                    // comment next line to turn off lazy Evaluation
                    lazyEvaluator = new LazyItemEvaluator<P, I, M, D>(_data, _itemFactory, _evaluationLoggingContext, _evaluationProfiler, _evaluationContext);

                    // Pass3: evaluate project items
                    foreach (ProjectItemGroupElement itemGroup in _itemGroupElements)
                    {
                        using (_evaluationProfiler.TrackElement(itemGroup))
                        {
                            EvaluateItemGroupElement(itemGroup, lazyEvaluator);
                        }
                    }
                }

                if (lazyEvaluator != null)
                {
                    using (_evaluationProfiler.TrackPass(EvaluationPass.LazyItems))
                    {
                        // Tell the lazy evaluator to compute the items and add them to _data
                        foreach (var itemData in lazyEvaluator.GetAllItemsDeferred())
                        {
                            if (itemData.ConditionResult)
                            {
                                _data.AddItem(itemData.Item);

                                if (_data.ShouldEvaluateForDesignTime)
                                {
                                    _data.AddToAllEvaluatedItemsList(itemData.Item);
                                }
                            }

                            if (_data.ShouldEvaluateForDesignTime)
                            {
                                _data.AddItemIgnoringCondition(itemData.Item);
                            }
                        }

                        // lazy evaluator can be collected now, the rest of evaluation does not need it anymore
                        lazyEvaluator = null;
                    }
                }

#if (!STANDALONEBUILD)
        CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildProjectEvaluatePass3End);
#endif
#if MSBUILDENABLEVSPROFILING
        string endPass3 = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End Pass 3 (Items)", projectFile);
        DataCollection.CommentMarkProfile(8819, endPass3);
#endif
                // Pass4: evaluate using-tasks
                using (_evaluationProfiler.TrackPass(EvaluationPass.UsingTasks))
                {
                    foreach (var entry in _usingTaskElements)
                    {
                        EvaluateUsingTaskElement(entry.Key, entry.Value);
                    }
                }

                // If there was no DefaultTargets attribute found in the depth first pass, 
                // use the name of the first target. If there isn't any target, don't error until build time.

                if (_data.DefaultTargets == null)
                {
                    _data.DefaultTargets = new List<string>(1);
                }

                var targetElementsCount = _targetElements.Count;
                if (_data.DefaultTargets.Count == 0 && targetElementsCount > 0)
                {
                    _data.DefaultTargets.Add(_targetElements[0].Name);
                }

                Dictionary<string, List<TargetSpecification>> targetsWhichRunBeforeByTarget = new Dictionary<string, List<TargetSpecification>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, List<TargetSpecification>> targetsWhichRunAfterByTarget = new Dictionary<string, List<TargetSpecification>>(StringComparer.OrdinalIgnoreCase);
                LinkedList<ProjectTargetElement> activeTargetsByEvaluationOrder = new LinkedList<ProjectTargetElement>();
                Dictionary<string, LinkedListNode<ProjectTargetElement>> activeTargets = new Dictionary<string, LinkedListNode<ProjectTargetElement>>(StringComparer.OrdinalIgnoreCase);
#if (!STANDALONEBUILD)
        CodeMarkers.Instance.CodeMarker(CodeMarkerEvent.perfMSBuildProjectEvaluatePass4End);
#endif
#if MSBUILDENABLEVSPROFILING
        string endPass4 = String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - End Pass 4 (UsingTasks)", projectFile);
        DataCollection.CommentMarkProfile(8820, endPass4);
#endif

                using (_evaluationProfiler.TrackPass(EvaluationPass.Targets))
                {
                    // Pass5: read targets (but don't evaluate them: that happens during build)
                    for (var i = 0; i < targetElementsCount; i++)
                    {
                        var element = _targetElements[i];
                        using (_evaluationProfiler.TrackElement(element))
                        {
                            ReadTargetElement(element, activeTargetsByEvaluationOrder, activeTargets);
                        }
                    }

                    foreach (ProjectTargetElement target in activeTargetsByEvaluationOrder)
                    {
                        using (_evaluationProfiler.TrackElement(target))
                        {
                            AddBeforeAndAfterTargetMappings(target, activeTargets, targetsWhichRunBeforeByTarget, targetsWhichRunAfterByTarget);
                        }
                    }

                    _data.BeforeTargets = targetsWhichRunBeforeByTarget;
                    _data.AfterTargets = targetsWhichRunAfterByTarget;

                    if (Traits.Instance.EscapeHatches.DebugEvaluation)
                    {
                        // This is so important for VS performance it's worth always tracing; accidentally having 
                        // inconsistent sets of global properties will cause reevaluations, which are wasteful and incorrect
                        if (_projectRootElement.Count > 0) // VB/C# will new up empty projects; they aren't worth recording
                        {
                            ProjectPropertyInstance configurationData = _data.GlobalPropertiesDictionary["currentsolutionconfigurationcontents"];
                            int hash = (configurationData != null) ? configurationData.EvaluatedValue.GetHashCode() : 0;
                            string propertyDump = null;

                            foreach (var entry in _data.GlobalPropertiesDictionary)
                            {
                                if (!String.Equals(entry.Name, "currentsolutionconfigurationcontents", StringComparison.OrdinalIgnoreCase))
                                {
                                    propertyDump += entry.Name + "=" + entry.EvaluatedValue + "\n";
                                }
                            }

                            string line = new string('#', 100) + "\n";

                            string output = String.Format(CultureInfo.CurrentUICulture, "###: MSBUILD: Evaluating or reevaluating project {0} with {1} global properties and {2} tools version, child count {3}, CurrentSolutionConfigurationContents hash {4} other properties:\n{5}", _projectRootElement.FullPath, globalProperties.Count, _data.Toolset.ToolsVersion, _projectRootElement.Count, hash, propertyDump);

                            Trace.WriteLine(line + output + line);
                        }
                    }

                    _data.FinishEvaluation();
                }
            }

            ErrorUtilities.VerifyThrow(_evaluationProfiler.IsEmpty(), "Evaluation profiler stack is not empty.");
            _evaluationLoggingContext.LogBuildEvent(new ProjectEvaluationFinishedEventArgs(ResourceUtilities.GetResourceString("EvaluationFinished"), projectFile)
            {
                BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                ProjectFile = projectFile,
                ProfilerResult = _evaluationProfiler.ProfiledResult
            });
        }

        /// <summary>
        /// Evaluate the properties in the passed in XML, into the project.
        /// Does a depth first traversal into Imports.
        /// In the process, populates the item, itemdefinition, target, and usingtask lists as well.
        /// </summary>
        private void PerformDepthFirstPass(ProjectRootElement currentProjectOrImport)
        {
            using (_evaluationProfiler.TrackFile(currentProjectOrImport.FullPath))
            {
                // We accumulate InitialTargets from the project and each import
                var initialTargets = _expander.ExpandIntoStringListLeaveEscaped(currentProjectOrImport.InitialTargets, ExpanderOptions.ExpandProperties, currentProjectOrImport.InitialTargetsLocation);
                _initialTargetsList.AddRange(initialTargets);

                if (!Traits.Instance.EscapeHatches.IgnoreTreatAsLocalProperty)
                {
                    foreach (string propertyName in _expander.ExpandIntoStringListLeaveEscaped(currentProjectOrImport.TreatAsLocalProperty, ExpanderOptions.ExpandProperties, currentProjectOrImport.TreatAsLocalPropertyLocation))
                    {
                        XmlUtilities.VerifyThrowProjectValidElementName(propertyName, currentProjectOrImport.Location);
                        _data.GlobalPropertiesToTreatAsLocal.Add(propertyName);
                    }
                }

                UpdateDefaultTargets(currentProjectOrImport);

                // Get all the implicit imports (e.g. <Project Sdk="" />, or <Sdk Name="" />, but not <Import Sdk="" />)
                List<ProjectImportElement> implicitImports = currentProjectOrImport.GetImplicitImportNodes(currentProjectOrImport);

                // Evaluate the "top" implicit imports as if they were the first entry in the file.
                foreach (var import in implicitImports)
                {
                    if (import.ImplicitImportLocation == ImplicitImportLocation.Top)
                    {
                        EvaluateImportElement(currentProjectOrImport.DirectoryPath, import);
                    }
                }

                foreach (ProjectElement element in currentProjectOrImport.Children)
                {
                    ProjectPropertyGroupElement propertyGroup = element as ProjectPropertyGroupElement;

                    if (propertyGroup != null)
                    {
                        EvaluatePropertyGroupElement(propertyGroup);
                        continue;
                    }

                    ProjectItemGroupElement itemGroup = element as ProjectItemGroupElement;

                    if (itemGroup != null)
                    {
                        _itemGroupElements.Add(itemGroup);

                        continue;
                    }

                    ProjectItemDefinitionGroupElement itemDefinitionGroup = element as ProjectItemDefinitionGroupElement;

                    if (itemDefinitionGroup != null)
                    {
                        _itemDefinitionGroupElements.Add(itemDefinitionGroup);

                        continue;
                    }

                    ProjectTargetElement target = element as ProjectTargetElement;

                    if (target != null)
                    {
                        if (_projectSupportsReturnsAttribute.ContainsKey(currentProjectOrImport))
                        {
                            _projectSupportsReturnsAttribute[currentProjectOrImport] |= (target.Returns != null);
                        }
                        else
                        {
                            _projectSupportsReturnsAttribute[currentProjectOrImport] = (target.Returns != null);
                        }

                        _targetElements.Add(target);

                        continue;
                    }

                    ProjectImportElement import = element as ProjectImportElement;
                    if (import != null)
                    {
                        EvaluateImportElement(currentProjectOrImport.DirectoryPath, import);
                        continue;
                    }

                    ProjectImportGroupElement importGroup = element as ProjectImportGroupElement;

                    if (importGroup != null)
                    {
                        EvaluateImportGroupElement(currentProjectOrImport.DirectoryPath, importGroup);
                        continue;
                    }

                    ProjectUsingTaskElement usingTask = element as ProjectUsingTaskElement;

                    if (usingTask != null)
                    {
                        _usingTaskElements.Add(new Pair<string, ProjectUsingTaskElement>(currentProjectOrImport.DirectoryPath, usingTask));
                        continue;
                    }

                    ProjectChooseElement choose = element as ProjectChooseElement;

                    if (choose != null)
                    {
                        EvaluateChooseElement(choose);
                        continue;
                    }

                    if (element is ProjectExtensionsElement)
                    {
                        continue;
                    }

                    if (element is ProjectSdkElement)
                    {
                        continue; // This case is handled by implicit imports.
                    }

                    ErrorUtilities.ThrowInternalError("Unexpected child type");
                }

                // Evaluate the "bottom" implicit imports as if they were the last entry in the file.
                foreach (var import in implicitImports)
                {
                    if (import.ImplicitImportLocation == ImplicitImportLocation.Bottom)
                    {
                        EvaluateImportElement(currentProjectOrImport.DirectoryPath, import);
                    }
                }
            }
        }

        /// <summary>
        /// Update the default targets value.
        /// We only take the first DefaultTargets value we encounter in a project or import.
        /// </summary>
        private void UpdateDefaultTargets(ProjectRootElement currentProjectOrImport)
        {
            if (_data.DefaultTargets == null)
            {
                string expanded = _expander.ExpandIntoStringLeaveEscaped(currentProjectOrImport.DefaultTargets, ExpanderOptions.ExpandProperties, currentProjectOrImport.DefaultTargetsLocation);

                if (expanded.Length > 0)
                {
                    SetBuiltInProperty(ReservedPropertyNames.projectDefaultTargets, EscapingUtilities.UnescapeAll(expanded));

                    List<string> temp = new List<string>(expanded.Split(s_splitter, StringSplitOptions.RemoveEmptyEntries));

                    for (int i = 0; i < temp.Count; i++)
                    {
                        string target = EscapingUtilities.UnescapeAll(temp[i].Trim());
                        if (target.Length > 0)
                        {
                            _data.DefaultTargets = _data.DefaultTargets ?? new List<string>(temp.Count);
                            _data.DefaultTargets.Add(target);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate the properties in the propertygroup and set the applicable ones on the data passed in
        /// </summary>
        private void EvaluatePropertyGroupElement(ProjectPropertyGroupElement propertyGroupElement)
        {
            using (_evaluationProfiler.TrackElement(propertyGroupElement))
            { 
                if (EvaluateConditionCollectingConditionedProperties(propertyGroupElement, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties))
                {
                    foreach (ProjectPropertyElement propertyElement in propertyGroupElement.Properties)
                    {
                        EvaluatePropertyElement(propertyElement);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate the itemdefinitiongroup and update the definitions library
        /// </summary>
        private void EvaluateItemDefinitionGroupElement(ProjectItemDefinitionGroupElement itemDefinitionGroupElement)
        {
            if (EvaluateCondition(itemDefinitionGroupElement, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties))
            {
                foreach (ProjectItemDefinitionElement itemDefinitionElement in itemDefinitionGroupElement.ItemDefinitions)
                {
                    using (_evaluationProfiler.TrackElement(itemDefinitionElement))
                    {
                        EvaluateItemDefinitionElement(itemDefinitionElement);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate the items in the itemgroup and add the applicable ones to the data passed in
        /// </summary>
        private void EvaluateItemGroupElement(ProjectItemGroupElement itemGroupElement, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            bool itemGroupConditionResult;
            if (lazyEvaluator != null)
            {
                itemGroupConditionResult = lazyEvaluator.EvaluateConditionWithCurrentState(itemGroupElement, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            }
            else
            {
                itemGroupConditionResult = EvaluateCondition(itemGroupElement, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            }

            if (itemGroupConditionResult || (_data.ShouldEvaluateForDesignTime && _data.CanEvaluateElementsWithFalseConditions))
            {
                foreach (ProjectItemElement itemElement in itemGroupElement.Items)
                {
                    using (_evaluationProfiler.TrackElement(itemElement))
                    {
                        EvaluateItemElement(itemGroupConditionResult, itemElement, lazyEvaluator);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate the usingtask and add the result into the data passed in
        /// </summary>
        private void EvaluateUsingTaskElement(string directoryOfImportingFile, ProjectUsingTaskElement projectUsingTaskElement)
        {
            TaskRegistry.RegisterTasksFromUsingTaskElement<P, I>
                (
                _evaluationLoggingContext.LoggingService,
                _evaluationLoggingContext.BuildEventContext,
                directoryOfImportingFile,
                projectUsingTaskElement,
                _data.TaskRegistry,
                _expander,
                ExpanderOptions.ExpandPropertiesAndItems
                );
        }

        /// <summary>
        /// Retrieve the matching ProjectTargetInstance from the cache and add it to the provided collection.
        /// If it is not cached already, read it and cache it.
        /// Do not evaluate anything: this occurs during build.
        /// </summary>
        private void ReadTargetElement(ProjectTargetElement targetElement, LinkedList<ProjectTargetElement> activeTargetsByEvaluationOrder, Dictionary<string, LinkedListNode<ProjectTargetElement>> activeTargets)
        {
            ProjectTargetInstance targetInstance = null;

            // If we already have read a target instance for this element, use that. 
            targetInstance = targetElement.TargetInstance;

            if (targetInstance == null)
            {
                targetInstance = ReadNewTargetElement(targetElement, _projectSupportsReturnsAttribute[(ProjectRootElement)targetElement.Parent], _evaluationProfiler);
            }

            string targetName = targetElement.Name;
            ProjectTargetInstance otherTarget = _data.GetTarget(targetName);
            if (otherTarget != null)
            {
                _evaluationLoggingContext.LogComment(MessageImportance.Low, "OverridingTarget", otherTarget.Name, otherTarget.Location.File, targetName, targetElement.Location.File);
            }

            LinkedListNode<ProjectTargetElement> node;
            if (activeTargets.TryGetValue(targetName, out node))
            {
                activeTargetsByEvaluationOrder.Remove(node);
            }

            activeTargets[targetName] = activeTargetsByEvaluationOrder.AddLast(targetElement);
            _data.AddTarget(targetInstance);
        }

        /// <summary>
        /// Updates the evaluation maps for BeforeTargets and AfterTargets
        /// </summary>
        private void AddBeforeAndAfterTargetMappings(ProjectTargetElement targetElement, Dictionary<string, LinkedListNode<ProjectTargetElement>> activeTargets, Dictionary<string, List<TargetSpecification>> targetsWhichRunBeforeByTarget, Dictionary<string, List<TargetSpecification>> targetsWhichRunAfterByTarget)
        {
            var beforeTargets = _expander.ExpandIntoStringListLeaveEscaped(targetElement.BeforeTargets, ExpanderOptions.ExpandPropertiesAndItems, targetElement.BeforeTargetsLocation);
            var afterTargets = _expander.ExpandIntoStringListLeaveEscaped(targetElement.AfterTargets, ExpanderOptions.ExpandPropertiesAndItems, targetElement.AfterTargetsLocation);

            foreach (string beforeTarget in beforeTargets)
            {
                string unescapedBeforeTarget = EscapingUtilities.UnescapeAll(beforeTarget);

                if (activeTargets.ContainsKey(unescapedBeforeTarget))
                {
                    List<TargetSpecification> beforeTargetsForTarget = null;
                    if (!targetsWhichRunBeforeByTarget.TryGetValue(unescapedBeforeTarget, out beforeTargetsForTarget))
                    {
                        beforeTargetsForTarget = new List<TargetSpecification>();
                        targetsWhichRunBeforeByTarget[unescapedBeforeTarget] = beforeTargetsForTarget;
                    }

                    beforeTargetsForTarget.Add(new TargetSpecification(targetElement.Name, targetElement.BeforeTargetsLocation));
                }
                else
                {
                    // This is a message, not a warning, because that enables people to speculatively extend the build of a project
                    // It's low importance as it's addressed to build authors
                    _evaluationLoggingContext.LogComment(MessageImportance.Low, "TargetDoesNotExistBeforeTargetMessage", unescapedBeforeTarget, targetElement.BeforeTargetsLocation.LocationString);
                }
            }

            foreach (string afterTarget in afterTargets)
            {
                string unescapedAfterTarget = EscapingUtilities.UnescapeAll(afterTarget);

                if (activeTargets.ContainsKey(unescapedAfterTarget))
                {
                    List<TargetSpecification> afterTargetsForTarget = null;
                    if (!targetsWhichRunAfterByTarget.TryGetValue(unescapedAfterTarget, out afterTargetsForTarget))
                    {
                        afterTargetsForTarget = new List<TargetSpecification>();
                        targetsWhichRunAfterByTarget[unescapedAfterTarget] = afterTargetsForTarget;
                    }

                    afterTargetsForTarget.Add(new TargetSpecification(targetElement.Name, targetElement.AfterTargetsLocation));
                }
                else
                {
                    // This is a message, not a warning, because that enables people to speculatively extend the build of a project
                    // It's low importance as it's addressed to build authors
                    _evaluationLoggingContext.LogComment(MessageImportance.Low, "TargetDoesNotExistAfterTargetMessage", unescapedAfterTarget, targetElement.AfterTargetsLocation.LocationString);
                }
            }
        }

        /// <summary>
        /// Set the built-in properties, most of which are read-only 
        /// </summary>
        private ICollection<P> AddBuiltInProperties()
        {
            string startupDirectory = BuildParameters.StartupDirectory;

            List<P> builtInProperties = new List<P>(19);

            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.toolsVersion, _data.Toolset.ToolsVersion));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.toolsPath, _data.Toolset.ToolsPath));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.binPath, _data.Toolset.ToolsPath));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.startupDirectory, startupDirectory));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.buildNodeCount, _maxNodeCount.ToString(CultureInfo.CurrentCulture)));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.programFiles32, FrameworkLocationHelper.programFiles32));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.assemblyVersion, Constants.AssemblyVersion));
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.version, MSBuildAssemblyFileVersion.Instance.MajorMinorBuild));

            // Fake OS env variables when not on Windows
            if (!NativeMethodsShared.IsWindows)
            {
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.osName, NativeMethodsShared.OSName));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.frameworkToolsRoot, NativeMethodsShared.FrameworkBasePath));
            }

#if RUNTIME_TYPE_NETCORE
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.msbuildRuntimeType, "Core"));
#elif MONO
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.msbuildRuntimeType,
                                                        NativeMethodsShared.IsMono ? "Mono" : "Full"));
#else
            builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.msbuildRuntimeType, "Full"));
#endif

            if (String.IsNullOrEmpty(_projectRootElement.FullPath))
            {
                // If this is an un-saved project, this is as far as we can go
                if (String.IsNullOrEmpty(_projectRootElement.DirectoryPath))
                {
                    builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectDirectory, startupDirectory));
                }
                else
                {
                    // Solution files based on the old OM end up here.  But they do have a location, which is where the solution was loaded from.
                    // We need to set this here otherwise we can't locate any projects the solution refers to.
                    builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectDirectory, _projectRootElement.DirectoryPath));
                }
            }
            else
            {
                // Add the MSBuildProjectXXXXX properties, but not the MSBuildFileXXXX ones. Those
                // vary according to the file they're evaluated in, so they have to be dealt with
                // specially in the Expander.
                string projectFile = EscapingUtilities.Escape(Path.GetFileName(_projectRootElement.FullPath));
                string projectFileWithoutExtension = EscapingUtilities.Escape(Path.GetFileNameWithoutExtension(_projectRootElement.FullPath));
                string projectExtension = EscapingUtilities.Escape(Path.GetExtension(_projectRootElement.FullPath));
                string projectFullPath = EscapingUtilities.Escape(_projectRootElement.FullPath);
                string projectDirectory = EscapingUtilities.Escape(_projectRootElement.DirectoryPath);

                int rootLength = Path.GetPathRoot(projectDirectory).Length;
                string projectDirectoryNoRoot = projectDirectory.Substring(rootLength);
                projectDirectoryNoRoot = FileUtilities.EnsureNoTrailingSlash(projectDirectoryNoRoot);
                projectDirectoryNoRoot = EscapingUtilities.Escape(FileUtilities.EnsureNoLeadingSlash(projectDirectoryNoRoot));

                // ReservedPropertyNames.projectDefaultTargets is already set
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectFile, projectFile));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectName, projectFileWithoutExtension));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectExtension, projectExtension));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectFullPath, projectFullPath));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectDirectory, projectDirectory));
                builtInProperties.Add(SetBuiltInProperty(ReservedPropertyNames.projectDirectoryNoRoot, projectDirectoryNoRoot));
            }

            return builtInProperties;
        }

        /// <summary>
        /// Pull in all the environment into our property bag
        /// </summary>
        private ICollection<P> AddEnvironmentProperties()
        {
            List<P> environmentPropertiesList = new List<P>(_environmentProperties.Count);

            foreach (ProjectPropertyInstance environmentProperty in _environmentProperties)
            {
                P property = _data.SetProperty(environmentProperty.Name, ((IProperty)environmentProperty).EvaluatedValueEscaped, false /* NOT global property */, false /* may NOT be a reserved name */);
                environmentPropertiesList.Add(property);
            }

            return environmentPropertiesList;
        }

        /// <summary>
        /// Put all the toolset's properties into our property bag
        /// </summary>
        private ICollection<P> AddToolsetProperties()
        {
            List<P> toolsetProperties = new List<P>(_data.Toolset.Properties.Count);

            foreach (ProjectPropertyInstance toolsetProperty in _data.Toolset.Properties.Values)
            {
                P property = _data.SetProperty(toolsetProperty.Name, ((IProperty)toolsetProperty).EvaluatedValueEscaped, false /* NOT global property */, false /* may NOT be a reserved name */);
                toolsetProperties.Add(property);
            }

            if (_data.SubToolsetVersion == null)
            {
                // In previous versions of MSBuild, there is almost always a subtoolset that adds a VisualStudioVersion property.  Since there
                // is most likely not a subtoolset now, we need to add VisualStudioVersion if its not already a property.
                if (!_data.Properties.Contains(Constants.VisualStudioVersionPropertyName))
                {
                    P subToolsetVersionProperty = _data.SetProperty(Constants.VisualStudioVersionPropertyName, MSBuildConstants.CurrentVisualStudioVersion, false /* NOT global property */, false /* may NOT be a reserved name */);
                    toolsetProperties.Add(subToolsetVersionProperty);
                }
            }
            else
            {
                SubToolset subToolset = null;

                // Make the subtoolset version itself available as a property -- but only if it's not already set. 
                // Because some people may be depending on this value even if there isn't a matching sub-toolset,
                // set the property even if there is no matching sub-toolset.  
                if (!_data.Properties.Contains(Constants.SubToolsetVersionPropertyName))
                {
                    P subToolsetVersionProperty = _data.SetProperty(Constants.SubToolsetVersionPropertyName, _data.SubToolsetVersion, false /* NOT global property */, false /* may NOT be a reserved name */);
                    toolsetProperties.Add(subToolsetVersionProperty);
                }

                if (_data.Toolset.SubToolsets.TryGetValue(_data.SubToolsetVersion, out subToolset))
                {
                    foreach (ProjectPropertyInstance subToolsetProperty in subToolset.Properties.Values)
                    {
                        P property = _data.SetProperty(subToolsetProperty.Name, ((IProperty)subToolsetProperty).EvaluatedValueEscaped, false /* NOT global property */, false /* may NOT be a reserved name */);
                        toolsetProperties.Add(property);
                    }
                }
            }

            return toolsetProperties;
        }

        /// <summary>
        /// Put all the global properties into our property bag
        /// </summary>
        private ICollection<P> AddGlobalProperties()
        {
            if (_data.GlobalPropertiesDictionary == null)
            {
                return Array.Empty<P>();
            }

            List<P> globalProperties = new List<P>(_data.GlobalPropertiesDictionary.Count);

            foreach (ProjectPropertyInstance globalProperty in _data.GlobalPropertiesDictionary)
            {
                P property = _data.SetProperty(globalProperty.Name, ((IProperty)globalProperty).EvaluatedValueEscaped, true /* IS global property */, false /* may NOT be a reserved name */);
                globalProperties.Add(property);
            }

            return globalProperties;
        }

        /// <summary>
        /// Set a built-in property in the supplied bag.
        /// NOT to be used for properties originating in XML.
        /// NOT to be used for global properties.
        /// NOT to be used for environment properties.
        /// </summary>
        private P SetBuiltInProperty(string name, string evaluatedValueEscaped)
        {
            P property = _data.SetProperty(name, evaluatedValueEscaped, false /* NOT global property */, true /* OK to be a reserved name */);
            return property;
        }

        /// <summary>
        /// Evaluate a single ProjectPropertyElement and update the data as appropriate
        /// </summary>
        private void EvaluatePropertyElement(ProjectPropertyElement propertyElement)
        {
            using (_evaluationProfiler.TrackElement(propertyElement))
            {
                // Global properties cannot be overridden.  We silently ignore them if we try.  Legacy behavior.
                // That is, unless this global property has been explicitly labeled as one that we want to treat as overridable for the duration 
                // of this project (or import). 
                if (
                        ((IDictionary<string, ProjectPropertyInstance>)_data.GlobalPropertiesDictionary).ContainsKey(propertyElement.Name) &&
                        !_data.GlobalPropertiesToTreatAsLocal.Contains(propertyElement.Name)
                    )
                {
                    return;
                }

                if (!EvaluateConditionCollectingConditionedProperties(propertyElement, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties))
                {
                    return;
                }

                // Set the name of the property we are currently evaluating so when we are checking to see if we want to add the property to the list of usedUninitialized properties we can not add the property if
                // it is the same as what we are setting the value on. Note: This needs to be set before we expand the property we are currently setting.
                _expander.UsedUninitializedProperties.CurrentlyEvaluatingPropertyElementName = propertyElement.Name;

                string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(propertyElement.Value, ExpanderOptions.ExpandProperties, propertyElement.Location);

                // If we are going to set a property to a value other than null or empty we need to check to see if it has been used
                // during evaluation.
                if (evaluatedValue.Length > 0 && _expander.WarnForUninitializedProperties)
                {
                    // Is the property we are currently setting in the list of properties which have been used but not initialized
                    IElementLocation elementWhichUsedProperty = null;
                    bool isPropertyInList = _expander.UsedUninitializedProperties.Properties.TryGetValue(propertyElement.Name, out elementWhichUsedProperty);

                    if (isPropertyInList)
                    {
                        // Once we are going to warn for a property once, remove it from the list so we do not add it again.
                        _expander.UsedUninitializedProperties.Properties.Remove(propertyElement.Name);
                        _evaluationLoggingContext.LogWarning(null, new BuildEventFileInfo(propertyElement.Location), "UsedUninitializedProperty", propertyElement.Name, elementWhichUsedProperty.LocationString);
                    }
                }

                _expander.UsedUninitializedProperties.CurrentlyEvaluatingPropertyElementName = null;

                P predecessor = _data.GetProperty(propertyElement.Name);

                P property = _data.SetProperty(propertyElement, evaluatedValue, predecessor);

                if (predecessor != null)
                {
                    LogPropertyReassignment(predecessor, property, propertyElement.Location.LocationString);
                }
            }
        }

        private void LogPropertyReassignment(P predecessor, P property, string location)
        {
            string newValue = property.EvaluatedValue;
            string oldValue = predecessor.EvaluatedValue;

            if (newValue != oldValue)
            {
                _evaluationLoggingContext.LogComment(
                    MessageImportance.Low,
                    "PropertyReassignment",
                    property.Name,
                    newValue,
                    oldValue,
                    location);
            }
        }

        private void EvaluateItemElement(bool itemGroupConditionResult, ProjectItemElement itemElement, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            bool itemConditionResult;
            if (lazyEvaluator != null)
            {
                itemConditionResult = lazyEvaluator.EvaluateConditionWithCurrentState(itemElement, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            }
            else
            {
                itemConditionResult = EvaluateCondition(itemElement, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            }

            if (!itemConditionResult && !(_data.ShouldEvaluateForDesignTime && _data.CanEvaluateElementsWithFalseConditions))
            {
                return;
            }

            if (lazyEvaluator != null)
            {
                var conditionResult = itemGroupConditionResult && itemConditionResult;

                lazyEvaluator.ProcessItemElement(_projectRootElement.DirectoryPath, itemElement, conditionResult);

                if (conditionResult)
                {
                    RecordEvaluatedItemElement(itemElement);
                }

                return;
            }

            // legacy, dead code beyond this point. Runs only if the lazy evaluator is null. Also, the interpretation of Remove is not implemented
            if (!string.IsNullOrEmpty(itemElement.Include))
            {
                EvaluateItemElementInclude(itemGroupConditionResult, itemConditionResult, itemElement);
            }
            else if (!string.IsNullOrEmpty(itemElement.Update))
            {
                EvaluateItemElementUpdate(itemElement);
            }
            else
            {
                ErrorUtilities.ThrowInternalError("Unexpected item operation");
            }
        }

        private void EvaluateItemElementUpdate(ProjectItemElement itemElement)
        {
            RecordEvaluatedItemElement(itemElement);

            var expandedItemSet =
                new HashSet<string>(
                    ExpressionShredder.SplitSemiColonSeparatedList
                        (
                            _expander.ExpandIntoStringLeaveEscaped(itemElement.Update, ExpanderOptions.ExpandPropertiesAndItems, itemElement.Location)
                        )
                        .SelectMany(i => EngineFileUtilities.Default.GetFileListEscaped(_projectRootElement.DirectoryPath, i))
                        .Select(EscapingUtilities.UnescapeAll));

            var itemsToUpdate = _data.GetItems(itemElement.ItemType).Where(i => expandedItemSet.Contains(i.EvaluatedInclude)).ToList();

            DecorateItemsWithMetadataFromProjectItemElement(itemElement, itemsToUpdate);
        }

        /// <summary>
        /// Evaluate a single ProjectItemElement into zero or more items.
        /// If specified, or if the condition on the item itself is false, only gathers the result into the list of items-ignoring-condition,
        /// and not into the real list of items.
        /// </summary>
        private void EvaluateItemElementInclude(bool itemGroupConditionResult, bool itemConditionResult, ProjectItemElement itemElement)
        {
            // Paths in items are evaluated relative to the outer project file, rather than relative to any targets file they may be contained in
            IList<I> items = CreateItemsFromInclude(_projectRootElement.DirectoryPath, itemElement, _itemFactory, itemElement.Include, _expander);

            // STEP 4: Evaluate, split, expand and subtract any Exclude
            if (itemElement.Exclude.Length > 0)
            {
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(itemElement.Exclude, ExpanderOptions.ExpandPropertiesAndItems, itemElement.ExcludeLocation);

                if (evaluatedExclude.Length > 0)
                {
                    var excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude);

                    HashSet<string> excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string excludeSplit in excludeSplits)
                    {
                        string[] excludeSplitFiles = EngineFileUtilities.Default.GetFileListEscaped(_projectRootElement.DirectoryPath, excludeSplit);

                        foreach (string excludeSplitFile in excludeSplitFiles)
                        {
                            excludes.Add(EscapingUtilities.UnescapeAll(excludeSplitFile));
                        }
                    }

                    List<I> remainingItems = new List<I>();

                    for (int i = 0; i < items.Count; i++)
                    {
                        if (!excludes.Contains(items[i].EvaluatedInclude))
                        {
                            remainingItems.Add(items[i]);
                        }
                    }

                    items = remainingItems;
                }
            }

            // STEP 5: Evaluate each metadata XML and apply them to each item we have so far
            DecorateItemsWithMetadataFromProjectItemElement(itemElement, items);

            // FINALLY: Add the items to the project
            if (itemConditionResult && itemGroupConditionResult)
            {
                RecordEvaluatedItemElement(itemElement);

                foreach (I item in items)
                {
                    _data.AddItem(item);

                    if (_data.ShouldEvaluateForDesignTime)
                    {
                        _data.AddToAllEvaluatedItemsList(item);
                    }
                }
            }

            if (_data.ShouldEvaluateForDesignTime)
            {
                foreach (I item in items)
                {
                    _data.AddItemIgnoringCondition(item);
                }
            }
        }

        private void DecorateItemsWithMetadataFromProjectItemElement(ProjectItemElement itemElement, IList<I> items)
        {
            if (itemElement.HasMetadata)
            {
                ////////////////////////////////////////////////////
                // UNDONE: Implement batching here.
                //
                // We want to allow built-in metadata in metadata values here. 
                // For example, so that an Idl file can specify that its Tlb output should be named %(Filename).tlb.
                // 
                // In other words, we want batching. However, we won't need to go to the trouble of using the regular batching code!
                // That's because that code is all about grouping into buckets of similar items. In this context, we're not
                // invoking a task, and it's fine to process each item individually, which will always give the correct results.
                //
                // For the CTP, to make the minimal change, we will not do this quite correctly.
                //
                // We will do this:
                // -- check whether any metadata values or their conditions contain any bare built-in metadata expressions,
                //    or whether they contain any custom metadata && the Include involved an @(itemlist) expression.
                // -- if either case is found, we go ahead and evaluate all the metadata separately for each item.
                // -- otherwise we can do the old thing (evaluating all metadata once then applying to all items)
                // 
                // This algorithm gives the correct results except when:
                // -- batchable expressions exist on the include, exclude, or condition on the item element itself
                //
                // It means that 99% of cases still go through the old code, which is best for the CTP.
                // When we ultimately implement this correctly, we should make sure we optimize for the case of very many items
                // and little metadata, none of which varies between items.
                List<string> values = new List<string>(itemElement.Count);

                foreach (ProjectMetadataElement metadatumElement in itemElement.Metadata)
                {
                    values.Add(metadatumElement.Value);
                    values.Add(metadatumElement.Condition);
                }

                ItemsAndMetadataPair itemsAndMetadataFound = ExpressionShredder.GetReferencedItemNamesAndMetadata(values);

                bool needToProcessItemsIndividually = false;

                if (itemsAndMetadataFound.Metadata != null && itemsAndMetadataFound.Metadata.Values.Count > 0)
                {
                    // If there is bare metadata of any kind, and the Include involved an item list, we should
                    // run items individually, as even non-built-in metadata might differ between items
                    List<string> include = new List<string>();
                    include.Add(itemElement.Include);
                    ItemsAndMetadataPair itemsAndMetadataFromInclude = ExpressionShredder.GetReferencedItemNamesAndMetadata(include);

                    if (itemsAndMetadataFromInclude.Items != null && itemsAndMetadataFromInclude.Items.Count > 0)
                    {
                        needToProcessItemsIndividually = true;
                    }
                    else
                    {
                        // If there is bare built-in metadata, we must always run items individually, as that almost
                        // always differs between items.

                        // UNDONE: When batching is implemented for real, we need to make sure that
                        // item definition metadata is included in all metadata operations during evaluation
                        if (itemsAndMetadataFound.Metadata.Values.Count > 0)
                        {
                            needToProcessItemsIndividually = true;
                        }
                    }
                }

                if (needToProcessItemsIndividually)
                {
                    foreach (I item in items)
                    {
                        _expander.Metadata = item;

                        foreach (ProjectMetadataElement metadatumElement in itemElement.Metadata)
                        {
                            if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll))
                            {
                                continue;
                            }

                            string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                            item.SetMetadata(metadatumElement, evaluatedValue);
                        }
                    }

                    // End of legal area for metadata expressions.
                    _expander.Metadata = null;
                }

                // End of pseudo batching
                ////////////////////////////////////////////////////
                // Start of old code
                else
                {
                    // Metadata expressions are allowed here.
                    // Temporarily gather and expand these in a table so they can reference other metadata elements above.
                    EvaluatorMetadataTable metadataTable = new EvaluatorMetadataTable(itemElement.ItemType);
                    _expander.Metadata = metadataTable;

                    // Also keep a list of everything so we can get the predecessor objects correct.
                    List<Pair<ProjectMetadataElement, string>> metadataList = new List<Pair<ProjectMetadataElement, string>>();

                    foreach (ProjectMetadataElement metadatumElement in itemElement.Metadata)
                    {
                        // Because of the checking above, it should be safe to expand metadata in conditions; the condition
                        // will be true for either all the items or none
                        if (!EvaluateCondition(metadatumElement, ExpanderOptions.ExpandAll, ParserOptions.AllowAll))
                        {
                            continue;
                        }

                        string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadatumElement.Value, ExpanderOptions.ExpandAll, metadatumElement.Location);

                        metadataTable.SetValue(metadatumElement, evaluatedValue);
                        metadataList.Add(new Pair<ProjectMetadataElement, string>(metadatumElement, evaluatedValue));
                    }

                    // Apply those metadata to each item
                    // Note that several items could share the same metadata objects

                    // Set all the items at once to make a potential copy-on-write optimization possible.
                    // This is valuable in the case where one item element evaluates to
                    // many items (either by semicolon or wildcards)
                    // and that item also has the same piece/s of metadata for each item.
                    _itemFactory.SetMetadata(metadataList, items);

                    // End of legal area for metadata expressions.
                    _expander.Metadata = null;
                }
            }
        }

        /// <summary>
        /// Evaluates an itemdefinition element, updating the definitions library.
        /// </summary>
        private void EvaluateItemDefinitionElement(ProjectItemDefinitionElement itemDefinitionElement)
        {
            // Get matching existing item definition, if any.
            IItemDefinition<M> itemDefinition = _data.GetItemDefinition(itemDefinitionElement.ItemType);

            // The expander should use the metadata from this item definition for further expansion, if any.
            // Otherwise, use a temporary, empty table.
            if (itemDefinition != null)
            {
                _expander.Metadata = itemDefinition;
            }
            else
            {
                _expander.Metadata = new EvaluatorMetadataTable(itemDefinitionElement.ItemType);
            }

            if (EvaluateCondition(itemDefinitionElement, ExpanderOptions.ExpandPropertiesAndMetadata, ParserOptions.AllowPropertiesAndCustomMetadata))
            {
                if (itemDefinition == null)
                {
                    itemDefinition = _data.AddItemDefinition(itemDefinitionElement.ItemType);
                    _expander.Metadata = itemDefinition;
                }

                foreach (ProjectMetadataElement metadataElement in itemDefinitionElement.Metadata)
                {
                    if (EvaluateCondition(metadataElement, ExpanderOptions.ExpandPropertiesAndMetadata, ParserOptions.AllowPropertiesAndCustomMetadata))
                    {
                        string evaluatedValue = _expander.ExpandIntoStringLeaveEscaped(metadataElement.Value, ExpanderOptions.ExpandPropertiesAndCustomMetadata, itemDefinitionElement.Location);

                        M predecessor = itemDefinition.GetMetadata(metadataElement.Name);

                        M metadatum = itemDefinition.SetMetadata(metadataElement, evaluatedValue, predecessor);

                        if (_data.ShouldEvaluateForDesignTime)
                        {
                            _data.AddToAllEvaluatedItemDefinitionMetadataList(metadatum);
                        }
                    }
                }
            }

            // End of valid area for metadata expansion.
            _expander.Metadata = null;
        }

        /// <summary>
        /// Evaluates an import element.
        /// If the condition is true, loads the import and continues the pass.
        /// </summary>
        /// <remarks>
        /// UNDONE: Protect against overflowing the stack by having too many nested imports.
        /// </remarks>
        private void EvaluateImportElement(string directoryOfImportingFile, ProjectImportElement importElement)
        {
            using (_evaluationProfiler.TrackElement(importElement))
            {
                List<ProjectRootElement> importedProjectRootElements = ExpandAndLoadImports(directoryOfImportingFile, importElement, out var sdkResult);

                foreach (ProjectRootElement importedProjectRootElement in importedProjectRootElements)
                {
                    _data.RecordImport(importElement, importedProjectRootElement, importedProjectRootElement.Version, sdkResult);
                    
                    PerformDepthFirstPass(importedProjectRootElement);
                }
            }
        }

        /// <summary>
        /// Evaluates an ImportGroup element.
        /// If the condition is true, evaluates the contained imports and continues the pass.
        /// </summary>
        /// <remarks>
        /// UNDONE: Protect against overflowing the stack by having too many nested imports.
        /// </remarks>
        private void EvaluateImportGroupElement(string directoryOfImportingFile, ProjectImportGroupElement importGroupElement)
        {
            using (_evaluationProfiler.TrackElement(importGroupElement))
            {
                if (EvaluateConditionCollectingConditionedProperties(importGroupElement, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties, _projectRootElementCache))
                {
                    foreach (ProjectImportElement importElement in importGroupElement.Imports)
                    {
                        EvaluateImportElement(directoryOfImportingFile, importElement);
                    }
                }
            }
        }

        /// <summary>
        /// Choose does not accept a condition.
        /// </summary>
        /// <remarks>
        /// We enter here in both the property and item passes, since Chooses can contain both.
        /// However, we only evaluate the When conditions on the first pass, so we only pulse 
        /// those states on that pass. On the other pass, it's as if they're not there.
        /// </remarks>
        private void EvaluateChooseElement(ProjectChooseElement chooseElement)
        {
            using (_evaluationProfiler.TrackElement(chooseElement))
            {
                foreach (ProjectWhenElement whenElement in chooseElement.WhenElements)
                {
                    if (EvaluateConditionCollectingConditionedProperties(whenElement, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties))
                    {
                        EvaluateWhenOrOtherwiseChildren(whenElement.Children);
                        return;
                    }
                }

                // "Otherwise" elements never have a condition
                if (chooseElement.OtherwiseElement != null)
                {
                    EvaluateWhenOrOtherwiseChildren(chooseElement.OtherwiseElement.Children);
                }
            }
        }

        /// <summary>
        /// Evaluates the children of a When or Choose.
        /// Returns true if the condition was true, so subsequent
        /// WhenElements and Otherwise can be skipped.
        /// </summary>
        private bool EvaluateWhenOrOtherwiseChildren(IEnumerable<ProjectElement> children)
        {
            foreach (ProjectElement element in children)
            {
                using (_evaluationProfiler.TrackElement(element))
                {
                    ProjectPropertyGroupElement propertyGroup = element as ProjectPropertyGroupElement;

                    if (propertyGroup != null)
                    {
                        EvaluatePropertyGroupElement(propertyGroup);
                        continue;
                    }

                    ProjectItemGroupElement itemGroup = element as ProjectItemGroupElement;

                    if (itemGroup != null)
                    {
                        _itemGroupElements.Add(itemGroup);
                        continue;
                    }

                    ProjectChooseElement choose = element as ProjectChooseElement;

                    if (choose != null)
                    {
                        EvaluateChooseElement(choose);
                        continue;
                    }

                    ErrorUtilities.ThrowInternalError("Unexpected child type");
                }
            }

            return true;
        }

        /// <summary>
        /// Expands and loads project imports.
        /// <remarks>
        /// Imports may contain references to "projectImportSearchPaths" defined in the app.config 
        /// toolset section. If this is the case, this method will search for the imported project
        /// in those additional paths if the default fails.
        /// </remarks>
        /// </summary>
        private List<ProjectRootElement> ExpandAndLoadImports(string directoryOfImportingFile, ProjectImportElement importElement, out SdkResult sdkResult)
        {
            var fallbackSearchPathMatch = _data.Toolset.GetProjectImportSearchPaths(importElement.Project);
            sdkResult = null;

            // no reference or we need to lookup only the default path,
            // so, use the Import path
            if (fallbackSearchPathMatch.Equals(ProjectImportPathMatch.None))
            {
                List<ProjectRootElement> projects;
                ExpandAndLoadImportsFromUnescapedImportExpressionConditioned(directoryOfImportingFile, importElement, out projects, out sdkResult);
                return projects;
            }

            // Note: Any property defined in the <projectImportSearchPaths> section can be replaced, MSBuildExtensionsPath
            // is used here as an example of behavior.
            // $(MSBuildExtensionsPath*) usually resolves to a single value, single default path
            //
            //     Eg. <Import Project='$(MSBuildExtensionsPath)\foo\extn.proj' />
            //
            // But this feature allows that when it is used in an Import element, it will behave as a "search path", meaning
            // that the relative project path "foo\extn.proj" will be searched for, in more than one location.
            // Essentially, we will try to load that project file by trying multiple values (search paths) for the
            // $(MSBuildExtensionsPath*) property.
            //
            // The various paths tried, in order are:
            //
            // 1. The value of the MSBuildExtensionsPath* property
            //
            // 2. Search paths available in the current toolset (via toolset.ImportPropertySearchPathsTable).
            //    That may be loaded from app.config with a definition like:
            //
            //    <toolset .. >
            //      <projectImportSearchPaths>
            //          <searchPaths os="osx">
            //              <property name="MSBuildExtensionsPath" value="/Library/Frameworks/Mono.framework/External/xbuild/;/tmp/foo"/>
            //              <property name="MSBuildExtensionsPath32" value="/Library/Frameworks/Mono.framework/External/xbuild/"/>
            //              <property name="MSBuildExtensionsPath64" value="/Library/Frameworks/Mono.framework/External/xbuild/"/>
            //          </searchPaths>
            //      </projectImportSearchPaths>
            //    </toolset>
            //
            // This is available only when used in an Import element and it's Condition. So, the following common pattern
            // would work:
            //
            //      <Import Project="$(MSBuildExtensionsPath)\foo\extn.proj" Condition="'Exists('$(MSBuildExtensionsPath)\foo\extn.proj')'" />
            //
            // The value of the MSBuildExtensionsPath* property, will always be "visible" with it's default value, example, when read or
            // referenced anywhere else. This is a very limited support, so, it doesn't come in to effect if the explicit reference to
            // the $(MSBuildExtensionsPath) property is not present in the Project attribute of the Import element. So, the following is
            // not supported:
            //
            //      <PropertyGroup><ProjectPathForImport>$(MSBuildExtensionsPath)\foo\extn.proj</ProjectPathForImport></PropertyGroup>
            //      <Import Project='$(ProjectPathForImport)' />
            //

            // Adding the value of $(MSBuildExtensionsPath*) property to the list of search paths
            var prop = _data.GetProperty(fallbackSearchPathMatch.PropertyName);

            var pathsToSearch = new string[fallbackSearchPathMatch.SearchPaths.Count + 1];
            pathsToSearch[0] = prop?.EvaluatedValue;                       // The actual value of the property, with no fallbacks
            fallbackSearchPathMatch.SearchPaths.CopyTo(pathsToSearch, 1);  // The list of fallbacks, in order
            
            string extensionPropertyRefAsString = fallbackSearchPathMatch.MsBuildPropertyFormat;

            _evaluationLoggingContext.LogComment(MessageImportance.Low, "SearchPathsForMSBuildExtensionsPath",
                                        extensionPropertyRefAsString,
                                        String.Join(";", pathsToSearch));

            bool atleastOneExactFilePathWasLookedAtAndNotFound = false;

            // If there are wildcards in the Import, a list of all the matches from all import search
            // paths will be returned (union of all files that match).
            var allProjects = new List<ProjectRootElement>();
            bool containsWildcards = FileMatcher.HasWildcards(importElement.Project);

            // Try every extension search path, till we get a Hit:
            // 1. 1 or more project files loaded
            // 2. 1 or more project files *found* but ignored (like circular, self imports)
            foreach (var extensionPath in pathsToSearch)
            {
                // In the rare case that the property we've enabled for search paths hasn't been defined
                // we will skip it, but continue with other paths in the fallback order.
                if (string.IsNullOrEmpty(extensionPath))
                    continue;

                string extensionPathExpanded = _data.ExpandString(extensionPath);

                if (!_fallbackSearchPathsCache.DirectoryExists(extensionPathExpanded))
                {
                    continue;
                }

                var newExpandedCondition = importElement.Condition.Replace(extensionPropertyRefAsString, extensionPathExpanded, StringComparison.OrdinalIgnoreCase);
                if (!EvaluateConditionCollectingConditionedProperties(importElement, newExpandedCondition, ExpanderOptions.ExpandProperties, ParserOptions.AllowProperties,
                            _projectRootElementCache))
                {
                    continue;
                }

                var newExpandedImportPath = importElement.Project.Replace(extensionPropertyRefAsString, extensionPathExpanded, StringComparison.OrdinalIgnoreCase);
                _evaluationLoggingContext.LogComment(MessageImportance.Low, "TryingExtensionsPath", newExpandedImportPath, extensionPathExpanded);

                List<ProjectRootElement> projects;
                var result = ExpandAndLoadImportsFromUnescapedImportExpression(directoryOfImportingFile, importElement, newExpandedImportPath, false, out projects);

                if (result == LoadImportsResult.ProjectsImported)
                {
                    // If we don't have a wildcard and we had a match, we're done.
                    if (!containsWildcards)
                    {
                        return projects;
                    }

                    allProjects.AddRange(projects);
                }

                if (result == LoadImportsResult.FoundFilesToImportButIgnored)
                {
                    // Circular, Self import cases are usually ignored
                    // Since we have a semi-success here, we stop looking at
                    // other paths

                    // If we don't have a wildcard and we had a match, we're done.
                    if (!containsWildcards)
                    {
                        return projects;
                    }

                    allProjects.AddRange(projects);
                }

                if (result == LoadImportsResult.TriedToImportButFileNotFound)
                {
                    atleastOneExactFilePathWasLookedAtAndNotFound = true;
                }
                // else if (result == LoadImportsResult.ImportExpressionResolvedToNothing) {}
            }

            // Found at least one project file for the Import, but no projects were loaded
            // atleastOneExactFilePathWasLookedAtAndNotFound would be false, eg, if the expression
            // was a wildcard and it resolved to zero files!
            if (allProjects.Count == 0 &&
                atleastOneExactFilePathWasLookedAtAndNotFound &&
                (_loadSettings & ProjectLoadSettings.IgnoreMissingImports) == 0)
            {
                ThrowForImportedProjectWithSearchPathsNotFound(fallbackSearchPathMatch, importElement);
            }

            return allProjects;
        }

        /// <summary>
        /// Load and parse the specified project import, which may have wildcards,
        /// into one or more ProjectRootElements, if it's Condition evaluates to true
        /// Caches the parsed import into the provided collection, so future
        /// requests can be satisfied without re-parsing it.
        /// </summary>
        private void ExpandAndLoadImportsFromUnescapedImportExpressionConditioned(
            string directoryOfImportingFile,
            ProjectImportElement importElement,
            out List<ProjectRootElement> projects,
            out SdkResult sdkResult,
            bool throwOnFileNotExistsError = true)
        {
            sdkResult = null;

            if (!EvaluateConditionCollectingConditionedProperties(importElement, ExpanderOptions.ExpandProperties,
                ParserOptions.AllowProperties, _projectRootElementCache))
            {
                if (_logProjectImportedEvents)
                {
                    // Expand the expression for the Log.  Since we know the condition evaluated to false, leave unexpandable properties in the condition so as not to cause an error
                    string expanded = _expander.ExpandIntoStringAndUnescape(importElement.Condition, ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError, importElement.ConditionLocation);

                    ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                        importElement.Location.Line,
                        importElement.Location.Column,
                        ResourceUtilities.GetResourceString("ProjectImportSkippedFalseCondition"),
                        importElement.Project,
                        importElement.ContainingProject.FullPath,
                        importElement.Location.Line,
                        importElement.Location.Column,
                        importElement.Condition,
                        expanded)
                    {
                        BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                        UnexpandedProject = importElement.Project,
                        ProjectFile = importElement.ContainingProject.FullPath
                    };

                    _evaluationLoggingContext.LogBuildEvent(eventArgs);
                }
                projects = new List<ProjectRootElement>();
                return;
            }

            string project = importElement.Project;

            if (importElement.ParsedSdkReference != null)
            {
                // Try to get the path to the solution and project being built. The solution path is not directly known
                // in MSBuild. It is passed in as a property either by the VS project system or by MSBuild's solution
                // metaproject. Microsoft.Common.CurrentVersion.targets sets the value to *Undefined* when not set, and
                // for backward compatibility, we shouldn't change that. But resolvers should be exposed to a string
                // that's null or a full path, so correct that here.
                var solutionPath = _data.GetProperty(SolutionProjectGenerator.SolutionPathPropertyName)?.EvaluatedValue;
                if (solutionPath == "*Undefined*") solutionPath = null;
                var projectPath = _data.GetProperty(ReservedPropertyNames.projectFullPath)?.EvaluatedValue;

                // Combine SDK path with the "project" relative path
                sdkResult = _sdkResolverService.ResolveSdk(_submissionId, importElement.ParsedSdkReference, _evaluationLoggingContext, importElement.Location, solutionPath, projectPath);

                if (!sdkResult.Success)
                {
                    if (_loadSettings.HasFlag(ProjectLoadSettings.IgnoreMissingImports))
                    {
                        ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                            importElement.Location.Line,
                            importElement.Location.Column,
                            ResourceUtilities.GetResourceString("CouldNotResolveSdk"),
                            importElement.ParsedSdkReference.ToString())
                        {
                            BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                            UnexpandedProject = importElement.Project,
                            ProjectFile = importElement.ContainingProject.FullPath,
                            ImportedProjectFile = null,
                            ImportIgnored = true,
                        };

                        _evaluationLoggingContext.LogBuildEvent(eventArgs);

                        projects = new List<ProjectRootElement>();

                        return;
                    }

                    ProjectErrorUtilities.ThrowInvalidProject(importElement.SdkLocation, "CouldNotResolveSdk", importElement.ParsedSdkReference.ToString());
                }

                project = Path.Combine(sdkResult.Path, project);
            }

            ExpandAndLoadImportsFromUnescapedImportExpression(directoryOfImportingFile, importElement, project,
                throwOnFileNotExistsError, out projects);
        }

        /// <summary>
        /// Load and parse the specified project import, which may have wildcards,
        /// into one or more ProjectRootElements.
        /// Caches the parsed import into the provided collection, so future 
        /// requests can be satisfied without re-parsing it.
        /// </summary>
        private LoadImportsResult ExpandAndLoadImportsFromUnescapedImportExpression(string directoryOfImportingFile, ProjectImportElement importElement, string unescapedExpression,
                                            bool throwOnFileNotExistsError, out List<ProjectRootElement> imports)
        {
            string importExpressionEscaped = _expander.ExpandIntoStringLeaveEscaped(unescapedExpression, ExpanderOptions.ExpandProperties, importElement.ProjectLocation);
            ElementLocation importLocationInProject = importElement.Location;

            if (String.IsNullOrWhiteSpace(importExpressionEscaped))
            {
                ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "InvalidAttributeValue", String.Empty, XMakeAttributes.project, XMakeElements.import);
            }

            bool atleastOneImportIgnored = false;
            bool atleastOneImportEmpty = false;
            imports = new List<ProjectRootElement>();

            foreach (string importExpressionEscapedItem in ExpressionShredder.SplitSemiColonSeparatedList(importExpressionEscaped))
            {
                string[] importFilesEscaped = null;

                try
                {
                    // Handle the case of an expression expanding to nothing specially;
                    // force an exception here to give a nicer message, that doesn't show the project directory in it.
                    if (importExpressionEscapedItem.Length == 0 || importExpressionEscapedItem.Trim().Length == 0)
                    {
                        FileUtilities.NormalizePath(EscapingUtilities.UnescapeAll(importExpressionEscapedItem));
                    }

                    // Expand the wildcards and provide an alphabetical order list of import statements.
                    importFilesEscaped = EngineFileUtilities.Default.GetFileListEscaped(directoryOfImportingFile, importExpressionEscapedItem, forceEvaluate: true);
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "InvalidAttributeValueWithException", EscapingUtilities.UnescapeAll(importExpressionEscapedItem), XMakeAttributes.project, XMakeElements.import, ex.Message);
                }

                if (importFilesEscaped.Length == 0)
                {
                    // Keep track of any imports that evaluated to empty
                    atleastOneImportEmpty = true;

                    if (_logProjectImportedEvents)
                    {
                        ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                            importElement.Location.Line,
                            importElement.Location.Column,
                            ResourceUtilities.GetResourceString("ProjectImportSkippedNoMatches"),
                            importExpressionEscapedItem,
                            importElement.ContainingProject.FullPath,
                            importElement.Location.Line,
                            importElement.Location.Column)
                        {
                            BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                            UnexpandedProject = importElement.Project,
                            ProjectFile = importElement.ContainingProject.FullPath,
                        };

                        _evaluationLoggingContext.LogBuildEvent(eventArgs);
                    }
                }

                foreach (string importFileEscaped in importFilesEscaped)
                {
                    string importFileUnescaped = EscapingUtilities.UnescapeAll(importFileEscaped);

                    // GetFileListEscaped may not return a rooted path, we need to root it. Also if there are no wild cards we still need to get the full path on the filespec.
                    try
                    {
                        if (directoryOfImportingFile != null && !Path.IsPathRooted(importFileUnescaped))
                        {
                            importFileUnescaped = Path.Combine(directoryOfImportingFile, importFileUnescaped);
                        }

                        // Canonicalize to eg., eliminate "\..\"
                        importFileUnescaped = FileUtilities.NormalizePath(importFileUnescaped);
                    }
                    catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "InvalidAttributeValueWithException", importFileUnescaped, XMakeAttributes.project, XMakeElements.import, ex.Message);
                    }

                    // If a file is included twice, or there is a cycle of imports, we ignore all but the first import
                    // and issue a warning to that effect.
                    if (String.Equals(_projectRootElement.FullPath, importFileUnescaped, StringComparison.OrdinalIgnoreCase) /* We are trying to import ourselves */)
                    {
                        _evaluationLoggingContext.LogWarning(null, new BuildEventFileInfo(importLocationInProject), "SelfImport", importFileUnescaped);
                        atleastOneImportIgnored = true;

                        continue;
                    }

                    // Circular dependencies (e.g. t0.targets imports t1.targets, t1.targets imports t2.targets and t2.targets imports t0.targets) will be
                    // caught by the check for duplicate imports which is done later in the method. However, if the project load setting requires throwing
                    // on circular imports or recording duplicate-but-not-circular imports, then we need to do exclusive check for circular imports here.
                    if ((_loadSettings & ProjectLoadSettings.RejectCircularImports) != 0 || (_loadSettings & ProjectLoadSettings.RecordDuplicateButNotCircularImports) != 0)
                    {
                        // Check if this import introduces circularity.
                        if (IntroducesCircularity(importFileUnescaped, importElement))
                        {
                            // Get the full path of the MSBuild file that has this import.
                            string importedBy = importElement.ContainingProject.FullPath ?? String.Empty;

                            _evaluationLoggingContext.LogWarning(null, new BuildEventFileInfo(importLocationInProject), "ImportIntroducesCircularity", importFileUnescaped, importedBy);

                            // Throw exception if the project load settings requires us to stop the evaluation of a project when circular imports are detected.
                            if ((_loadSettings & ProjectLoadSettings.RejectCircularImports) != 0)
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "ImportIntroducesCircularity", importFileUnescaped, importedBy);
                            }

                            // Ignore this import and no more further processing on it.
                            atleastOneImportIgnored = true;
                            continue;
                        }
                    }

                    ProjectImportElement previouslyImportedAt;
                    bool duplicateImport = false;

                    if (_importsSeen.TryGetValue(importFileUnescaped, out previouslyImportedAt))
                    {
                        string parenthesizedProjectLocation = String.Empty;

                        // If neither file involved is the project itself, append its path in square brackets
                        if (previouslyImportedAt.ContainingProject != _projectRootElement && importElement.ContainingProject != _projectRootElement)
                        {
                            parenthesizedProjectLocation = "[" + _projectRootElement.FullPath + "]";
                        }
                        // TODO: Detect if the duplicate import came from an SDK attribute
                        _evaluationLoggingContext.LogWarning(null, new BuildEventFileInfo(importLocationInProject), "DuplicateImport", importFileUnescaped, previouslyImportedAt.Location.LocationString, parenthesizedProjectLocation);
                        duplicateImport = true;
                    }

                    ProjectRootElement importedProjectElement;

                    try
                    {
                        // We take the explicit loaded flag from the project ultimately being evaluated.  The goal being that
                        // if a project system loaded a user's project, all imports (which would include property sheets and .user file)
                        // may impact evaluation and should be included in the weak cache without ever being cleared out to avoid
                        // the project system being exposed to multiple PRE instances for the same file.  We only want to consider
                        // clearing the weak cache (and therefore setting explicitload=false) for projects the project system never
                        // was directly interested in (i.e. the ones that were reached for purposes of building a P2P.)
                        bool explicitlyLoaded = importElement.ContainingProject.IsExplicitlyLoaded;
                        importedProjectElement = _projectRootElementCache.Get(
                            importFileUnescaped,
                            (p, c) =>
                            {
                                return ProjectRootElement.OpenProjectOrSolution(
                                    importFileUnescaped,
                                    new ReadOnlyConvertingDictionary<string, ProjectPropertyInstance, string>(
                                        _data.GlobalPropertiesDictionary,
                                        instance => ((IProperty)instance).EvaluatedValueEscaped),
                                    _data.ExplicitToolsVersion,
                                    _projectRootElementCache,
                                    explicitlyLoaded);
                            },
                            explicitlyLoaded,
                            // don't care about formatting, reuse whatever is there
                            preserveFormatting: null);

                        if (duplicateImport)
                        {
                            // Only record the data if we want to record duplicate imports
                            if ((_loadSettings & ProjectLoadSettings.RecordDuplicateButNotCircularImports) != 0)
                            {
                                _data.RecordImportWithDuplicates(importElement, importedProjectElement,
                                    importedProjectElement.Version);
                            }

                            // Since we have already seen this we need to not continue on in the processing.
                            atleastOneImportIgnored = true;
                            continue;
                        }
                        else
                        {
                            imports.Add(importedProjectElement);

                            if (_logProjectImportedEvents)
                            {
                                ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                                    importElement.Location.Line,
                                    importElement.Location.Column,
                                    ResourceUtilities.GetResourceString("ProjectImported"),
                                    importedProjectElement.FullPath,
                                    importElement.ContainingProject.FullPath,
                                    importElement.Location.Line,
                                    importElement.Location.Column)
                                {
                                    BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                                    ImportedProjectFile = importedProjectElement.FullPath,
                                    UnexpandedProject = importElement.Project,
                                    ProjectFile = importElement.ContainingProject.FullPath
                                };

                                _evaluationLoggingContext.LogBuildEvent(eventArgs);
                            }
                        }
                    }
                    catch (InvalidProjectFileException ex)
                    {
                        // The import couldn't be read from disk, or something similar. In that case,
                        // the error message would be more useful if it pointed to the location in the importing project file instead.
                        // Perhaps the import tag has a typo in, for example.

                        // There's a specific message for file not existing
                        if (!File.Exists(importFileUnescaped))
                        {
                            bool ignoreMissingImportsFlagSet = (_loadSettings & ProjectLoadSettings.IgnoreMissingImports) != 0;
                            if (!throwOnFileNotExistsError || ignoreMissingImportsFlagSet)
                            {
                                if (ignoreMissingImportsFlagSet)
                                {
                                    // Log message for import skipped
                                    ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                                        importElement.Location.Line,
                                        importElement.Location.Column,
                                        ResourceUtilities.GetResourceString("ProjectImportSkippedMissingFile"),
                                        importFileUnescaped,
                                        importElement.ContainingProject.FullPath,
                                        importElement.Location.Line,
                                        importElement.Location.Column)
                                    {
                                        BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                                        UnexpandedProject = importElement.Project,
                                        ProjectFile = importElement.ContainingProject.FullPath,
                                        ImportedProjectFile = importFileUnescaped,
                                        ImportIgnored = true,
                                    };

                                    _evaluationLoggingContext.LogBuildEvent(eventArgs);
                                }


                                continue;
                            }

                            ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "ImportedProjectNotFound",
                                importFileUnescaped);
                        }
                        else
                        {
                            bool ignoreImport = false;
                            string ignoreImportResource = null;

                            if (((_loadSettings & ProjectLoadSettings.IgnoreEmptyImports) != 0 || Traits.Instance.EscapeHatches.IgnoreEmptyImports) && ProjectRootElement.IsEmptyXmlFile(importFileUnescaped))
                            {
                                // If IgnoreEmptyImports is enabled, check if the file is considered empty
                                //
                                ignoreImport = true;
                                ignoreImportResource = "ProjectImportSkippedEmptyFile";
                            }
                            else if ((_loadSettings & ProjectLoadSettings.IgnoreInvalidImports) != 0)
                            {
                                // If IgnoreInvalidImports is enabled, log all other non-handled exceptions and continue
                                //
                                ignoreImport = true;
                                ignoreImportResource = "ProjectImportSkippedInvalidFile";
                            }

                            if (ignoreImport)
                            {
                                atleastOneImportIgnored = true;

                                // Log message for import skipped
                                ProjectImportedEventArgs eventArgs = new ProjectImportedEventArgs(
                                    importElement.Location.Line,
                                    importElement.Location.Column,
                                    ResourceUtilities.GetResourceString(ignoreImportResource),
                                    importFileUnescaped,
                                    importElement.ContainingProject.FullPath,
                                    importElement.Location.Line,
                                    importElement.Location.Column)
                                {
                                    BuildEventContext = _evaluationLoggingContext.BuildEventContext,
                                    UnexpandedProject = importElement.Project,
                                    ProjectFile = importElement.ContainingProject.FullPath,
                                    ImportedProjectFile = importFileUnescaped,
                                    ImportIgnored = true,
                                };

                                _evaluationLoggingContext.LogBuildEvent(eventArgs);

                                continue;
                            }

                            // If this exception is a wrapped exception (like IOException or XmlException) then wrap it as an invalid import instead
                            if (ex.InnerException != null)
                            {
                                // Otherwise a more generic message, still pointing to the location of the import tag
                                ProjectErrorUtilities.ThrowInvalidProject(importLocationInProject, "InvalidImportedProjectFile",
                                    importFileUnescaped, ex.InnerException.Message);
                            }

                            // Throw the original InvalidProjectFileException because it has no InnerException and was not wrapping something else
                            throw;
                        }
                    }

                    // Because these expressions will never be expanded again, we 
                    // can store the unescaped value. The only purpose of escaping is to 
                    // avoid undesired splitting or expansion.
                    _importsSeen.Add(importFileUnescaped, importElement);
                } 
            }

            if (imports.Count > 0)
            {
                return LoadImportsResult.ProjectsImported;
            }

            if (atleastOneImportIgnored)
            {
                return LoadImportsResult.FoundFilesToImportButIgnored;
            }

            if (atleastOneImportEmpty)
            {
                // One or more expression resolved to "", eg. a wildcard
                return LoadImportsResult.ImportExpressionResolvedToNothing;
            }

            // No projects were imported, none were ignored but we did have atleast
            // one file to process, which means that we did try to load a file but
            // failed w/o an exception escaping from here.
            // We ignore only the file not existing error, so, that is the case here
            // (if @throwOnFileNotExistsError==true, then it would have thrown
            //  and we wouldn't be here)
            return LoadImportsResult.TriedToImportButFileNotFound;
        }

        /// <summary>
        /// Checks if an import matches with another import in its ancestor line of imports.
        /// </summary>
        /// <param name="importFileUnescaped"> The import that is being added. </param>
        /// <param name="importElement"> The importing element for this import. </param>
        /// <returns> True, if and only if this import introduces a circularity. </returns>
        private bool IntroducesCircularity(string importFileUnescaped, ProjectImportElement importElement)
        {
            bool foundMatchingAncestor = false;

            // While we haven't found a matching ancestor haven't reach the project node,
            // keep climbing the import chain and checking for matches.
            while (importElement != null)
            {
                // Get the full path of the MSBuild file that imports this file.
                string importedBy = importElement.ContainingProject.FullPath;

                if (String.Equals(importFileUnescaped, importedBy, StringComparison.OrdinalIgnoreCase))
                {
                    // Circular dependency found!
                    foundMatchingAncestor = true;
                    break;
                }

                if (!String.IsNullOrEmpty(importedBy)) // The full path of a project loaded from memory can be null.
                {
                    // Set the "counter" to the importing project.
                    _importsSeen.TryGetValue(importedBy, out importElement);
                }
                else
                {
                    importElement = null;
                }
            }

            return foundMatchingAncestor;
        }

        /// <summary>
        /// Evaluate a given condition
        /// </summary>
        private bool EvaluateCondition(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element, element.Condition, expanderOptions, parserOptions);
        }

        private bool EvaluateCondition(ProjectElement element, string condition, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            if (condition.Length == 0)
            {
                return true;
            }

            using (_evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
            {
                bool result = ConditionEvaluator.EvaluateCondition
                    (
                    condition,
                    parserOptions,
                    _expander,
                    expanderOptions,
                    GetCurrentDirectoryForConditionEvaluation(element),
                    element.ConditionLocation,
                    _evaluationLoggingContext.LoggingService,
                    _evaluationLoggingContext.BuildEventContext
                    );

                return result;
            }
        }

        private bool EvaluateConditionCollectingConditionedProperties(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions, ProjectRootElementCache projectRootElementCache = null)
        {
            return EvaluateConditionCollectingConditionedProperties(element, element.Condition, expanderOptions, parserOptions, projectRootElementCache);
        }

        /// <summary>
        /// Evaluate a given condition, collecting conditioned properties.
        /// </summary>
        private bool EvaluateConditionCollectingConditionedProperties(ProjectElement element, string condition, ExpanderOptions expanderOptions, ParserOptions parserOptions, ProjectRootElementCache projectRootElementCache = null)
        {
            if (condition.Length == 0)
            {
                return true;
            }

            if (!_data.ShouldEvaluateForDesignTime)
            {
                return EvaluateCondition(element, condition, expanderOptions, parserOptions);
            }

            using (_evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
            {
                bool result = ConditionEvaluator.EvaluateConditionCollectingConditionedProperties
                    (
                    condition,
                    parserOptions,
                    _expander,
                    expanderOptions,
                    _data.ConditionedProperties,
                    GetCurrentDirectoryForConditionEvaluation(element),
                    element.ConditionLocation,
                    _evaluationLoggingContext.LoggingService,
                    _evaluationLoggingContext.BuildEventContext,
                    projectRootElementCache
                    );

                return result;
            }
        }

        /// <summary>
        /// COMPAT: Whidbey used the "current project file/targets" directory for evaluating Import and PropertyGroup conditions
        /// Orcas broke this by using the current root project file for all conditions
        /// For Dev10+, we'll fix this, and use the current project file/targets directory for Import, ImportGroup and PropertyGroup
        /// but the root project file for the rest. Inside of targets will use the root project file as always.
        /// </summary>
        private string GetCurrentDirectoryForConditionEvaluation(ProjectElement element)
        {
            if (element is ProjectPropertyGroupElement || element is ProjectImportElement || element is ProjectImportGroupElement)
            {
                return element.ContainingProject.DirectoryPath;
            }
            else
            {
                return _data.Directory;
            }
        }

        private void RecordEvaluatedItemElement(ProjectItemElement itemElement)
        {
            if (_loadSettings.HasFlag(ProjectLoadSettings.RecordEvaluatedItemElements))
            {
                _data.EvaluatedItemElements.Add(itemElement);
            }
        }

        /// <summary>
        /// Throws InvalidProjectException because we failed to import a project which contained a ProjectImportSearchPath fall-back.
        /// <param name="searchPathMatch">MSBuildExtensionsPath reference kind found in the Project attribute of the Import element</param>
        /// <param name="importElement">The importing element for this import</param>
        /// </summary>
        private void ThrowForImportedProjectWithSearchPathsNotFound(ProjectImportPathMatch searchPathMatch, ProjectImportElement importElement)
        {
            var extensionsPathProp = _data.GetProperty(searchPathMatch.PropertyName);
            string importExpandedWithDefaultPath;
            string relativeProjectPath;

            if (extensionsPathProp != null)
            {
                string extensionsPathPropValue = extensionsPathProp.EvaluatedValue;
                importExpandedWithDefaultPath =
                    _expander.ExpandIntoStringLeaveEscaped(
                        importElement.Project.Replace(searchPathMatch.MsBuildPropertyFormat, extensionsPathPropValue),
                        ExpanderOptions.ExpandProperties, importElement.ProjectLocation);

                relativeProjectPath = FileUtilities.MakeRelative(extensionsPathPropValue, importExpandedWithDefaultPath);
            }
            else
            {
                // If we can't get the original property, just use the actual text from the project file in the error message.
                // This should be a very rare case where the toolset is out of sync with the fallback. This will resolve
                // a null ref calling EvaluatedValue on the property.
                importExpandedWithDefaultPath = importElement.Project;
                relativeProjectPath = importElement.Project;
            }

            var onlyFallbackSearchPaths = searchPathMatch.SearchPaths.Select(s => _data.ExpandString(s)).ToList();

            string stringifiedListOfSearchPaths = StringifyList(onlyFallbackSearchPaths);

#if FEATURE_SYSTEM_CONFIGURATION
            string configLocation = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            ProjectErrorUtilities.ThrowInvalidProject(importElement.ProjectLocation,
                "ImportedProjectFromExtensionsPathNotFoundFromAppConfig",
                importExpandedWithDefaultPath,
                relativeProjectPath,
                searchPathMatch.MsBuildPropertyFormat,
                stringifiedListOfSearchPaths,
                configLocation);
#else
            ProjectErrorUtilities.ThrowInvalidProject(importElement.ProjectLocation, "ImportedProjectFromExtensionsPathNotFound",
                                                        importExpandedWithDefaultPath,
                                                        relativeProjectPath,
                                                        searchPathMatch.MsBuildPropertyFormat,
                                                        stringifiedListOfSearchPaths);
#endif
        }

        /// <summary>
        /// Stringify a list of strings, like {"abc, "def", "foo"} to "abc, def and foo"
        /// or {"abc"} to "abc"
        /// <param name="strings">List of strings to stringify</param>
        /// <returns>Stringified list</returns>
        /// </summary>
        private static string StringifyList(IList<string> strings)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < strings.Count - 1; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"\"{strings[i]}\"");
            }

            if (strings.Count > 1)
            {
                sb.Append(" and ");
            }

            sb.Append($"\"{strings[strings.Count - 1]}\"");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents result of attempting to load imports (ExpandAndLoadImportsFromUnescapedImportExpression*)
    /// </summary>
    internal enum LoadImportsResult
    {
        ProjectsImported,
        FoundFilesToImportButIgnored,
        TriedToImportButFileNotFound,
        ImportExpressionResolvedToNothing,
        ConditionWasFalse
    }
}
