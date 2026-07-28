using dotkit.Models;

namespace dotkit.Services;

public class ProjectDetector
{
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

        var projectInfo = new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            ProjectPath = projectPath,
            ProgramPath = Directory.GetFiles(ProjectDirectory, "Program.cs").FirstOrDefault()
                ?? throw new FileNotFoundException("Program.cs was not found."),
            AppSettingsPath = Directory.GetFiles(ProjectDirectory, "appsettings.json").FirstOrDefault()
                ?? Path.Combine(ProjectDirectory, "appsettings.json"),
            HasUserSecrets = projectContent.Contains("<UserSecretsId>"),
            IsWebApi = projectContent.Contains(@"Microsoft.NET.Sdk.Web")
        };

        if (!projectInfo.IsWebApi)
            throw new InvalidOperationException("The selected project is not an ASP.NET Core Web API.");

        return projectInfo;
    }
}
