// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework.Profiler;

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
            hashCode = hashCode * -1521134295 + obj.EvaluationPass.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.EvaluationPassDescription);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.File);
            hashCode = hashCode * -1521134295 + EqualityComparer<int?>.Default.GetHashCode(obj.Line);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.ElementName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.ElementDescription);
            hashCode = hashCode * -1521134295 + obj.Kind.GetHashCode();
            return hashCode;
        }
    }
}
