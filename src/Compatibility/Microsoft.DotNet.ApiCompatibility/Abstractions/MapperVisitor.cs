namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class MapperVisitor
    {
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

        public virtual void Visit(AssemblySetMapper mapper)
        {
            foreach (AssemblyMapper assembly in mapper.GetAssemblies())
            {
                Visit(assembly);
            }
        }

        public virtual void Visit(AssemblyMapper mapper)
        {
            foreach (NamespaceMapper nsMapper in mapper.GetNamespaces())
            {
                Visit(nsMapper);
            }
        }

        public virtual void Visit(NamespaceMapper mapper)
        {
            foreach (TypeMapper type in mapper.GetTypes())
            {
                Visit(type);
            }
        }

        public virtual void Visit(TypeMapper mapper)
        {
            foreach (TypeMapper type in mapper.GetNestedTypes())
            {
                Visit(type);
            }

            foreach (MemberMapper member in mapper.GetMembers())
            {
                Visit(member);
            }
        }

        public virtual void Visit(MemberMapper mapper) { }
    }
}
