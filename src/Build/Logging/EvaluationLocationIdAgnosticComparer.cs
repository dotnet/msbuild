// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework.Profiler;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Comparer for <see cref="EvaluationLocation"/> that ignores
    /// both <see cref="EvaluationLocation.Id"/> and <see cref="EvaluationLocation.ParentId"/>
    /// </summary>
    internal class EvaluationLocationIdAgnosticComparer : IEqualityComparer<EvaluationLocation>
    {
        /// <nodoc/>
        public static EvaluationLocationIdAgnosticComparer Singleton = new EvaluationLocationIdAgnosticComparer();

        private EvaluationLocationIdAgnosticComparer()
        { }

        /// <inheritdoc/>
        public bool Equals(EvaluationLocation x, EvaluationLocation y)
        {
            return
                x.EvaluationPass == y.EvaluationPass &&
                x.EvaluationPassDescription == y.EvaluationPassDescription &&
                string.Equals(x.File, y.File, StringComparison.OrdinalIgnoreCase) &&
                x.Line == y.Line &&
                x.ElementName == y.ElementName &&
                x.ElementDescription == y.ElementDescription &&
                x.Kind == y.Kind;
        }

        /// <inheritdoc/>
        public int GetHashCode(EvaluationLocation obj)
        {
            var hashCode = 1198539463;
            hashCode = (hashCode * -1521134295) + EqualityComparer<EvaluationPass>.Default.GetHashCode(obj.EvaluationPass);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.EvaluationPassDescription);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.File);
            hashCode = (hashCode * -1521134295) + EqualityComparer<int?>.Default.GetHashCode(obj.Line);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.ElementName);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.ElementDescription);
            hashCode = (hashCode * -1521134295) + EqualityComparer<EvaluationLocationKind>.Default.GetHashCode(obj.Kind);
            return hashCode;
        }
    }
}
