#if (ImplicitUsings != "enable")
using System;

#endif
#if (csharpFeature_FileScopedNamespaces)
namespace Company.ClassLibrary1;

public struct Struct1
{

}
#else
namespace Company.ClassLibrary1
{
    public struct Struct1
    {

    }
}
#endif
