using MyApp.Test;

namespace MyApp
{
#if defaultTrue
    public class DefaultTrueIncluded { }
#else
    public class DefaultTrueExcluded { }
#endif

//-:cnd:noEmit
#if defaultTrue
    public class InsideUnknownDirectiveNoEmit { }
#endif
//+:cnd:noEmit

#if (defaultFalse)
    public class DefaultFalseExcluded { }
#else
    public class DefaultFalseIncluded { }
#endif

// Without noEmit the following line will be emitted
//-:cnd
#if defaultFalse
    public class InsideUnknownDirectiveEmit { }
#endif
//+:cnd
}