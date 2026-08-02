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

            var arguments = string.IsNullOrEmpty(version)
                ? $"add \"{_project.ProjectPath}\" package {package}"
                : $"add \"{_project.ProjectPath}\" package {package} --version {version}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    WorkingDirectory = projectDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
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
}
