using dotkit.Models;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class TemplateInstallerTests
{
    [Fact]
    public async Task InstallTemplatesAsync_CreatesJwtSettings()
    {
        using var tmp = new TempDir();
        var templatesDir = System.IO.Path.Combine(tmp.Path, "Templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtSettings.cs.template"),
            "namespace {{Namespace}}.Configuration; public class JwtSettings {{ }}");
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtService.cs.template"),
            "namespace {{Namespace}}.Services; public class JwtService {{ }}");

        var projectDir = System.IO.Path.Combine(tmp.Path, "Project");
        Directory.CreateDirectory(projectDir);
        var projectPath = System.IO.Path.Combine(projectDir, "test.csproj");
        File.WriteAllText(projectPath, "");

        var project = new ProjectInfo
        {
            Name = "MyApp",
            ProjectPath = projectPath
        };

        var installer = new TemplateInstaller(project) { TemplatesPath = templatesDir };
        await installer.InstallTemplatesAsync();

        var settingsPath = System.IO.Path.Combine(projectDir, "Configuration", "JwtSettings.cs");
        Assert.True(File.Exists(settingsPath));
        var content = File.ReadAllText(settingsPath);
        Assert.Contains("namespace MyApp.Configuration;", content);
    }

    [Fact]
    public async Task InstallTemplatesAsync_CreatesJwtService()
    {
        using var tmp = new TempDir();
        var templatesDir = System.IO.Path.Combine(tmp.Path, "Templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtSettings.cs.template"),
            "namespace {{Namespace}}.Configuration;");
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtService.cs.template"),
            "namespace {{Namespace}}.Services; public class JwtService {{ }}");

        var projectDir = System.IO.Path.Combine(tmp.Path, "Project");
        Directory.CreateDirectory(projectDir);
        var projectPath = System.IO.Path.Combine(projectDir, "test.csproj");
        File.WriteAllText(projectPath, "");

        var project = new ProjectInfo { Name = "MyApp", ProjectPath = projectPath };
        var installer = new TemplateInstaller(project) { TemplatesPath = templatesDir };
        await installer.InstallTemplatesAsync();

        var servicePath = System.IO.Path.Combine(projectDir, "Services", "JwtService.cs");
        Assert.True(File.Exists(servicePath));
        var content = File.ReadAllText(servicePath);
        Assert.Contains("namespace MyApp.Services;", content);
    }

    [Fact]
    public async Task InstallTemplatesAsync_ReplacesNamespace()
    {
        using var tmp = new TempDir();
        var templatesDir = System.IO.Path.Combine(tmp.Path, "Templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtSettings.cs.template"),
            "namespace {{Namespace}}.Configuration;");
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtService.cs.template"),
            "namespace {{Namespace}}.Services;");

        var projectDir = System.IO.Path.Combine(tmp.Path, "Project");
        Directory.CreateDirectory(projectDir);
        var projectPath = System.IO.Path.Combine(projectDir, "test.csproj");
        File.WriteAllText(projectPath, "");

        var project = new ProjectInfo { Name = "CustomNs", ProjectPath = projectPath };
        var installer = new TemplateInstaller(project) { TemplatesPath = templatesDir };
        await installer.InstallTemplatesAsync();

        var content = File.ReadAllText(System.IO.Path.Combine(projectDir, "Configuration", "JwtSettings.cs"));
        Assert.DoesNotContain("{{Namespace}}", content);
        Assert.Contains("CustomNs", content);
    }

    [Fact]
    public async Task InstallTemplatesAsync_Skips_WhenTemplateMissing()
    {
        using var tmp = new TempDir();
        var templatesDir = System.IO.Path.Combine(tmp.Path, "Templates");
        Directory.CreateDirectory(templatesDir);

        var projectDir = System.IO.Path.Combine(tmp.Path, "Project");
        Directory.CreateDirectory(projectDir);
        var projectPath = System.IO.Path.Combine(projectDir, "test.csproj");
        File.WriteAllText(projectPath, "");

        var project = new ProjectInfo { Name = "MyApp", ProjectPath = projectPath };
        var installer = new TemplateInstaller(project) { TemplatesPath = templatesDir };
        await installer.InstallTemplatesAsync();

        Assert.False(File.Exists(System.IO.Path.Combine(projectDir, "Configuration", "JwtSettings.cs")));
        Assert.False(File.Exists(System.IO.Path.Combine(projectDir, "Services", "JwtService.cs")));
    }

    [Fact]
    public async Task InstallTemplatesAsync_CreatesConfigurationDirectory()
    {
        using var tmp = new TempDir();
        var templatesDir = System.IO.Path.Combine(tmp.Path, "Templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtSettings.cs.template"),
            "namespace {{Namespace}}.Configuration;");
        File.WriteAllText(System.IO.Path.Combine(templatesDir, "JwtService.cs.template"),
            "namespace {{Namespace}}.Services;");

        var projectDir = System.IO.Path.Combine(tmp.Path, "Project");
        var projectPath = System.IO.Path.Combine(projectDir, "test.csproj");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(projectPath, "");

        var project = new ProjectInfo { Name = "MyApp", ProjectPath = projectPath };
        var installer = new TemplateInstaller(project) { TemplatesPath = templatesDir };
        await installer.InstallTemplatesAsync();

        Assert.True(Directory.Exists(System.IO.Path.Combine(projectDir, "Configuration")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(projectDir, "Services")));
    }
}
