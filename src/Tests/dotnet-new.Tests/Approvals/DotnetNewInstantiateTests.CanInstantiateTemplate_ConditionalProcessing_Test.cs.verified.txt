using MyApp.Test;

namespace MyApp
{
    public class DefaultTrueIncluded { }

#if defaultTrue
    public class InsideUnknownDirectiveNoEmit { }
#endif

    public class DefaultFalseIncluded { }

// Without noEmit the following line will be emitted
//-:cnd
#if defaultFalse
    public class InsideUnknownDirectiveEmit { }
#endif
//+:cnd
}