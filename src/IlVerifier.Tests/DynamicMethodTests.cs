using System;
using System.Reflection.Emit;
using FluentAssertions;

namespace ILVerifier.Tests;

public class DynamicMethodTests
{
    private IMethodSkeleton GetMethodSkeleton(string name, Type returnType, Type[] parameterTypes)
    {
        return new DynamicMethodSkeleton(name, returnType, parameterTypes, typeof(DynamicMethodTests));
    }

    [Fact]
    public void ShouldAddNumbers()
    {
        var methodSkeleton = GetMethodSkeleton("Add", typeof(int), new[] { typeof(int), typeof(int) });
        var generator = methodSkeleton.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);

        var addDelegate = methodSkeleton.CreateDelegate<Func<int, int, int>>();
        // var result = addDelegate(8, 7);
        // result.Should().Be(15);
    }
}