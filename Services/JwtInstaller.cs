using System.Diagnostics;
using dotkit.Models;
using Serilog;

namespace dotkit.Services;

public class JwtInstaller
{
    private readonly ProjectInfo _project;
    private readonly ILogger _logger;

    public JwtInstaller(ProjectInfo project)
    {
        _project = project;
        _logger = Log.ForContext<JwtInstaller>();
    }

    public async Task InstallAsync()
    {
        var projectDir = Path.GetDirectoryName(_project.ProjectPath)!;

        var packages = new[] { "Microsoft.AspNetCore.Authentication.JwtBearer" };

        foreach (var package in packages)
        {
            _logger.Information("Installing package: {Package}", package);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"add \"{_project.ProjectPath}\" package {package}",
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
