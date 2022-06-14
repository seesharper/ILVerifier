namespace ILVerifier.Tests;

using System;
using System.IO;
using System.Reflection.Emit;
using System.Text;
using FluentAssertions;

[Collection("VerificationTests")]
public class VerifierTests
{
    [Fact]
    public void ShouldVerifyAssembly()
    {
        // Note that this failes if saved by ILPack if we use CreateDelagate extension method Try to create an issue for this
        /*
        IL]: Error [StackUnexpected]: [/home/runner/work/ILVerifier/ILVerifier/src/IlVerifier.Tests/bin/Release/net6.0/VerifiedAssembly.dll : ILVerifier.Tests.DynamicMethodTests::ShouldAddNumbers()][offset 0x0000006F][found ref '!!0'][expected ref '[S.P.CoreLib]System.Func`3<int32,int32,int32>'] Unexpected type on the stack.
        */
        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .Verify(typeof(VerifierTests).Assembly);
    }

    [Fact]
    public void ShouldVerifyAssemblyUsingAssemblyFile()
    {
        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .Verify(typeof(VerifierTests).Assembly.Location);
    }

    [Fact]
    public void ShouldVerifyAssemblyWithFullyQualifiedVerifiedAssemblyPath()
    {
        string customVerifiedAssemblyPath = Path.Combine(Path.GetDirectoryName(Path.Combine(typeof(VerifierTests).Assembly.Location)), "CustomAssembly.dll");

        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithVerifiedAssemblyPath(customVerifiedAssemblyPath)
            .Verify(typeof(VerifierTests).Assembly);

        File.Exists(customVerifiedAssemblyPath).Should().BeTrue();
    }

    [Fact]
    public void ShouldVerifyAssemblyWithVerifiedAssemblyPath()
    {
        string expectedPath = Path.Combine(Path.GetDirectoryName(Path.Combine(typeof(VerifierTests).Assembly.Location)), "CustomAssembly.dll");

        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithVerifiedAssemblyPath("CustomAssembly.dll")
            .Verify(typeof(VerifierTests).Assembly);

        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public void ShouldOutputNormalVerbosityLevel()
    {
        StringWriter standardOut = new();

        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithVerbosityLevel(VerbosityLevel.Normal)
            .WithStandardOutTo(standardOut)
            .Verify(typeof(VerifierTests).Assembly);

        standardOut.ToString().Should().Contain("All Classes and Methods in");
    }

    [Fact]
    public void ShouldOutputDetailedVerbosityLevel()
    {
        StringWriter standardOut = new();

        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithVerbosityLevel(VerbosityLevel.Detailed)
            .WithStandardOutTo(standardOut)
            .Verify(typeof(VerifierTests).Assembly);

        standardOut.ToString().Should().Contain("All Classes and Methods in");
        standardOut.ToString().Should().Contain("Types found");
    }

    [Fact]
    public void ShouldOutputDiagnosticsVerbosityLevel()
    {
        StringWriter standardOut = new();

        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithVerbosityLevel(VerbosityLevel.Diagnostics)
            .WithStandardOutTo(standardOut)
            .Verify(typeof(VerifierTests).Assembly);

        standardOut.ToString().Should().Contain("All Classes and Methods in");
        standardOut.ToString().Should().Contain("Types found");
        standardOut.ToString().Should().Contain("Verifying [ILVerifier.Tests]ILVerifier.Tests.VerifierTests");
    }

    [Fact]
    public void ShouldThrowExceptionWhenILVerifyIsMissing()
    {
        var verifier = new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .WithPathToILVerify("rubbishPath");
        Action verifyAction = () => verifier.Verify(typeof(VerifierTests).Assembly);
        var exception = verifyAction.Should().Throw<InvalidOperationException>().WithMessage("Unable to execute 'ilverify'*");
    }


    [Fact]
    public void ShouldVerifyValidMethod()
    {
        var method = new VerifiableMethodSkeleton("Add", typeof(int), new Type[] { typeof(int), typeof(int) });
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Add);
        generator.Emit(OpCodes.Ret);

        Action createDelegate = () => method.CreateDelegate(typeof(Func<int, int, int>));
        createDelegate.Should().NotThrow();
    }

    [Fact]
    public void ShouldVerifyInvalidMethod()
    {
        var method = new VerifiableMethodSkeleton("Add", typeof(int), new Type[] { typeof(int), typeof(int) });
        var generator = method.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldarg_1);
        generator.Emit(OpCodes.Add);

        Action createDelegate = () => method.CreateDelegate(typeof(Func<int, int, int>));
        createDelegate.Should().Throw<InvalidProgramException>();
    }
}