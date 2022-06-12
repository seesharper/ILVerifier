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
    private readonly ILVerifierOptions _options = new();     
    
    public Verifier Configure(Action<ILVerifierOptions> configureOptions)
    {
        configureOptions(_options);
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
        var arguments = GetArguments(fileName);
        
        try
        {
            ReadAsync(_options.PathToILVerify,GetArguments(fileName)).GetAwaiter().GetResult();
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
            ReadAsync(_options.PathToILVerify, "--version").GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            string message = "Unable to execute `ilverify`. Ensure that the global tool is installed. Install with `dotnet tool install dotnet-ilverify -g`";
            
            throw new InvalidOperationException(message,exception);
        }
    }

    private string GetArguments(string fileName)
    {
        string useVerboseOutputOption = _options.UseVerboseOutput ? "--verbose" : string.Empty;        
        return $"{Quote(fileName)} -r {Quote(GetFrameworkReferencePath())} {GetReferencedAssemblies()} {useVerboseOutputOption}";        
    }

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
        if (string.IsNullOrWhiteSpace(_options.FileName))
        {
            _options.FileName = "VerifiedAssembly.dll";
            return Path.Combine(GetOutputFolder(), _options.FileName);
        } 
        if (Path.IsPathFullyQualified(_options.FileName))
        {
            return _options.FileName;
        }      
        
        return Path.Combine(GetOutputFolder(),_options.FileName);
    }

    private static string GetOutputFolder() => Path.GetDirectoryName(typeof(Verifier).Assembly.Location);

    private static string GetFrameworkReferencePath() => Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll");

    private string GetReferencedAssemblies()
    {
        var sb = new StringBuilder();
        foreach (var referencedAssembly in _options.ReferencedAssemblies)
        {
            sb.Append($" -r {Quote(referencedAssembly.Location)}");
        }
        return sb.ToString();        
    }
}


public class ILVerifierOptions
{
    public string FileName { get; set; }

    public List<Assembly> ReferencedAssemblies { get; } = new List<Assembly>();

    public bool UseVerboseOutput { get; set; }

    public bool UseStatistics { get; set; }

    public string PathToILVerify {get;set;} = "ilverify";
}
