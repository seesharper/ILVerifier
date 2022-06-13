using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Lokad.ILPack;
using static SimpleExec.Command;
namespace ILVerifier;


public class Verifier
{
    private TextWriter _standardOutTextWriter = Console.Out;

    private string _pathToVerifiedAssembly;

    private readonly List<Assembly> _referencedAssemblies = new();

    private VerbosityLevel _verbosityLevel = VerbosityLevel.Quiet;

    private string _pathToILVerify = "ilverify";

    /// <summary>
    /// Sets the specified <see cref="VerbosityLevel"/>.
    /// </summary>
    /// <param name="verbosity">The <see cref="VerbosityLevel"/> to be used.</param>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithVerbosityLevel(VerbosityLevel verbosity)
    {
        _verbosityLevel = verbosity;
        return this;
    }

    /// <summary>
    /// Adds an assembly reference to the verifier.
    /// </summary>
    /// <param name="assembly">The assembly for which to add a reference.</param>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithAssemblyReference(Assembly assembly)
    {
        _referencedAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Adds an assembly reference to the verifier based on the given <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type from which to add an assembly reference.</typeparam>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithAssemblyReferenceFromType<T>()
    {
        WithAssemblyReference(typeof(T).Assembly);
        return this;
    }

    /// <summary>
    /// Sets the path to the "ILVerify" tool.
    /// </summary>
    /// <param name="pathToILVerify">The full path to the "ILVerify" tool.</param>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithPathToILVerify(string pathToILVerify)
    {
        _pathToILVerify = pathToILVerify;
        return this;
    }

    /// <summary>
    /// Sets the path to the generated assembly to be verified.
    /// </summary>
    /// <param name="pathToVerifiedAssembly">A fully qualified or relative path to the generated assembly to be verified.</param>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithVerifiedAssemblyPath(string pathToVerifiedAssembly)
    {
        _pathToVerifiedAssembly = pathToVerifiedAssembly;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="TextWriter"/> to used to forward standard out from "ILVerify".
    /// </summary>
    /// <param name="standardOutTextWriter">A <see cref="TextWriter"/> to used to forward standard out from "ILVerify".</param>
    /// <returns><see cref="Verifier"/></returns>
    public Verifier WithStandardOutTo(TextWriter standardOutTextWriter)
    {
        _standardOutTextWriter = standardOutTextWriter;
        return this;
    }

    /// <summary>
    /// Verifies the IL of the specified <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The <see cref="Assembly"/> to be verified.</param>
    public void Verify(Assembly assembly)
    {
        EnsureILVerifyIsInstalled();
        var fileName = GetFileName();
        SaveAssemblyToDisc(assembly, fileName);
        VerifyAssembly(fileName);
    }

    private void VerifyAssembly(string fileName)
    {
        try
        {
            (string standardOut, string standardError) = ReadAsync(_pathToILVerify, GetArguments(fileName)).GetAwaiter().GetResult();
            if (_verbosityLevel > VerbosityLevel.Quiet)
            {
                _standardOutTextWriter.Write(standardOut);
            }
        }
        catch (SimpleExec.ExitCodeReadException exitCodeReadException)
        {
            throw new InvalidProgramException(exitCodeReadException.StandardOutput);
        }
    }

    private void EnsureILVerifyIsInstalled()
    {
        try
        {
            ReadAsync(_pathToILVerify, "--version").GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            string message = "Unable to execute 'ilverify'. Ensure that the global tool is installed. Install with 'dotnet tool install dotnet-ilverify -g'";

            throw new InvalidOperationException(message, exception);
        }
    }

    private string GetArguments(string fileName)
    {
        string useVerboseOutputOption = GetVerbosityOption();
        return $"{Quote(fileName)} -r {Quote(GetFrameworkReferencePath())} {GetReferencedAssemblies()} {useVerboseOutputOption}";
    }

    private string GetVerbosityOption() => _verbosityLevel switch
    {
        VerbosityLevel.Normal => "",
        VerbosityLevel.Detailed => "--statistics",
        VerbosityLevel.Diagnostics => "--verbose --statistics",
        _ => ""
    };

    private static string Quote(string value) => $"\"{value}\"";

    private static void SaveAssemblyToDisc(Assembly assembly, string fileName)
    {
        var generator = new AssemblyGenerator();
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
        generator.GenerateAssembly(assembly, fileName);
    }

    private string GetFileName()
    {
        if (string.IsNullOrWhiteSpace(_pathToVerifiedAssembly))
        {
            return Path.Combine(GetOutputFolder(), "VerifiedAssembly.dll");
        }
        if (Path.IsPathFullyQualified(_pathToVerifiedAssembly))
        {
            return _pathToVerifiedAssembly;
        }

        return Path.Combine(GetOutputFolder(), _pathToVerifiedAssembly);
    }

    private static string GetOutputFolder() => Path.GetDirectoryName(typeof(Verifier).Assembly.Location);

    private static string GetFrameworkReferencePath() => Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll");

    private string GetReferencedAssemblies()
    {
        var sb = new StringBuilder();
        foreach (var referencedAssembly in _referencedAssemblies)
        {
            sb.Append($" -r {Quote(referencedAssembly.Location)}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Determines the verbosity of the messages written to standard out.
/// </summary>
public enum VerbosityLevel
{
    /// <summary>
    /// Write nothing.
    /// </summary>
    Quiet,

    /// <summary>
    /// Outputs the default output from "ILVerify".
    /// </summary>
    Normal,

    /// <summary>
    /// Outputs verbose output by adding the "--statistics" option to "ILVerify".
    /// </summary>

    Detailed,

    /// <summary>
    /// Outputs all available output by adding both "--verbose" and "--statistics" to "ILVerify".
    /// </summary>
    Diagnostics
}