# ILVerifier

## Introduction

This library is aimed at developers that uses [System.Reflection.Emit](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit) to either modify existing assemblies or dynamically generate code at runtime. 

## Verifying IL
For years, the only tool available for verifying assemblies was a tool called [Peverify](https://docs.microsoft.com/en-us/dotnet/framework/tools/peverify-exe-peverify-tool). This is a command line tool that is only available for .Net Framework and Mono. [ILVerify](https://github.com/dotnet/runtime/blob/main/src/coreclr/tools/ILVerify/README.md) is a new tool from Microsoft that is meant to be a replacement for [Peverify](https://docs.microsoft.com/en-us/dotnet/framework/tools/peverify-exe-peverify-tool). It is currently only available as a global tool [package](https://www.nuget.org/packages/dotnet-ilverify) and this means that to use this tool in unit tests, we need to run it with a set of arguments in its own process. Most importantly , we need an assembly written to disk before we can verify it. 


## Usage 

Install from [NuGet](https://www.nuget.org/packages/ILVerifier/)

`ILVerifier` can handle both existing assemblies already on disk or assemblies that are dynamically created using [AssemblyBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder).

Example with an existing assembly 

```csharp
 new Verifier().Verify(typeof(Foo).Assembly.Location);                
```

Example with just an assembly reference 

```csharp
 new Verifier().Verify(typeof(Foo).Assembly);                
```

### Assembly references

If the assembly to be verified references other assemblies these also need to be specified using the `WithAssemblyReference` method.
Say that we have a `FooAssembly` that references `BarAssembly`
Typically we will get an error saying something like 

```
Assembly or module not found: BarAssembly
```

> Note: `MyAssembly` is just an example of a missing assembly reference

Solve this by adding a reference like this

```csharp
 new Verifier()     
    .WithAssemblyReference(typeof(Bar).Assembly)
    .Verify(typeof(Foo).Assembly);
```

Alteratively we can specify the assembly reference through type contained in the referenced assembly.

```csharp
new Verifier()     
    .WithAssemblyReferenceFromType<Bar>()
    .Verify(typeof(Foo).Assembly);
```

## Saving assemblies to disk

This is the next problem we need to deal with since .Net Core does not really allow dynamically created assemblies to be saved to disk. We used to have [AssemblyBuilderAccess.RunAndSave](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilderaccess?view=netframework-4.8), but that is not available on .Net Core. The issue being tracked [here](https://github.com/dotnet/runtime/issues/15704), but it remains open since it was create in 2015.

Luckily for us, there is a excellent library called [Lokad.ILPack](https://github.com/Lokad/ILPack) that fills the gap of being able to save an assembly to disk

## DynamicMethod

Creating new assemblies using AssemblyBuilder and friends is one thing since then we actually have an assembly to be saved to disk using [Lokad.ILPack](https://github.com/Lokad/ILPack). Another thing is if we create new code using [DynamicMethod](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.dynamicmethod). Then it is not so trivial since the assembly hosting the dynamic method is not really accessible to us and thus it cannot be saved to disk. 

## IMethodSkeleton

One solution to verifying dynamic methods is to create an abstraction over the method itself. Note that the following interfaces and classes are not part of `ILVerifier`. They exists only here in the test project of this repo. Feel free to reuse this approach. 

```csharp

/// <summary>
/// Represents the skeleton of a dynamic method.
/// </summary>
public interface IMethodSkeleton
{
    /// <summary>
    /// Gets the <see cref="ILGenerator"/> for this method.
    /// </summary>
    /// <returns><see cref="ILGenerator"/>.</returns>
    ILGenerator GetILGenerator();
    
    /// <summary>
    /// Completes the dynamic method and creates a delegate that can be used to execute it.
    /// </summary>
    /// <param name="delegateType">A delegate type whose signature matches that of the dynamic method.</param>
    /// <returns>A delegate of the specified type, which can be used to execute the dynamic method.</returns>
    Delegate CreateDelegate(Type delegateType);     
}
```

The idea here is that we don't work directly with `DynamicMethod` anymore giving is the chance to swap the default implementation for another during testing. But first here is the `DynamicMethodSkeleton` which just wraps `DynamicMethod`.

```csharp
/// <summary>
/// A <see cref="IMethodSkeleton"/> that uses the <see cref="DynamicMethod"/> class.
/// </summary>
public class DynamicMethodSkeleton : IMethodSkeleton
{
    private readonly DynamicMethod dynamicMethod;
   
    public DynamicMethodSkeleton(string name, Type returnType, Type[] parameterTypes, Module module)
    {
            dynamicMethod = new DynamicMethod(
                    name,
                    returnType,
                    parameterTypes,
                    module,
                    true);
    }
   
    public DynamicMethodSkeleton(string name, Type returnType, Type[] parameterTypes, Type owner)
    {
        dynamicMethod = new DynamicMethod(
                    name,
                    returnType,
                    parameterTypes,
                    owner,
                    true);

    }
  
    public ILGenerator GetILGenerator() => dynamicMethod.GetILGenerator();
  
    public Delegate CreateDelegate(Type delegateType) => dynamicMethod.CreateDelegate(delegateType);    
}
```

Now for the interesting part. How do we verify the IL for our dynamic method?. Actually this is now quite easy. 
We just have to swap the default `IMethodSkeleton` implementation for a one that emits code into a method using [MethodBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.methodbuilder).

```csharp
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
            name, MethodAttributes.Public | MethodAttributes.Static, returnType, parameterTypes);
        methodBuilder.InitLocals = true;
    }

    public ILGenerator GetILGenerator() => methodBuilder.GetILGenerator();

    public Delegate CreateDelegate(Type delegateType)
    {
        var dynamicType = typeBuilder.CreateType();
        new Verifier().Verify(dynamicType.Assembly);
        MethodInfo methodInfo = dynamicType.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        return Delegate.CreateDelegate(delegateType, methodInfo);
    }
}
```

Notice that just before we create the delegate we add in a call to `ILVerifier`

```csharp
public Delegate CreateDelegate(Type delegateType)
{
    var dynamicType = typeBuilder.CreateType();
    new Verifier().Verify(dynamicType.Assembly);
    MethodInfo methodInfo = dynamicType.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
    return Delegate.CreateDelegate(delegateType, methodInfo);
}
```

## Putting it together

Let's create a test class containing one method `ShouldAddNumbers` that uses a dynamic method.

```csharp
public class DynamicMethodTests
{
    protected virtual IMethodSkeleton GetMethodSkeleton(string name, Type returnType, Type[] parameterTypes)
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
        //generator.Emit(OpCodes.Ret);

        var addDelegate = methodSkeleton.CreateDelegate<Func<int, int, int>>();
        var result = addDelegate(8, 7);
        result.Should().Be(15);
    }
}
```

While this works and the test is green, we also need to verify that the IL is correct. This is important since we don't want the user to end up with a `Common Language Runtime detected an invalid program.`.
Also note that the runtime does not catch all mistakes we have made with our IL and worst case scenario here is that we run with invalid code.  
So to verify this code we simply create another test class inheriting from `DynamicMethodTests`.

```csharp
public class DynamicMethodVerificationTests : DynamicMethodTests
{
    protected override IMethodSkeleton GetMethodSkeleton(string name, Type returnType, Type[] parameterTypes)
    {
        return new VerifiableMethodSkeleton(name, returnType, parameterTypes);
    }
}
```

The only thing going on here is that we swap the `IMethodSkeleton` implementation with `VerifiableMethodSkeleton` and 
the end result here is that we have 2 tests. One that uses `DynamicMethod` under the hood and one that uses `MethodBuilder`.


## Make a mistake

Although our `Add` method is a trivial example, it is easy to forget something. Let's NOT return from the generated method. 

```csharp
generator.Emit(OpCodes.Ldarg_0);
generator.Emit(OpCodes.Ldarg_1);
generator.Emit(OpCodes.Add);
//generator.Emit(OpCodes.Ret);
```

Running the test that uses `DynamicMethodSkeleton` simply outputs 

```
System.InvalidProgramException : Common Language Runtime detected an invalid program.
```

There is no information about what is wrong and how to fix it. 

Now run the test that uses `VerifiableMethodSkeleton`

The output then becomes 

```
System.InvalidProgramException : [IL]: Error [MethodFallthrough]: [/Users/bernhardrichter/GitHub/ILVerifier/src/ILVerifier.Tests/bin/Debug/net6.0/VerifiedAssembly.dll : .Add::Add(int32, int32)][offset 0x00000002] Fall through end of the method without returning.
```

Now, that is useful information and we could in most cases understand what causes the error.


## Summary

Once more, this library is just standing on the shoulders of two other giants. It would not have been possible without [Lokad.ILPack](https://github.com/Lokad/ILPack) for saving assemblies to disk or without [ILVerify](https://github.com/dotnet/runtime/blob/main/src/coreclr/tools/ILVerify/README.md) for actually verifying assemblies. It would simply be impossible for someone to create a library like this from scratch. 
Then again, I hope that this library can make it a little easier to get started with IL verification for .Net developers and that the example of abstracting the building of dynamic methods into `IMethodSkeleton` made some sense. 
