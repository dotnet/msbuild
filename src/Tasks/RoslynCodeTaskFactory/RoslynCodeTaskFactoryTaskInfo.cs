// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Tasks
{
    internal sealed class RoslynCodeTaskFactoryTaskInfo : IEquatable<RoslynCodeTaskFactoryTaskInfo>
    {
        /// <summary>
        /// Gets or sets the code language of the task.
        /// </summary>
        public string CodeLanguage { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="RoslynCodeTaskFactoryCodeType"/> of the task.
        /// </summary>
        public RoslynCodeTaskFactoryCodeType CodeType { get; set; }

        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets a <see cref="ISet{String}"/> of namespaces to use.
        /// </summary>
        public ISet<string> Namespaces { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets an <see cref="ISet{String}"/> of assembly references.
        /// </summary>
        public ISet<string> References { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the source code of the assembly.
        /// </summary>
        public string SourceCode { get; set; }

        /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
        public bool Equals(RoslynCodeTaskFactoryTaskInfo other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return String.Equals(SourceCode, other.SourceCode, StringComparison.OrdinalIgnoreCase) && References.SetEquals(other.References);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as RoslynCodeTaskFactoryTaskInfo);
        }

        public override int GetHashCode()
        {
            // This is good enough to avoid most collisions, no need to hash References
            return SourceCode.GetHashCode();
        }
    }
}
