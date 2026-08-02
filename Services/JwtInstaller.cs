using System.Diagnostics;
using dotkit.Models;
using Serilog;

namespace dotkit.Services;

public class JwtInstaller
{
    private readonly ProjectInfo _project;
    private readonly ILogger _logger;
    private readonly PackageVersionResolver _versionResolver;

    public JwtInstaller(ProjectInfo project, PackageVersionResolver? versionResolver = null)
    {
        _project = project;
        _logger = Log.ForContext<JwtInstaller>();
        _versionResolver = versionResolver ?? new PackageVersionResolver();
    }

    public async Task InstallAsync()
    {
        var projectDir = Path.GetDirectoryName(_project.ProjectPath)!;

        var packages = new[] { "Microsoft.AspNetCore.Authentication.JwtBearer" };

        foreach (var package in packages)
        {
            _logger.Information("Installing package: {Package}", package);

            var version = await _versionResolver.ResolveAsync(package, _project.LowestTargetFrameworkMajor);

            if (string.IsNullOrEmpty(version))
                _logger.Warning("Could not determine a compatible version of {Package} for target framework major {Major}; installing without a version", package, _project.LowestTargetFrameworkMajor);
            else
                _logger.Information("Targeting .NET {Major}: installing {Package} version {Version}", _project.LowestTargetFrameworkMajor, package, version);

            var startInfo = BuildDotnetAddStartInfo(_project.ProjectPath, package, version);
            startInfo.WorkingDirectory = projectDir;

            using var process = new Process { StartInfo = startInfo };

            process.Start();
            _ = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.Error("Error: {Error}", error);
                throw new InvalidOperationException($"Failed to install {package}: {error}");
            }

            _logger.Information("Package {Package} installed", package);
        }
    }

    internal static ProcessStartInfo BuildDotnetAddStartInfo(string projectPath, string package, string? version)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("add");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("package");
        startInfo.ArgumentList.Add(package);

        if (!string.IsNullOrEmpty(version))
        {
            startInfo.ArgumentList.Add("--version");
            startInfo.ArgumentList.Add(version);
        }

        return startInfo;
    }
}
