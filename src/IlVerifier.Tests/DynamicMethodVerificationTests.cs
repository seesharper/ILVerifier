using System;

namespace ILVerifier.Tests;

public class DynamicMethodVerificationTests : DynamicMethodTests
{
    protected override IMethodSkeleton GetMethodSkeleton(string name, Type returnType, Type[] parameterTypes)
    {
        return new VerifiableMethodSkeleton(name, returnType, parameterTypes);
    }
}