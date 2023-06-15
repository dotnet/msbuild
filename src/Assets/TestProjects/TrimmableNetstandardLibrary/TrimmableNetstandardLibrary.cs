using System.Diagnostics.CodeAnalysis;

class TrimmableNetStandardLibrary
{
    public static void EntryPoint()
    {
        RequiresUnreferencedCode();
    }


    [RequiresUnreferencedCode("Requires unreferenced code")]
    static void RequiresUnreferencedCode()
    {
    }    
}
