open System

#if defaultTrue
type DefaultTrueIncluded() = do printfn ""
#else
type DefaultTrueExcluded() = do printfn ""
#endif

//-:cnd:noEmit
#if defaultTrue
type InsideUnknownDirectiveNoEmit() = do printfn ""
#endif
//+:cnd:noEmit

#if (defaultFalse)
type DefaultFalseExcluded() = do printfn ""
#else
type DefaultFalseIncluded() = do printfn ""
#endif

// Without noEmit the following line will be emitted
//-:cnd
#if defaultFalse
type InsideUnknownDirectiveEmit() = do printfn ""
#endif
//+:cnd