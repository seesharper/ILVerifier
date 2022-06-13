using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Lokad.ILPack;
using static SimpleExec.Command;
namespace ILVerifer;
public class Verifier
{
    private TextWriter _standardOutTextWriter = Console.Out;

    private string _pathToVerifiedAssembly;

    public List<Assembly> _referencedAssemblies { get; } = new List<Assembly>();

    private VerbosityLevel _verbosityLevel = VerbosityLevel.Quiet;

    private string _pathToILVerify = "ilverify";

    public Verifier WithVerbosityLevel(VerbosityLevel verbosity)
    {
        _verbosityLevel = verbosity;
        return this;
    }

    public Verifier WithAssemblyReference(Assembly assembly)
    {
        _referencedAssemblies.Add(assembly);
        return this;
    }

    public Verifier WithAssemblyReferenceFromType<T>()
    {
        WithAssemblyReference(typeof(T).Assembly);
        return this;
    }

    public Verifier WithPathToILVerify(string pathToILVerify)
    {
        _pathToILVerify = pathToILVerify;
        return this;
    }

    public Verifier WithVerifiedAssemblyPath(string pathToVerifiedAssembly)
    {
        _pathToVerifiedAssembly = pathToVerifiedAssembly;
        return this;
    }

    public Verifier WithStandardOutTo(TextWriter standardOutTextWriter)
    {
        _standardOutTextWriter = standardOutTextWriter;
        return this;
    }

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
