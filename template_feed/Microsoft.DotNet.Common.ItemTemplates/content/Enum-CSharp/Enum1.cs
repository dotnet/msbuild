#if (csharpFeature_FileScopedNamespaces)
namespace Company.ClassLibrary1;

public enum Enum1
{

}
#else
namespace Company.ClassLibrary1
{
    public enum Enum1
    {

    }
}
#endif
