#if (ImplicitUsings != "enable")
using System;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.ClassLibrary1;

public interface Interface1
{

}
#else
namespace Company.ClassLibrary1
{
    public interface Interface1
    {

    }
}
#endif
