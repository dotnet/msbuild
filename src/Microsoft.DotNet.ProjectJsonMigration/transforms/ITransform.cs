using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public interface ITransform<T, U>
    {
        U Transform(T source);
    }
}