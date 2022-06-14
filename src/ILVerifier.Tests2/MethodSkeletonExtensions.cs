using System;

namespace ILVerifier.Tests
{
    public static class MethodSkeletonExtensions
    {
        public static TDelegate CreateDelegate<TDelegate>(this IMethodSkeleton methodSkeleton) where TDelegate : Delegate
            => (TDelegate)methodSkeleton.CreateDelegate(typeof(TDelegate));
    }
}