using MyApp.Test;

//Things
namespace MyApp
{
#if defaultTrue
    public class DefaultTrueIncluded { }
#else
    public class DefaultTrueExcluded { }
#endif

#if defaultFalse
    public class DefaultFalseExcluded { }
#else
    public class DefaultFalseIncluded { }
#endif

//-:cnd:noEmit
#if DEBUG1
    public class InsideUnknownDirectiveNoEmit { }
#endif
//+:cnd:noEmit

//-:cnd
#if DEBUG2
    public class InsideUnknownDirectiveEmit { }
#endif
//+:cnd
}