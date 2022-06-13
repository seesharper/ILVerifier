namespace ILVerifier.Tests;

using System;
using System.IO;
using System.Reflection.Emit;
using System.Text;
using FluentAssertions;

public class VerifierTests
{
    [Fact]
    public void ShouldVerifyAssembly()
    {
        new Verifier()
            .WithAssemblyReferenceFromType<Verifier>()
            .WithAssemblyReference(typeof(FluentActions).Assembly)
            .Verify(typeof(VerifierTests).Assembly);
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
        standardOut.ToString().Should().Contain("Verifying [IlVerifier.Tests]ILVerifier.Tests.VerifierTests");
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

        Action createDelegate = () => method.CreateDelegate<Func<int, int, int>>();
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

        Action createDelegate = () => method.CreateDelegate<Func<int, int, int>>();
        createDelegate.Should().Throw<InvalidProgramException>();
    }
}