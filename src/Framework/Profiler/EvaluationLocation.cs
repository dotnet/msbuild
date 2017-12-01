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
        TotalEvaluation,
        /// <nodoc/>
        InitialProperties,
        /// <nodoc/>
        Properties,
        /// <nodoc/>
        ItemDefintionGroups,
        /// <nodoc/>
        Items,
        /// <nodoc/>
        LazyItems,
        /// <nodoc/>
        UsingTasks,
        /// <nodoc/>
        Targets
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
                {EvaluationPass.TotalEvaluation, "Total Evaluation"},
                {EvaluationPass.InitialProperties, "Initial properties (pass 0)"},
                {EvaluationPass.Properties, "Properties (pass 1)"},
                {EvaluationPass.ItemDefintionGroups, "Item definition groups (pass 2)"},
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
        public string ElementOrCondition { get; }

        /// <summary>
        /// True when <see cref="ElementOrCondition"/> is an element
        /// </summary>
        public bool IsElement { get; }

        /// <summary>
        /// True when <see cref="ElementOrCondition"/> is a condition
        /// </summary>
        public bool IsCondition => !IsElement;

        /// <summary>
        /// Constructs the condition case
        /// </summary>
        public EvaluationLocation(EvaluationPass evaluationPass, string evaluationDescription, string file, int? line, string condition)
            : this(evaluationPass, evaluationDescription, file, line, "Condition", condition, isElement: false)
        {}

        /// <summary>
        /// Constructs the project element case
        /// </summary>
        public EvaluationLocation(EvaluationPass evaluationPass, string evaluationDescription, string file, int? line, IProjectElement element)
            : this(evaluationPass, evaluationDescription, file, line, element?.ElementName, element?.OuterXmlElement, isElement: true)
        {}

        /// <summary>
        /// Constructs the generic case.
        /// </summary>
        /// <remarks>
        /// Used by serialization/deserialization purposes
        /// </remarks>
        public EvaluationLocation(EvaluationPass evaluationPass, string evaluationDescription, string file, int? line, string elementName, string elementOrCondition, bool isElement)
        {
            EvaluationPass = evaluationPass;
            EvaluationDescription = evaluationDescription;
            File = file;
            Line = line;
            ElementName = elementName;
            ElementOrCondition = elementOrCondition;
            IsElement = isElement;
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
                this.File, this.Line, this.ElementName, this.ElementOrCondition, this.IsElement);
        }

        /// <nodoc/>
        public EvaluationLocation WithFile(string file)
        {
            return new EvaluationLocation(this.EvaluationPass, this.EvaluationDescription, file, null, null, null, this.IsElement);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndElement(string file, int? line, IProjectElement element)
        {
            return new EvaluationLocation(this.EvaluationPass, this.EvaluationDescription, file, line, element);
        }

        /// <nodoc/>
        public EvaluationLocation WithFileLineAndCondition(string file, int? line, string condition)
        {
            return new EvaluationLocation(this.EvaluationPass, this.EvaluationDescription, file, line, condition);
        }

        /// <nodoc/>
        public override bool Equals(object obj)
        {
            if (obj is EvaluationLocation other)
            {
                return
                    EvaluationPass == other.EvaluationPass &&
                    EvaluationDescription == other.EvaluationDescription &&
                    string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase) &&
                    Line == other.Line &&
                    ElementName == other.ElementName;
            }
            return false;
        }

        /// <nodoc/>
        public override int GetHashCode()
        {
            var hashCode = 590978104;
            hashCode = hashCode * -1521134295 + base.GetHashCode();

            hashCode = hashCode * -1521134295 + EqualityComparer<EvaluationPass>.Default.GetHashCode(EvaluationPass);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EvaluationDescription);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(File?.ToLower());
            hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(Line);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ElementOrCondition);

            return hashCode;
        }

        /// <nodoc/>
        public override string ToString()
        {
            return $"{EvaluationDescription ?? string.Empty}\t{File ?? string.Empty}\t{Line?.ToString() ?? string.Empty}\t{ElementName ?? string.Empty}";
        }
    }
}
