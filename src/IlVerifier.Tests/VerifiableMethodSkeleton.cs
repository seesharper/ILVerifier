using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ILVerifier.Tests;

public class VerifiableMethodSkeleton : IMethodSkeleton
{
    private readonly string name;

    private TypeBuilder typeBuilder;
    private MethodBuilder methodBuilder;


    public VerifiableMethodSkeleton(string name, Type returnType, Type[] parameterTypes)
    {
        this.name = name;
        var assemblyBuilder = CreateAssemblyBuilder();
        CreateTypeBuilder(assemblyBuilder);
        CreateMethodBuilder(returnType, parameterTypes);
    }

    private AssemblyBuilder CreateAssemblyBuilder()
        => AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicMethodAssembly"), AssemblyBuilderAccess.Run);

    private void CreateTypeBuilder(AssemblyBuilder assemblyBuilder)
        => typeBuilder = assemblyBuilder.DefineDynamicModule("DynamicMethodModule").DefineType(name, TypeAttributes.Public);

    private void CreateMethodBuilder(Type returnType, Type[] parameterTypes)
    {
        methodBuilder = typeBuilder.DefineMethod(
            "DynamicMethod", MethodAttributes.Public | MethodAttributes.Static, returnType, parameterTypes);
        methodBuilder.InitLocals = true;
    }

    public ILGenerator GetILGenerator() => methodBuilder.GetILGenerator();

    public Delegate CreateDelegate(Type delegateType)
    {
        var dynamicType = typeBuilder.CreateType();
        new Verifier().Verify(dynamicType.Assembly);
        MethodInfo methodInfo = dynamicType.GetMethod("DynamicMethod", BindingFlags.Static | BindingFlags.Public);
        return Delegate.CreateDelegate(delegateType, methodInfo);
    }
}