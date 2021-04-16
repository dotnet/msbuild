// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Base class for Rules to use in order to be discovered and invoked by the <see cref="IRuleRunner"/>
    /// </summary>
    public abstract class Rule
    {
        /// <summary>
        /// Entrypoint to evaluate an <see cref="AssemblyMapper"/>
        /// </summary>
        /// <param name="mapper">The <see cref="AssemblyMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add any differences to.</param>
        public virtual void Run(AssemblyMapper mapper, IList<CompatDifference> differences)
        {
        }

        /// <summary>
        /// Entrypoint to evaluate an <see cref="TypeMapper"/>
        /// </summary>
        /// <param name="mapper">The <see cref="TypeMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add any differences to.</param>
        public virtual void Run(TypeMapper mapper, IList<CompatDifference> differences)
        {
        }

        /// <summary>
        /// Entrypoint to evaluate an <see cref="MemberMapper"/>
        /// </summary>
        /// <param name="mapper">The <see cref="MemberMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add any differences to.</param>
        public virtual void Run(MemberMapper mapper, IList<CompatDifference> differences)
        {
        }
    }
}
