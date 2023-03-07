#if (csharpFeature_RecordClass)
namespace Company.ClassLibrary1;

public record class Record1
{

}
#elseif (csharpFeature_Record)
namespace Company.ClassLibrary1
{
    public record Record1
    {

    }
}
#else
namespace Company.ClassLibrary1
{
    //Record was added in C# 9 and later, so Class was used instead. 
    //See more info: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record
    public class Record1
    {

    }
}
#endif
