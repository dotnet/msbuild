using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public interface ITransform<T, U>
    {
        U Transform(T source);
    }
}