using dotkit.Models;
using Serilog;

namespace dotkit.Services;

public class TemplateInstaller
{
    private readonly ProjectInfo _project;
    private readonly ILogger _logger;

    public string TemplatesPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Templates");

    public TemplateInstaller(ProjectInfo project)
    {
        _project = project;
        _logger = Log.ForContext<TemplateInstaller>();
    }

    public async Task InstallTemplatesAsync()
    {
        var projectDir = Path.GetDirectoryName(_project.ProjectPath)!;

        await InstallJwtSettingsAsync(projectDir, _project.Name);
        await InstallJwtServiceAsync(projectDir, _project.Name);
    }

    private async Task InstallJwtSettingsAsync(string projectDir, string ns)
    {
        var templatePath = Path.Combine(TemplatesPath, "JwtSettings.cs.template");
        var targetDir = Path.Combine(projectDir, "Configuration");
        var targetPath = Path.Combine(targetDir, "JwtSettings.cs");

        if (!File.Exists(templatePath))
        {
            _logger.Warning("Template not found: {Path}", templatePath);
            return;
        }

        Directory.CreateDirectory(targetDir);

        string content = await File.ReadAllTextAsync(templatePath);
        content = content.Replace("{{Namespace}}", ns);

        await File.WriteAllTextAsync(targetPath, content);
        _logger.Information("Created Configuration/JwtSettings.cs");
    }

    private async Task InstallJwtServiceAsync(string projectDir, string ns)
    {
        var templatePath = Path.Combine(TemplatesPath, "JwtService.cs.template");
        var targetDir = Path.Combine(projectDir, "Services");
        var targetPath = Path.Combine(targetDir, "JwtService.cs");

        if (!File.Exists(templatePath))
        {
            _logger.Warning("Template not found: {Path}", templatePath);
            return;
        }

        Directory.CreateDirectory(targetDir);

        string content = await File.ReadAllTextAsync(templatePath);
        content = content.Replace("{{Namespace}}", ns);

        await File.WriteAllTextAsync(targetPath, content);
        _logger.Information("Created Services/JwtService.cs");
    }
}
