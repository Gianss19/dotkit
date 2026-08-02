using System.Text.RegularExpressions;
using dotkit.Models;

namespace dotkit.Services;

public class ProjectDetector
{
    private static readonly Regex TargetFrameworkRegex = new(
        @"<TargetFramework[s]?\b[^>]*>(?<tfms>[^<]+)</TargetFramework[s]?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NetMajorRegex = new(
        @"^net(?<major>\d+)\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string ProjectDirectory { get; }

    public ProjectDetector(string projectDirectory)
    {
        ProjectDirectory = projectDirectory;
    }

    public ProjectInfo Detect()
    {
        if (!Directory.Exists(ProjectDirectory))
            throw new DirectoryNotFoundException($"Directory '{ProjectDirectory}' not found.");

        var projectPath = Directory
            .GetFiles(ProjectDirectory, "*.csproj")
            .FirstOrDefault();

        if (projectPath is null)
            throw new FileNotFoundException("No .csproj file was found.");

        var projectContent = File.ReadAllText(projectPath);

        var targetFrameworks = ExtractTargetFrameworks(projectContent);
        ValidateTargetFrameworks(targetFrameworks);

        var projectInfo = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            ProjectPath = projectPath,
            ProgramPath = Directory.GetFiles(ProjectDirectory, "Program.cs").FirstOrDefault()
                ?? throw new FileNotFoundException("Program.cs was not found."),
            AppSettingsPath = Directory.GetFiles(ProjectDirectory, "appsettings.json").FirstOrDefault()
                ?? Path.Combine(ProjectDirectory, "appsettings.json"),
            HasUserSecrets = projectContent.Contains("<UserSecretsId>"),
            IsWebApi = projectContent.Contains(@"Microsoft.NET.Sdk.Web"),
            TargetFrameworks = targetFrameworks,
            LowestTargetFrameworkMajor = targetFrameworks
                .Select(TryGetNetMajor)
                .Where(m => m > 0)
                .DefaultIfEmpty(0)
                .Min()
        };

        if (!projectInfo.IsWebApi)
            throw new InvalidOperationException("The selected project is not an ASP.NET Core Web API.");

        return projectInfo;
    }

    public static List<string> ExtractTargetFrameworks(string projectContent)
    {
        var frameworks = new List<string>();

        foreach (Match match in TargetFrameworkRegex.Matches(projectContent))
        {
            var value = match.Groups["tfms"].Value;
            foreach (var framework in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!frameworks.Contains(framework))
                    frameworks.Add(framework);
            }
        }

        return frameworks;
    }

    public static void ValidateTargetFrameworks(List<string> targetFrameworks)
    {
        var unsupported = new List<string>();

        foreach (var tfm in targetFrameworks)
        {
            var major = TryGetNetMajor(tfm);
            if (major == 0 || major < 6)
                unsupported.Add(tfm);
        }

        if (unsupported.Count > 0)
            throw new InvalidOperationException(
                $"dotkit requires a project targeting .NET 6.0 or later. Found: {string.Join(", ", unsupported)}");
    }

    public static int TryGetNetMajor(string targetFramework)
    {
        var match = NetMajorRegex.Match(targetFramework);
        return match.Success && int.TryParse(match.Groups["major"].Value, out var major)
            ? major
            : 0;
    }
}
