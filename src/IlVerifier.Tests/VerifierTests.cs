namespace ILVerifier.Tests;

using System.Reflection.Emit;
using ILVerifer;
public class UnitTest1
{
    [Fact]
    public void ShouldVerifyAssembly()
    {
        var verifier = new Verifier()
        .Configure(options => options.ReferencedAssemblies.Add(typeof(Verifier).Assembly));
        verifier.Verify(typeof(UnitTest1).Assembly);
    }

    [Fact]
    public void ShouldVerifyDynamicMethod()
    {
        var method = new DynamicMethod("Add",typeof(int), new Type[]{typeof(int), typeof(int)});
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);

        var del  = method.CreateDelegate<Func<int,int,int>>();
        
        var test = del.GetType().Assembly;
        //var result = del(2,2);

         var verifier = new Verifier()
        .Configure(options => options.ReferencedAssemblies.Add(typeof(Verifier).Assembly));
        verifier.Verify(method.Module.Assembly);
    }
}