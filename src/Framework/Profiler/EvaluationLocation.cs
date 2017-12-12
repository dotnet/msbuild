// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Location for different elements tracked by the evaluation profiler.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework.Profiler
{
    /// <summary>
    /// Evaluation main phases used by the profiler
    /// </summary>
    /// <remarks>
    /// Order matters since the profiler pretty printer orders profiled items from top to bottom using 
    /// the pass they belong to
    /// </remarks>
    public enum EvaluationPass : byte
    {
        /// <nodoc/>
        TotalEvaluation = 0,
        /// <nodoc/>
        TotalGlobbing = 1,
        /// <nodoc/>
        InitialProperties = 2,
        /// <nodoc/>
        Properties = 3,
        /// <nodoc/>
        ItemDefinitionGroups = 4,
        /// <nodoc/>
        Items = 5,
        /// <nodoc/>
        LazyItems = 6,
        /// <nodoc/>
        UsingTasks = 7,
        /// <nodoc/>
        Targets = 8
    }

    /// <summary>
    /// The kind of the evaluated location being tracked
    /// </summary>
    public enum EvaluationLocationKind : byte
    {
        /// <nodoc/>
        Item = 0,
        /// <nodoc/>
        Condition = 1,
        /// <nodoc/>
        Glob = 2
    }

    /// <summary>
    /// Represents a location for different evaluation elements tracked by the EvaluationProfiler.
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public struct EvaluationLocation
    {
        /// <summary>
        /// Default descriptions for locations that are used in case a description is not provided
        /// </summary>
        private static readonly Dictionary<EvaluationPass, string> PassDefaultDescription =
            new Dictionary<EvaluationPass, string>
            {
                {EvaluationPass.TotalEvaluation, "Total evaluation"},
                {EvaluationPass.TotalGlobbing, "Total evaluation for globbing"},
                {EvaluationPass.InitialProperties, "Initial properties (pass 0)"},
                {EvaluationPass.Properties, "Properties (pass 1)"},
                {EvaluationPass.ItemDefinitionGroups, "Item definition groups (pass 2)"},
                {EvaluationPass.Items, "Items (pass 3)"},
                {EvaluationPass.LazyItems, "Lazy items (pass 3.1)"},
                {EvaluationPass.UsingTasks, "Using tasks (pass 4)"},
                {EvaluationPass.Targets, "Targets (pass 5)"},
            };

        /// <nodoc/>
        public EvaluationPass EvaluationPass { get; }

        /// <nodoc/>
        public string EvaluationDescription { get; }

        /// <nodoc/>
        public string File { get; }

        /// <nodoc/>
        public int? Line { get; }

        /// <nodoc/>
        public string ElementName { get; }

        /// <nodoc/>
        public string Description { get; }

        /// <nodoc/>
        public EvaluationLocationKind Kind { get; }

        /// <nodoc/>
        public static EvaluationLocation CreateLocationForCondition(EvaluationPass evaluationPass, string evaluationDescription, string file,
            int? line, string condition)
        {
            return new EvaluationLocation(evaluationPass, evaluationDescription, file, line, "Condition", condition, kind: EvaluationLocationKind.Condition);
        }

        /// <nodoc/>
        public static EvaluationLocation CreateLocationForProject(EvaluationPass evaluationPass, string evaluationDescription, string file,
            int? line, IProjectElement element)
        {
            return new EvaluationLocation(evaluationPass, evaluationDescription, file, line, element?.ElementName,
                element?.OuterElement, kind: EvaluationLocationKind.Item);
        }

        /// <nodoc/>
        public static EvaluationLocation CreateLocationForGlob(EvaluationPass evaluationPass,
            string evaluationDescription, string file, int? line, string globDescription)
        {
            return new EvaluationLocation(evaluationPass, evaluationDescription, file, line, "Glob", globDescription, kind: EvaluationLocationKind.Glob);
        }

        /// <nodoc/>
        public static EvaluationLocation CreateLocationForAggregatedGlob()
        {
            return new EvaluationLocation(EvaluationPass.TotalGlobbing,
                PassDefaultDescription[EvaluationPass.TotalGlobbing], file: null, kind: EvaluationLocationKind.Glob,
                line: null, elementName: null, description: null);
        }

        /// <summary>
        /// Constructs the generic case.
        /// </summary>
        /// <remarks>
        /// Used by serialization/deserialization purposes
        /// </remarks>
        public EvaluationLocation(EvaluationPass evaluationPass, string evaluationDescription, string file, int? line, string elementName, string description, EvaluationLocationKind kind)
        {
            EvaluationPass = evaluationPass;
            EvaluationDescription = evaluationDescription;
            File = file;
            Line = line;
            ElementName = elementName;
            Description = description;
            Kind = kind;
        }

        private static readonly EvaluationLocation Empty = new EvaluationLocation();

        /// <summary>
        /// An empty location, used as the starting instance.
        /// </summary>
        public static EvaluationLocation EmptyLocation { get; } = Empty;
        
        /// <nodoc/>
        public EvaluationLocation WithEvaluationPass(EvaluationPass evaluationPass, string passDescription = null)
        {
            return new EvaluationLocation(evaluationPass, passDescription ?? PassDefaultDescription[evaluationPass],
                this.File, this.Line, this.ElementName, this.Description, this.Kind);
        }

        /// <nodoc/>
        public EvaluationLocation WithFile(string file)
        {
            return new EvaluationLocation(this.EvaluationPass, this.EvaluationDescription, file, null, null, null, this.Kind);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndElement(string file, int? line, IProjectElement element)
        {
            return CreateLocationForProject(this.EvaluationPass, this.EvaluationDescription, file, line, element);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndCondition(string file, int? line, string condition)
        {
            return CreateLocationForCondition(this.EvaluationPass, this.EvaluationDescription, file, line, condition);
        }

        /// <nodoc/>
        public EvaluationLocation WithGlob(string globDescription)
        {
            return CreateLocationForGlob(this.EvaluationPass, this.EvaluationDescription, this.File, this.Line, globDescription);
        }

        /// <nodoc/>
        public override bool Equals(object obj)
        {
            if (obj is EvaluationLocation)
            {
                var other = (EvaluationLocation) obj;
                return
                    EvaluationPass == other.EvaluationPass &&
                    EvaluationDescription == other.EvaluationDescription &&
                    string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase) &&
                    Line == other.Line &&
                    ElementName == other.ElementName &&
                    Description == other.Description &&
					Kind == other.Kind;
            }
            return false;
        }

        /// <nodoc/>
        public override string ToString()
        {
            return $"{EvaluationDescription ?? string.Empty}\t{File ?? string.Empty}\t{Line?.ToString() ?? string.Empty}\t{ElementName ?? string.Empty}\tDescription:{Description}\t{this.EvaluationDescription}";
        }

        /// <nodoc/>
        public override int GetHashCode()
        {
            var hashCode = 1198539463;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EvaluationPass.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EvaluationDescription);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(File);
            hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(Line);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Description);
            hashCode = hashCode * -1521134295 + Kind.GetHashCode();
            return hashCode;
        }
    }
}
