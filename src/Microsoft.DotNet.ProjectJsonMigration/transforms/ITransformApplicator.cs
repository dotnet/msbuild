using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public interface ITransformApplicator
    {
        void Execute<T, U>(
            T element,
            U destinationElement) where T : ProjectElement where U : ProjectElementContainer;

        void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement) where T : ProjectElement where U : ProjectElementContainer;

        void Execute(
            ProjectItemElement item,
            ProjectItemGroupElement destinationItemGroup,
            bool mergeExisting);

        void Execute(
            IEnumerable<ProjectItemElement> items,
            ProjectItemGroupElement destinationItemGroup,
            bool mergeExisting);
    }
}