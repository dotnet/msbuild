// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Class that implements a visitor pattern to visit the tree for a given mapper.
    /// </summary>
    public class MapperVisitor
    {
        /// <summary>
        /// Visits the tree for the given <see cref="ElementMapper{T}"/>.
        /// </summary>
        /// <typeparam name="T">Underlying type for the objects that the mapper holds.</typeparam>
        /// <param name="mapper"><see cref="ElementMapper{T}"/> to visit.</param>
        public void Visit<T>(ElementMapper<T> mapper)
        {
            if (mapper is AssemblySetMapper assemblySetMapper)
            {
                Visit(assemblySetMapper);
            }
            else if (mapper is AssemblyMapper assemblyMapper)
            {
                Visit(assemblyMapper);
            }
            else if (mapper is NamespaceMapper nsMapper)
            {
                Visit(nsMapper);
            }
            else if (mapper is TypeMapper typeMapper)
            {
                Visit(typeMapper);
            }
            else if (mapper is MemberMapper memberMapper)
            {
                Visit(memberMapper);
            }
        }

        /// <summary>
        /// Visits the <see cref="AssemblySetMapper"/> and visits each <see cref="AssemblyMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="AssemblySetMapper"/> to visit.</param>
        public virtual void Visit(AssemblySetMapper mapper)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            foreach (AssemblyMapper assembly in mapper.GetAssemblies())
            {
                Visit(assembly);
            }
        }

        /// <summary>
        /// Visits the <see cref="AssemblyMapper"/> and visits each <see cref="NamespaceMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="AssemblyMapper"/> to visit.</param>
        public virtual void Visit(AssemblyMapper mapper)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            foreach (NamespaceMapper nsMapper in mapper.GetNamespaces())
            {
                Visit(nsMapper);
            }
        }

        /// <summary>
        /// Visits the <see cref="NamespaceMapper"/> and visits each <see cref="TypeMapper"/> in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="NamespaceMapper"/> to visit.</param>
        public virtual void Visit(NamespaceMapper mapper)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            foreach (TypeMapper type in mapper.GetTypes())
            {
                Visit(type);
            }
        }

        /// <summary>
        /// Visits the <see cref="TypeMapper"/> and visits the nested types and members in the mapper.
        /// </summary>
        /// <param name="mapper">The <see cref="TypeMapper"/> to visit.</param>
        public virtual void Visit(TypeMapper mapper)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            foreach (TypeMapper type in mapper.GetNestedTypes())
            {
                Visit(type);
            }

            foreach (MemberMapper member in mapper.GetMembers())
            {
                Visit(member);
            }
        }

        /// <summary>
        /// Visits the <see cref="MemberMapper"/>.
        /// </summary>
        /// <param name="mapper">The <see cref="MemberMapper"/> to visit.</param>
        public virtual void Visit(MemberMapper mapper) { }
    }
}
