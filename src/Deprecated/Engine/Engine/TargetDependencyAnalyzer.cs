// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Enumeration of the results of target dependency analysis.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal enum DependencyAnalysisResult
    {
        SkipUpToDate,
        SkipNoInputs,
        SkipNoOutputs,
        IncrementalBuild,
        FullBuild
    }

    /// <summary>
    /// This class is used for performing dependency analysis on targets to determine if they should be built/rebuilt/skipped.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class TargetDependencyAnalyzer
    {
        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the given target.
        /// </summary>
        /// <owner>SumedhK</owner>
        internal TargetDependencyAnalyzer(string projectDirectory, Target targetToAnalyze, EngineLoggingServices loggingServices, BuildEventContext buildEventContext)
        {
            ErrorUtilities.VerifyThrow(projectDirectory != null, "Need a project directory.");
            ErrorUtilities.VerifyThrow(targetToAnalyze != null, "Need a target to analyze.");
            ErrorUtilities.VerifyThrow(targetToAnalyze.TargetElement != null, "Need a target element.");

            this.projectDirectory = projectDirectory;
            this.targetToAnalyze = targetToAnalyze;
            this.targetInputsAttribute = targetToAnalyze.TargetElement.Attributes[XMakeAttributes.inputs];
            this.targetOutputsAttribute = targetToAnalyze.TargetElement.Attributes[XMakeAttributes.outputs];
            this.loggingService = loggingServices;
            this.buildEventContext = buildEventContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the target to perform dependency analysis on.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Target object.</value>
        internal Target TargetToAnalyze
        {
            get
            {
                return targetToAnalyze;
            }
        }

               /// <summary>
        /// Gets the value of the target's "Inputs" attribute.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Input specification string (can be empty).</value>
        private string TargetInputSpecification
        {
            get
            {
                if (targetInputSpecification == null)
                {
                    targetInputSpecification = ((targetInputsAttribute != null) ? targetInputsAttribute.Value : String.Empty);
                }

                return targetInputSpecification;
            }
        }

        /// <summary>
        /// Gets the value of the target's "Outputs" attribute.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Output specification string (can be empty).</value>
        private string TargetOutputSpecification
        {
            get
            {
                if (targetOutputSpecification == null)
                {
                    targetOutputSpecification = ((targetOutputsAttribute != null) ? targetOutputsAttribute.Value : String.Empty);
                }

                return targetOutputSpecification;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Compares the target's inputs against its outputs to determine if the target needs to be built/rebuilt/skipped.
        /// </summary>
        /// <remarks>
        /// The collections of changed and up-to-date inputs returned from this method are valid IFF this method decides an
        /// incremental build is needed.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="bucket"></param>
        /// <param name="changedTargetInputs"></param>
        /// <param name="upToDateTargetInputs"></param>
        /// <returns>
        /// DependencyAnalysisResult.SkipUpToDate, if target is up-to-date;
        /// DependencyAnalysisResult.SkipNoInputs, if target has no inputs;
        /// DependencyAnalysisResult.SkipNoOutputs, if target has no outputs;
        /// DependencyAnalysisResult.IncrementalBuild, if only some target outputs are out-of-date;
        /// DependencyAnalysisResult.FullBuild, if target is out-of-date
        /// </returns>
        internal DependencyAnalysisResult PerformDependencyAnalysis
        (
            ItemBucket bucket,
            out Hashtable changedTargetInputs,
            out Hashtable upToDateTargetInputs
        )
        {
            // Clear any old dependency analysis logging details
            dependencyAnalysisDetail.Clear();
            uniqueTargetInputs.Clear();
            uniqueTargetOutputs.Clear();

            ProjectErrorUtilities.VerifyThrowInvalidProject((TargetOutputSpecification.Length > 0) || (TargetInputSpecification.Length == 0),
                this.TargetToAnalyze.TargetElement, "TargetInputsSpecifiedWithoutOutputs", TargetToAnalyze.Name);

            DependencyAnalysisResult result = DependencyAnalysisResult.SkipUpToDate;

            changedTargetInputs = null;
            upToDateTargetInputs = null;

            if (TargetOutputSpecification.Length == 0)
            {
                // if the target has no output specification, we always build it
                result = DependencyAnalysisResult.FullBuild;
            }
            else
            {
                Hashtable itemVectorsInTargetInputs;
                Hashtable itemVectorTransformsInTargetInputs;
                Hashtable discreteItemsInTargetInputs;

                Hashtable itemVectorsInTargetOutputs;
                Hashtable discreteItemsInTargetOutputs;
                ArrayList targetOutputItemSpecs;

                ParseTargetInputOutputSpecifications(bucket,
                    out itemVectorsInTargetInputs,
                    out itemVectorTransformsInTargetInputs,
                    out discreteItemsInTargetInputs,
                    out itemVectorsInTargetOutputs,
                    out discreteItemsInTargetOutputs,
                    out targetOutputItemSpecs);

                ArrayList itemVectorsReferencedInBothTargetInputsAndOutputs;
                ArrayList itemVectorsReferencedOnlyInTargetInputs;
                ArrayList itemVectorsReferencedOnlyInTargetOutputs = null;

                // if the target has no outputs because the output specification evaluated to empty
                if (targetOutputItemSpecs.Count == 0)
                {
                    result = PerformDependencyAnalysisIfNoOutputs();
                }
                // if there are no discrete output items...
                else if (discreteItemsInTargetOutputs.Count == 0)
                {
                    // try to correlate inputs and outputs by checking:
                    // 1) which item vectors are referenced by both input and output items
                    // 2) which item vectors are referenced only by input items
                    // 3) which item vectors are referenced only by output items
                    // NOTE: two item vector transforms cannot be correlated, even if they reference the same item vector, because
                    // depending on the transform expression, there might be no relation between the results of the transforms; as
                    // a result, input items that are item vector transforms are treated as discrete items
                    DiffHashtables(itemVectorsInTargetInputs, itemVectorsInTargetOutputs,
                        out itemVectorsReferencedInBothTargetInputsAndOutputs,
                        out itemVectorsReferencedOnlyInTargetInputs,
                        out itemVectorsReferencedOnlyInTargetOutputs);

                    // if there are no item vectors only referenced by output items...
                    // NOTE: we consider output items that reference item vectors not referenced by any input item, as discrete
                    // items, since we cannot correlate them to any input items
                    if (itemVectorsReferencedOnlyInTargetOutputs.Count == 0)
                    {
                        /*
                         * At this point, we know the following:
                         * 1) the target has outputs
                         * 2) the target has NO discrete outputs
                         * 
                         * This implies:
                         * 1) the target only references vectors (incl. transforms) in its outputs
                         * 2) all vectors referenced in the outputs are also referenced in the inputs
                         * 3) the referenced vectors are not empty
                         * 
                         * We can thus conclude: the target MUST have (non-discrete) inputs
                         * 
                         */
                        ErrorUtilities.VerifyThrow(itemVectorsReferencedInBothTargetInputsAndOutputs.Count > 0, "The target must have inputs.");
                        Debug.Assert(GetItemSpecsFromItemVectors(itemVectorsInTargetInputs).Count > 0, "The target must have inputs.");

                        result = PerformDependencyAnalysisIfDiscreteInputs(itemVectorsInTargetInputs,
                                    itemVectorTransformsInTargetInputs, discreteItemsInTargetInputs, itemVectorsReferencedOnlyInTargetInputs,
                                    targetOutputItemSpecs);

                        if (result != DependencyAnalysisResult.FullBuild)
                        {
                            // once the inputs and outputs have been correlated, we can do a 1-to-1 comparison between each input
                            // and its corresponding output, to discover which inputs have changed, and which are up-to-date...
                            result = PerformDependencyAnalysisIfCorrelatedInputsOutputs(itemVectorsInTargetInputs, itemVectorsInTargetOutputs,
                                itemVectorsReferencedInBothTargetInputsAndOutputs,
                                out changedTargetInputs, out upToDateTargetInputs);
                        }
                    }
                }

                // if there are any discrete items in the target outputs, then we have no obvious correlation to the inputs they
                // depend on, since any input can contribute to a discrete output, so we compare all inputs against all outputs
                // NOTE: output items are considered discrete, if
                // 1) they do not reference any item vector
                // 2) they reference item vectors that are not referenced by any input item
                if ((discreteItemsInTargetOutputs.Count > 0) ||
                    ((itemVectorsReferencedOnlyInTargetOutputs?.Count > 0)))
                {
                    result = PerformDependencyAnalysisIfDiscreteOutputs(
                                itemVectorsInTargetInputs, itemVectorTransformsInTargetInputs, discreteItemsInTargetInputs,
                                targetOutputItemSpecs);
                }

                if (result == DependencyAnalysisResult.SkipUpToDate)
                {
                    loggingService.LogComment(buildEventContext, MessageImportance.Normal,
                        "SkipTargetBecauseOutputsUpToDate",
                        TargetToAnalyze.Name);
                    
                    // Log the target inputs & outputs
                    if (!loggingService.OnlyLogCriticalEvents)
                    {
                        string inputs;
                        string outputs;
                        // Extract the unique inputs and outputs gatheres during TLDA
                        ExtractUniqueInputsAndOutputs(out inputs, out outputs);

                        if (inputs != null)
                        {
                            loggingService.LogComment(buildEventContext, MessageImportance.Low, "SkipTargetUpToDateInputs", inputs);
                        }

                        if (outputs != null)
                        {
                            loggingService.LogComment(buildEventContext, MessageImportance.Low, "SkipTargetUpToDateOutputs", outputs);
                        }
                    }
                }
            }

            LogReasonForBuildingTarget(result);

            return result;
        }

        /// <summary>
        /// Does appropriate logging to indicate why this target is being built fully or partially.
        /// </summary>
        /// <param name="result"></param>
        private void LogReasonForBuildingTarget(DependencyAnalysisResult result)
        {
            // Only if we are not logging just critical events should we be logging the details
            if (!loggingService.OnlyLogCriticalEvents)
            {
                if (result == DependencyAnalysisResult.FullBuild && this.dependencyAnalysisDetail.Count > 0)
                {
                    // For the full build decision the are three possible outcomes
                    loggingService.LogComment(buildEventContext,"BuildTargetCompletely", this.targetToAnalyze.Name);

                    foreach (DependencyAnalysisLogDetail logDetail in this.dependencyAnalysisDetail)
                    {
                        string reason = GetFullBuildReason(logDetail);
                        loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, reason);
                    }
                }
                else if (result == DependencyAnalysisResult.IncrementalBuild)
                {
                    // For the partial build decision the are three possible outcomes
                    loggingService.LogComment(buildEventContext, MessageImportance.Normal, "BuildTargetPartially", this.targetToAnalyze.Name);
                    foreach (DependencyAnalysisLogDetail logDetail in this.dependencyAnalysisDetail)
                    {
                        string reason = GetIncrementalBuildReason(logDetail);
                        loggingService.LogCommentFromText(buildEventContext, MessageImportance.Low, reason);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a string indicating why a full build is occurring.
        /// </summary>
        internal static string GetFullBuildReason(DependencyAnalysisLogDetail logDetail)
        {
            string reason = null;

            if (logDetail.Reason == OutofdateReason.NewerInput)
            {
                // One of the inputs was newer than all of the outputs
                reason = ResourceUtilities.FormatResourceString("BuildTargetCompletelyInputNewer", logDetail.Input, logDetail.Output);
            }
            else if (logDetail.Reason == OutofdateReason.MissingOutput)
            {
                // One of the outputs was missing
                reason = ResourceUtilities.FormatResourceString("BuildTargetCompletelyOutputDoesntExist", logDetail.Output);
            }
            else if (logDetail.Reason == OutofdateReason.MissingInput)
            {
                // One of the inputs was missing
                reason = ResourceUtilities.FormatResourceString("BuildTargetCompletelyInputDoesntExist", logDetail.Input);
            }

            return reason;
        }

        /// <summary>
        /// Returns a string indicating why an incremental build is occurring.
        /// </summary>
        private static string GetIncrementalBuildReason(DependencyAnalysisLogDetail logDetail)
        {
            string reason = null;

            if (logDetail.Reason == OutofdateReason.NewerInput)
            {
                // One of the inputs was newer than its corresponding output
                reason = ResourceUtilities.FormatResourceString("BuildTargetPartiallyInputNewer", logDetail.InputItemName, logDetail.Input, logDetail.Output);
            }
            else if (logDetail.Reason == OutofdateReason.MissingOutput)
            {
                // One of the outputs was missing
                reason = ResourceUtilities.FormatResourceString("BuildTargetPartiallyOutputDoesntExist", logDetail.OutputItemName, logDetail.Input, logDetail.Output);
            }
            else if (logDetail.Reason == OutofdateReason.MissingInput)
            {
                // One of the inputs was missing
                reason = ResourceUtilities.FormatResourceString("BuildTargetPartiallyInputDoesntExist", logDetail.InputItemName, logDetail.Input, logDetail.Output);
            }

            return reason;
        }

        /// <summary>
        /// Extract only the unique inputs and outputs from all the inputs and outputs gathered
        /// during depedency analysis
        /// </summary>
        /// <param name="inputs">[out] the unique inputs</param>
        /// <param name="outputs">[out] the unique outputs</param>
        private void ExtractUniqueInputsAndOutputs(out string inputs, out string outputs)
        {
            if (this.uniqueTargetInputs.Count > 0)
            {
                StringBuilder inputsBuilder = new StringBuilder();
                // Each of our inputs needs to be added to the string
                foreach (string input in this.uniqueTargetInputs.Keys)
                {
                    inputsBuilder.Append(input);
                    inputsBuilder.Append(";");
                }
                // We don't want the trailing ; so remove it
                inputs = inputsBuilder.ToString(0, inputsBuilder.Length - 1);
            }
            else
            {
                inputs = String.Empty;
            }

            if (this.uniqueTargetOutputs.Count > 0)
            {
                StringBuilder outputsBuilder = new StringBuilder();
                // Each of our outputs needs to be added to the string
                foreach (string output in this.uniqueTargetOutputs.Keys)
                {
                    outputsBuilder.Append(output);
                    outputsBuilder.Append(";");
                }
                // We don't want the trailing ; so remove it
                outputs = outputsBuilder.ToString(0, outputsBuilder.Length - 1);
            }
            else
            {
                outputs = String.Empty;
            }
        }

        /// <summary>
        /// Parses the target's "Inputs" and "Outputs" attributes and gathers up referenced items.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="itemVectorsInTargetInputs"></param>
        /// <param name="itemVectorTransformsInTargetInputs"></param>
        /// <param name="discreteItemsInTargetInputs"></param>
        /// <param name="itemVectorsInTargetOutputs"></param>
        /// <param name="discreteItemsInTargetOutputs"></param>
        /// <param name="targetOutputItemSpecs"></param>
        /// <owner>SumedhK</owner>
        private void ParseTargetInputOutputSpecifications
        (
            ItemBucket bucket,
            out Hashtable itemVectorsInTargetInputs,
            out Hashtable itemVectorTransformsInTargetInputs,
            out Hashtable discreteItemsInTargetInputs,
            out Hashtable itemVectorsInTargetOutputs,
            out Hashtable discreteItemsInTargetOutputs,
            out ArrayList targetOutputItemSpecs
        )
        {
            // break down the input/output specifications along the standard separator, after expanding all embedded properties
            // and item metadata
            Expander propertyAndMetadataExpander = new Expander(bucket.Expander, ExpanderOptions.ExpandPropertiesAndMetadata);
            List<string> targetInputs = propertyAndMetadataExpander.ExpandAllIntoStringListLeaveEscaped(TargetInputSpecification, this.targetInputsAttribute);
            List<string> targetOutputs = propertyAndMetadataExpander.ExpandAllIntoStringListLeaveEscaped(TargetOutputSpecification, this.targetOutputsAttribute);

            itemVectorTransformsInTargetInputs = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // figure out which of the inputs are:
            // 1) item vectors
            // 2) item vectors with transforms
            // 3) "discrete" items i.e. items that do not reference item vectors
            SeparateItemVectorsFromDiscreteItems(this.targetInputsAttribute, targetInputs, bucket,
                out itemVectorsInTargetInputs,
                itemVectorTransformsInTargetInputs,
                out discreteItemsInTargetInputs);

            // figure out which of the outputs are:
            // 1) item vectors (with or without transforms)
            // 2) "discrete" items i.e. items that do not reference item vectors
            SeparateItemVectorsFromDiscreteItems(this.targetOutputsAttribute, targetOutputs, bucket,
                out itemVectorsInTargetOutputs,
                null /* don't want transforms separated */,
                out discreteItemsInTargetOutputs);

            // list out all the output item-specs
            targetOutputItemSpecs = GetItemSpecsFromItemVectors(itemVectorsInTargetOutputs);
            targetOutputItemSpecs.AddRange(discreteItemsInTargetOutputs.Values);
        }

        /// <summary>
        /// Determines if the target needs to be built/rebuilt/skipped if it has no outputs (because they evaluated to empty).
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <returns>Indication of how to build the target.</returns>
        private DependencyAnalysisResult PerformDependencyAnalysisIfNoOutputs()
        {
            DependencyAnalysisResult result = DependencyAnalysisResult.SkipNoOutputs;

            // If the target has no inputs declared and the outputs evaluated to empty, do a full build. Remember that somebody
            // may specify Outputs="@(blah)", where the item list "blah" is actually produced by some task within this target. So
            // at the beginning, when we're trying to do to the dependency analysis, there's nothing in the "blah" list, but after
            // the target executes, there will be.
            if (TargetInputSpecification.Length == 0)
            {
                result = DependencyAnalysisResult.FullBuild;
            }
            // otherwise, don't build the target
            else
            {
                loggingService.LogComment(buildEventContext, MessageImportance.Normal, 
                    "SkipTargetBecauseNoOutputs", TargetToAnalyze.Name);
                // detailed reason is low importance to keep log clean
                loggingService.LogComment(buildEventContext, MessageImportance.Low,
                    "SkipTargetBecauseNoOutputsDetail");
            }

            return result;
        }

        /// <summary>
        /// Determines if the target needs to be built/rebuilt/skipped if it has discrete inputs.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVectorsInTargetInputs"></param>
        /// <param name="itemVectorTransformsInTargetInputs"></param>
        /// <param name="discreteItemsInTargetInputs"></param>
        /// <param name="itemVectorsReferencedOnlyInTargetInputs"></param>
        /// <param name="targetOutputItemSpecs"></param>
        /// <returns>Indication of how to build the target.</returns>
        private DependencyAnalysisResult PerformDependencyAnalysisIfDiscreteInputs
        (
            Hashtable itemVectorsInTargetInputs,
            Hashtable itemVectorTransformsInTargetInputs,
            Hashtable discreteItemsInTargetInputs,
            ArrayList itemVectorsReferencedOnlyInTargetInputs,
            ArrayList targetOutputItemSpecs
        )
        {
            DependencyAnalysisResult result = DependencyAnalysisResult.SkipUpToDate;

            // list out all the discrete input item-specs...
            // NOTE: we treat input items that are item vector transforms, as discrete items, since we cannot correlate them to
            // any output item
            ArrayList discreteTargetInputItemSpecs = GetItemSpecsFromItemVectors(itemVectorTransformsInTargetInputs);
            discreteTargetInputItemSpecs.AddRange(discreteItemsInTargetInputs.Values);

            // we treat input items that reference item vectors not referenced by any output item, as discrete items, since we
            // cannot correlate them to any output item
            foreach (string itemVectorType in itemVectorsReferencedOnlyInTargetInputs)
            {
                discreteTargetInputItemSpecs.AddRange(GetItemSpecsFromItemVectors(itemVectorsInTargetInputs, itemVectorType));
            }

            // if there are any discrete input items, we can treat them as "meta" inputs, because:
            // 1) we have already confirmed there are no discrete output items
            // 2) apart from the discrete input items, we can correlate all input items to all output items, since we know they
            //    both reference the same item vectors
            // NOTES:
            // 1) a typical example of a "meta" input is when the project file itself is listed as an input -- this forces
            //    rebuilds when the project file changes, even if no actual inputs have changed
            // 2) discrete input items and discrete output items are not treated symmetrically, because it is more likely that
            //    an uncorrelated input is a "meta" input, than an uncorrelated output is a "meta" output, since outputs can
            //    typically be built out of more than one set of inputs
            if (discreteTargetInputItemSpecs.Count > 0)
            {
                // if any output item is out-of-date w.r.t. any discrete input item, do a full build
                DependencyAnalysisLogDetail dependencyAnalysisDetailEntry;
                bool someOutOfDate = IsAnyOutOfDate(out dependencyAnalysisDetailEntry, projectDirectory, discreteTargetInputItemSpecs, targetOutputItemSpecs);

                if (someOutOfDate)
                {
                    dependencyAnalysisDetail.Add(dependencyAnalysisDetailEntry);
                    result = DependencyAnalysisResult.FullBuild;
                }
                else
                {
                    RecordUniqueInputsAndOutputs(discreteTargetInputItemSpecs, targetOutputItemSpecs);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines if the target needs to be built/rebuilt/skipped if its inputs and outputs can be correlated.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVectorsInTargetInputs"></param>
        /// <param name="itemVectorsInTargetOutputs"></param>
        /// <param name="itemVectorsReferencedInBothTargetInputsAndOutputs"></param>
        /// <param name="changedTargetInputs"></param>
        /// <param name="upToDateTargetInputs"></param>
        /// <returns>Indication of how to build the target.</returns>
        private DependencyAnalysisResult PerformDependencyAnalysisIfCorrelatedInputsOutputs
        (
            Hashtable itemVectorsInTargetInputs,
            Hashtable itemVectorsInTargetOutputs,
            ArrayList itemVectorsReferencedInBothTargetInputsAndOutputs,
            out Hashtable changedTargetInputs,
            out Hashtable upToDateTargetInputs
        )
        {
            DependencyAnalysisResult result = DependencyAnalysisResult.SkipUpToDate;

            changedTargetInputs = new Hashtable(StringComparer.OrdinalIgnoreCase);
            upToDateTargetInputs = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // indicates if an incremental build is really just a full build, because all input items have changed
            int numberOfInputItemVectorsWithAllChangedItems = 0;

            foreach (string itemVectorType in itemVectorsReferencedInBothTargetInputsAndOutputs)
            {
                Hashtable inputItemVectors = (Hashtable)itemVectorsInTargetInputs[itemVectorType];
                Hashtable outputItemVectors = (Hashtable)itemVectorsInTargetOutputs[itemVectorType];

                // NOTE: recall that transforms have been separated out already
                ErrorUtilities.VerifyThrow(inputItemVectors.Count == 1,
                    "There should only be one item vector of a particular type in the target inputs that can be filtered.");

                // NOTE: this loop only makes one iteration
                foreach (BuildItemGroup inputItems in inputItemVectors.Values)
                {
                    if (inputItems.Count > 0)
                    {
                        BuildItemGroup changedInputItems = new BuildItemGroup();
                        BuildItemGroup upToDateInputItems = new BuildItemGroup();

                        BuildItem[] inputItemsAssumedToBeUpToDate = inputItems.ToArray();

                        foreach (DictionaryEntry outputEntry in outputItemVectors)
                        {
                            BuildItemGroup outputItems = (BuildItemGroup)outputEntry.Value;
                            // Get the metadata name so if it is missing we can print out a nice error message.
                            string outputItemExpression = (string)outputEntry.Key;

                            ErrorUtilities.VerifyThrow(inputItems.Count == outputItems.Count,
                                "An item vector of a particular type must always contain the same number of items.");

                            for (int i = 0; i < inputItemsAssumedToBeUpToDate.Length; i++)
                            {
                                // if we haven't already determined that this input item has changed
                                if (inputItemsAssumedToBeUpToDate[i] != null)
                                {
                                    // Check to see if the outputItem specification is null or empty, if that is the case we need to error out saying that some metadata
                                    // on one of the items used in the target output is missing.
                                    bool outputEscapedValueIsNullOrEmpty = String.IsNullOrEmpty(outputItems[i].FinalItemSpecEscaped);
                                    ProjectErrorUtilities.VerifyThrowInvalidProject(!outputEscapedValueIsNullOrEmpty,
                                                                                    this.TargetToAnalyze.TargetElement,
                                                                                    "TargetOutputsSpecifiedAreMissing",
                                                                                    inputItemsAssumedToBeUpToDate[i].FinalItemSpecEscaped,
                                                                                    outputItems[i].Name,
                                                                                    outputItemExpression,
                                                                                    TargetToAnalyze.Name);

                                    // check if it has changed
                                    bool outOfDate = IsOutOfDate(inputItemsAssumedToBeUpToDate[i].FinalItemSpecEscaped, outputItems[i].FinalItemSpecEscaped, inputItemsAssumedToBeUpToDate[i].Name, outputItems[i].Name);
                                    if (outOfDate)
                                    {
                                        changedInputItems.AddItem(inputItemsAssumedToBeUpToDate[i]);
                                        inputItemsAssumedToBeUpToDate[i] = null;

                                        result = DependencyAnalysisResult.IncrementalBuild;
                                    }
                                }
                            }

                            if (changedInputItems.Count == inputItems.Count)
                            {
                                numberOfInputItemVectorsWithAllChangedItems++;
                                break;
                            }
                        }

                        if (changedInputItems.Count < inputItems.Count)
                        {
                            foreach (BuildItem item in inputItemsAssumedToBeUpToDate)
                            {
                                if (item != null)
                                {
                                    upToDateInputItems.AddItem(item);
                                }
                            }
                        }

                        changedTargetInputs[itemVectorType] = changedInputItems;
                        upToDateTargetInputs[itemVectorType] = upToDateInputItems;
                    }
                }
            }

            ErrorUtilities.VerifyThrow(numberOfInputItemVectorsWithAllChangedItems <= itemVectorsReferencedInBothTargetInputsAndOutputs.Count,
                "The number of vectors containing all changed items cannot exceed the number of correlated vectors.");

            // if all correlated input items have changed
            if (numberOfInputItemVectorsWithAllChangedItems == itemVectorsReferencedInBothTargetInputsAndOutputs.Count)
            {
                ErrorUtilities.VerifyThrow(result == DependencyAnalysisResult.IncrementalBuild,
                    "If inputs have changed, this must be an incremental build.");

                // then the incremental build is really a full build
                result = DependencyAnalysisResult.FullBuild;
            }

            return result;
        }

        /// <summary>
        /// Determines if the target needs to be built/rebuilt/skipped if it has discrete outputs.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVectorsInTargetInputs"></param>
        /// <param name="itemVectorTransformsInTargetInputs"></param>
        /// <param name="discreteItemsInTargetInputs"></param>
        /// <param name="targetOutputItemSpecs"></param>
        /// <returns>Indication of how to build the target.</returns>
        private DependencyAnalysisResult PerformDependencyAnalysisIfDiscreteOutputs
        (
            Hashtable itemVectorsInTargetInputs,
            Hashtable itemVectorTransformsInTargetInputs,
            Hashtable discreteItemsInTargetInputs,
            ArrayList targetOutputItemSpecs
        )
        {
            DependencyAnalysisResult result = DependencyAnalysisResult.SkipUpToDate;

            ArrayList targetInputItemSpecs = GetItemSpecsFromItemVectors(itemVectorsInTargetInputs);
            targetInputItemSpecs.AddRange(GetItemSpecsFromItemVectors(itemVectorTransformsInTargetInputs));
            targetInputItemSpecs.AddRange(discreteItemsInTargetInputs.Values);

            // if the target has no inputs specified...
            if (targetInputItemSpecs.Count == 0)
            {
                // if the target did declare inputs, but the specification evaluated to nothing
                if (TargetInputSpecification.Length > 0)
                {
                    loggingService.LogComment(buildEventContext, MessageImportance.Normal, 
                        "SkipTargetBecauseNoInputs", TargetToAnalyze.Name);
                    // detailed reason is low importance to keep log clean
                    loggingService.LogComment(buildEventContext, MessageImportance.Low,
                        "SkipTargetBecauseNoInputsDetail");

                    // don't build the target
                    result = DependencyAnalysisResult.SkipNoInputs;
                }
                else
                {
                    // There were no inputs specified, so build completely
                    loggingService.LogComment(buildEventContext, "BuildTargetCompletely", this.targetToAnalyze.Name);
                    loggingService.LogComment(buildEventContext, "BuildTargetCompletelyNoInputsSpecified");

                    // otherwise, do a full build
                    result = DependencyAnalysisResult.FullBuild;
                }
            }
            // if any input is newer than any output, do a full build
            else
            {
                DependencyAnalysisLogDetail dependencyAnalysisDetailEntry;
                bool someOutOfDate =  IsAnyOutOfDate(out dependencyAnalysisDetailEntry, projectDirectory, targetInputItemSpecs, targetOutputItemSpecs);
   
                if (someOutOfDate)
                {
                    dependencyAnalysisDetail.Add(dependencyAnalysisDetailEntry);
                    result = DependencyAnalysisResult.FullBuild;
                }
                else
                {
                    RecordUniqueInputsAndOutputs(targetInputItemSpecs, targetOutputItemSpecs);
                }
            }

            return result;
        }

        /// <summary>
        /// Separates item vectors from discrete items, and discards duplicates. If requested, item vector transforms are also
        /// separated out. The item vectors (and the transforms) are partitioned by type, since there can be more than one item
        /// vector of the same type.
        /// </summary>
        /// <remarks>
        /// The item vector collection is a Hashtable of Hashtables, where the top-level Hashtable is indexed by item type, and
        /// each "partition" Hashtable is indexed by the item vector itself.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="attributeContainingItems">The XML attribute which we're operating on here.  
        /// The sole purpose of passing in this parameter is to be able to provide line/column number 
        /// information in the event there's an error.</param>
        /// <param name="items"></param>
        /// <param name="bucket"></param>
        /// <param name="itemVectors"></param>
        /// <param name="itemVectorTransforms"></param>
        /// <param name="discreteItems"></param>
        private void SeparateItemVectorsFromDiscreteItems
        (
            XmlAttribute attributeContainingItems,
            List<string> items,
            ItemBucket bucket,
            out Hashtable itemVectors,
            Hashtable itemVectorTransforms,
            out Hashtable discreteItems
        )
        {
            itemVectors = new Hashtable(StringComparer.OrdinalIgnoreCase);
            discreteItems = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (string item in items)
            {
                Match itemVectorMatch;
                BuildItemGroup itemVectorContents = bucket.Expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped(item, attributeContainingItems, out itemVectorMatch);
                if (itemVectorContents != null)
                {
                    Hashtable itemVectorCollection;
                    if ((itemVectorTransforms == null) ||
                        (itemVectorMatch.Groups["TRANSFORM_SPECIFICATION"].Length == 0))
                    {
                        itemVectorCollection = itemVectors;
                    }
                    else
                    {
                        itemVectorCollection = itemVectorTransforms;
                    }

                    string itemVectorType = itemVectorMatch.Groups["TYPE"].Value;
                    Hashtable itemVectorCollectionPartition = (Hashtable)itemVectorCollection[itemVectorType];

                    if (itemVectorCollectionPartition == null)
                    {
                        itemVectorCollectionPartition = new Hashtable(StringComparer.OrdinalIgnoreCase);
                        itemVectorCollection[itemVectorType] = itemVectorCollectionPartition;
                    }

                    itemVectorCollectionPartition[item] = itemVectorContents;

                    ErrorUtilities.VerifyThrow((itemVectorTransforms == null) || (itemVectorCollection.Equals(itemVectorTransforms)) || (itemVectorCollectionPartition.Count == 1),
                        "If transforms have been separated out, there should only be one item vector per partition.");
                }
                else
                {
                    discreteItems[item] = item;
                }
            }
        }

        /// <summary>
        /// Retrieves the item-specs of all items in the given item vector collection.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVectors"></param>
        /// <returns>list of item-specs</returns>
        private static ArrayList GetItemSpecsFromItemVectors(Hashtable itemVectors)
        {
            ArrayList itemSpecs = new ArrayList();

            foreach (string itemType in itemVectors.Keys)
            {
                itemSpecs.AddRange(GetItemSpecsFromItemVectors(itemVectors, itemType));
            }

            return itemSpecs;
        }

        /// <summary>
        /// Retrieves the item-specs of all items of the specified type in the given item vector collection.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVectors"></param>
        /// <param name="itemType"></param>
        /// <returns>list of item-specs</returns>
        private static ArrayList GetItemSpecsFromItemVectors(Hashtable itemVectors, string itemType)
        {
            ArrayList itemSpecs = new ArrayList();

            Hashtable itemVectorPartition = (Hashtable)itemVectors[itemType];

            if (itemVectorPartition != null)
            {
                foreach (BuildItemGroup items in itemVectorPartition.Values)
                {
                    foreach (BuildItem item in items)
                    {
                        // The FinalItemSpec can be empty-string in the case of an item transform.  See bug
                        // VSWhidbey 523719.
                        if (item.FinalItemSpecEscaped.Length > 0)
                        {
                            itemSpecs.Add(item.FinalItemSpecEscaped);
                        }
                    }
                }
            }

            return itemSpecs;
        }

        /// <summary>
        /// Finds the differences in the keys between the two given hashtables.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="h1"></param>
        /// <param name="h2"></param>
        /// <param name="commonKeys"></param>
        /// <param name="uniqueKeysInH1"></param>
        /// <param name="uniqueKeysInH2"></param>
        private static void DiffHashtables(Hashtable h1, Hashtable h2, out ArrayList commonKeys, out ArrayList uniqueKeysInH1, out ArrayList uniqueKeysInH2)
        {
            commonKeys = new ArrayList();
            uniqueKeysInH1 = new ArrayList();
            uniqueKeysInH2 = new ArrayList();

            foreach (object h1Key in h1.Keys)
            {
                if (h2[h1Key] != null)
                {
                    commonKeys.Add(h1Key);
                }
                else
                {
                    uniqueKeysInH1.Add(h1Key);
                }
            }

            foreach (object h2Key in h2.Keys)
            {
                if (h1[h2Key] == null)
                {
                    uniqueKeysInH2.Add(h2Key);
                }
            }
        }

        /// <summary>
        /// Compares the set of files/directories designated as "inputs" against the set of files/directories designated as
        /// "outputs", and indicates if any "output" file/directory is out-of-date w.r.t. any "input" file/directory.
        /// </summary>
        /// <remarks>
        /// NOTE: Internal for unit test purposes only.
        /// </remarks>
        /// <owner>danmose</owner>
        /// <param name="inputs"></param>
        /// <param name="outputs"></param>
        /// <returns>true, if any "input" is newer than any "output", or if any input or output does not exist.</returns>
        internal static bool IsAnyOutOfDate(out DependencyAnalysisLogDetail dependencyAnalysisDetailEntry, string projectDirectory, IList inputs, IList outputs)
        {
            ErrorUtilities.VerifyThrow((inputs.Count > 0) && (outputs.Count > 0), "Need to specify inputs and outputs.");

             // Algorithm: walk through all the outputs to find the oldest output
            //            walk through the inputs as far as we need to until we find one that's newer (if any)

            // PERF -- we could change this to ensure that we walk the shortest list first (because we walk that one entirely): 
            //         possibly the outputs list isn't actually the shortest list. However it always is the shortest
            //         in the cases I've seen, and adding this optimization would make the code hard to read.

            string oldestOutput = EscapingUtilities.UnescapeAll((string)outputs[0]);

            FileInfo oldestOutputInfo;
            try
            {
                string oldestOutputFullPath = Path.Combine(projectDirectory, oldestOutput);
                oldestOutputInfo = FileUtilities.GetFileInfoNoThrow(oldestOutputFullPath);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                {
                    throw;
                }
                // Output does not exist
                oldestOutputInfo = null;
            }

            if (oldestOutputInfo == null)
            {
                // First output is missing: we must build the target
                string arbitraryInput = EscapingUtilities.UnescapeAll((string)inputs[0]);
                dependencyAnalysisDetailEntry = new DependencyAnalysisLogDetail(arbitraryInput, oldestOutput, null, null, OutofdateReason.MissingOutput);
                return true;
            }

            for (int i = 1; i < outputs.Count; i++)
            {
                string candidateOutput = EscapingUtilities.UnescapeAll((string)outputs[i]);

                FileInfo candidateOutputInfo;
                try
                {
                    string candidateOutputFullPath = Path.Combine(projectDirectory, candidateOutput);
                    candidateOutputInfo = FileUtilities.GetFileInfoNoThrow(candidateOutputFullPath);
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.NotExpectedException(e))
                    {
                        throw;
                    }
                    // Output does not exist
                    candidateOutputInfo = null;
                }

                if (candidateOutputInfo == null)
                {
                    // An output is missing: we must build the target
                    string arbitraryInput = EscapingUtilities.UnescapeAll((string)inputs[0]);
                    dependencyAnalysisDetailEntry = new DependencyAnalysisLogDetail(arbitraryInput, candidateOutput, null, null, OutofdateReason.MissingOutput);
                    return true;
                }

                if (oldestOutputInfo.LastWriteTime > candidateOutputInfo.LastWriteTime)
                {
                    // This output is older than the previous record holder
                    oldestOutputInfo = candidateOutputInfo;
                    oldestOutput = candidateOutput;
                }
            }

            // Now compare the oldest output with each input and break out if we find one newer.
            foreach (string input in inputs)
            {
                string unescapedInput = EscapingUtilities.UnescapeAll(input);

                FileInfo inputInfo = null;
                try
                {
                    string unescapedInputFullPath = Path.Combine(projectDirectory, unescapedInput);
                    inputInfo = FileUtilities.GetFileInfoNoThrow(unescapedInputFullPath);
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.NotExpectedException(e))
                    {
                        throw;
                    }
                    // Output does not exist
                    inputInfo = null;
                }

                if (inputInfo == null)
                {
                    // An input is missing: we must build the target
                    dependencyAnalysisDetailEntry = new DependencyAnalysisLogDetail(unescapedInput, oldestOutput, null, null, OutofdateReason.MissingInput);
                    return true;
                }
                else
                {
                    if (inputInfo.LastWriteTime > oldestOutputInfo.LastWriteTime)
                    {
                        // This input is newer than the oldest output: we must build the target
                        dependencyAnalysisDetailEntry = new DependencyAnalysisLogDetail(unescapedInput, oldestOutput, null, null, OutofdateReason.NewerInput);
                        return true;
                    }
                }
            }

            // All exist and no inputs are newer than any outputs; up to date
            dependencyAnalysisDetailEntry = null;
            return false;
        }

        /// <summary>
        /// Record the unique input and output files so that the "up to date" message
        /// can list them in the log later.
        /// </summary>
        private void RecordUniqueInputsAndOutputs(ArrayList inputs, ArrayList outputs)
        {
            // Only if we are not logging just critical events should we be gathering full details
            if (!loggingService.OnlyLogCriticalEvents)
            {
                foreach (string input in inputs)
                {
                    if (!this.uniqueTargetInputs.ContainsKey(input))
                    {
                        this.uniqueTargetInputs.Add(input, null);
                    }
                }
                foreach (string output in outputs)
                {
                    if (!this.uniqueTargetOutputs.ContainsKey(output))
                    {
                        this.uniqueTargetOutputs.Add(output, null);
                    }
                }
            }
        }

        /// <summary>
        /// Compares the file/directory designated as "input" against the file/directory designated as "output", and indicates if
        /// the "output" file/directory is out-of-date w.r.t. the "input" file/directory.
        /// </summary>
        /// <remarks>
        /// If the "input" does not exist on disk, we treat its disappearance as a change, and consider the "input" to be newer
        /// than the "output", regardless of whether the "output" itself exists.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="inputItemName"></param>
        /// <param name="outputItemName"></param>
        /// <returns>true, if "input" is newer than "output"</returns>
        private bool IsOutOfDate(string input, string output, string inputItemName, string outputItemName)
        {
            bool inputDoesNotExist;
            bool outputDoesNotExist;
            input = EscapingUtilities.UnescapeAll(input);
            output = EscapingUtilities.UnescapeAll(output);
            bool outOfDate = (CompareLastWriteTimes(input, output, out inputDoesNotExist, out outputDoesNotExist) == 1) || inputDoesNotExist;

            // Only if we are not logging just critical events should we be gathering full details
            if (!loggingService.OnlyLogCriticalEvents)
            {
                // Make a not of unique inputs
                if (!this.uniqueTargetInputs.ContainsKey(input))
                {
                    this.uniqueTargetInputs.Add(input, null);
                }

                // Make a note of unique outputs
                if (!this.uniqueTargetOutputs.ContainsKey(output))
                {
                    this.uniqueTargetOutputs.Add(output, null);
                }
            }

            RecordComparisonResults(input, output, inputItemName, outputItemName, inputDoesNotExist, outputDoesNotExist, outOfDate);

            return outOfDate;
        }

        /// <summary>
        /// Add timestamp comparison results to a list, to log them together later.
        /// </summary>
        private void RecordComparisonResults(string input, string output, string inputItemName, string outputItemName, bool inputDoesNotExist, bool outputDoesNotExist, bool outOfDate)
        {
            // Only if we are not logging just critical events should we be gathering full details
            if (!loggingService.OnlyLogCriticalEvents)
            {
                // Record the details of the out-of-date decision
                if (inputDoesNotExist)
                {
                    this.dependencyAnalysisDetail.Add(new DependencyAnalysisLogDetail(input, output, inputItemName, outputItemName, OutofdateReason.MissingInput));
                }
                else if (outputDoesNotExist)
                {
                    this.dependencyAnalysisDetail.Add(new DependencyAnalysisLogDetail(input, output, inputItemName, outputItemName, OutofdateReason.MissingOutput));
                }
                else if (outOfDate)
                {
                    this.dependencyAnalysisDetail.Add(new DependencyAnalysisLogDetail(input, output, inputItemName, outputItemName, OutofdateReason.NewerInput));
                }
            }
        }

        /// <summary>
        /// Compares the last-write times of the given files/directories.
        /// </summary>
        /// <remarks>
        /// Existing files/directories are always considered newer than non-existent ones, and two non-existent files/directories
        /// are considered to have the same last-write time.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <param name="path1DoesNotExist">[out] indicates if the first file/directory does not exist on disk</param>
        /// <param name="path2DoesNotExist">[out] indicates if the second file/directory does not exist on disk</param>
        /// <returns>
        /// -1  if the first file/directory is older than the second;
        ///  0  if the files/directories were both last written to at the same time;
        /// +1  if the first file/directory is newer than the second
        /// </returns>
        private int CompareLastWriteTimes(string path1, string path2, out bool path1DoesNotExist, out bool path2DoesNotExist)
        {
            ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2),
                "Need to specify paths to compare.");

            FileInfo path1Info;
            try
            {
                path1 = Path.Combine(projectDirectory, path1);
                path1Info = FileUtilities.GetFileInfoNoThrow(path1);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                {
                    throw;
                }
                path1Info = null;
            }

            FileInfo path2Info;
            try
            {
                path2 = Path.Combine(projectDirectory, path2);
                path2Info = FileUtilities.GetFileInfoNoThrow(path2);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.NotExpectedException(e))
                {
                    throw;
                }
                path2Info = null;
            }

            path1DoesNotExist = (path1Info == null);
            path2DoesNotExist = (path2Info == null);

            if (path1DoesNotExist)
            {
                if (path2DoesNotExist)
                {
                    // Neither exist
                    return 0;
                }
                else
                {
                    // Only path 2 exists
                    return -1;
                }
            }
            else if (path2DoesNotExist)
            {
                // Only path 1 exists
                return +1;
            }

            // Both exist
            return DateTime.Compare(path1Info.LastWriteTime, path2Info.LastWriteTime);
        }

        #endregion

        // the project directory, all relative paths are 
        // relative to here
        private string projectDirectory;
        // the target to analyze
        private Target targetToAnalyze;

        // the value of the target's "Inputs" attribute
        private string targetInputSpecification;
        // the value of the target's "Outputs" attribute
        private string targetOutputSpecification;
        // The XmlAttribute for the "Inputs"
        private XmlAttribute targetInputsAttribute;
        // The XmlAttribute for the "Outputs"
        private XmlAttribute targetOutputsAttribute;
        // Details of the dependency analysis for logging
        ArrayList dependencyAnalysisDetail = new ArrayList();
        // The unique target inputs
        Hashtable uniqueTargetInputs = new Hashtable(StringComparer.OrdinalIgnoreCase);
        // The unique target outputs;
        Hashtable uniqueTargetOutputs = new Hashtable(StringComparer.OrdinalIgnoreCase);
        // Engine logging service which to log message to
        EngineLoggingServices loggingService;
        // Event context information where event is raised from
        BuildEventContext buildEventContext;
    }

    /// <summary>
    /// Why TLDA decided this entry was out of date
    /// </summary>
    enum OutofdateReason
    {
        MissingInput, // The input file was missing
        MissingOutput, // The output file was missing
        NewerInput // The input file was newer
    }

    /// <summary>
    /// A logging detail entry. Describes what TLDA decided about inputs / outputs
    /// </summary>
    class DependencyAnalysisLogDetail
    {
        private OutofdateReason reason;
        private string inputItemName;
        private string outputItemName;
        private string input;
        private string output;

        /// <summary>
        /// The reason that we are logging this entry
        /// </summary>
        internal OutofdateReason Reason
        {
            get { return reason; }
        }

        /// <summary>
        /// The input item name (can be null)
        /// </summary>
        public string InputItemName
        {
            get { return inputItemName; }
        }

        /// <summary>
        /// The output item name (can be null)
        /// </summary>
        public string OutputItemName
        {
            get { return outputItemName; }
        }

        /// <summary>
        /// The input file
        /// </summary>
        public string Input
        {
            get { return input; }
        }

        /// <summary>
        /// The output file
        /// </summary>
        public string Output
        {
            get { return output; }
        }

        /// <summary>
        /// Construct a log detail element
        /// </summary>
        /// <param name="input">Input file</param>
        /// <param name="output">Output file</param>
        /// <param name="inputItemName">Input item name (can be null)</param>
        /// <param name="outputItemName">Output item name (can be null)</param>
        /// <param name="reason">The reason we are logging</param>
        public DependencyAnalysisLogDetail(string input, string output, string inputItemName, string outputItemName, OutofdateReason reason)
        {
            this.reason = reason;
            this.inputItemName = inputItemName;
            this.outputItemName = outputItemName;
            this.input = input;
            this.output = output;
        }
    }
}
