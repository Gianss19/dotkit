using System.Diagnostics;
using dotkit.Models;
using Serilog;

namespace dotkit.Services;

public class UserSecretsManager
{
    private readonly ProjectInfo _project;
    private readonly ILogger _logger;

    public UserSecretsManager(ProjectInfo project)
    {
        _project = project;
        _logger = Log.ForContext<UserSecretsManager>();
    }

    public async Task ConfigureAsync(string secretKey, string issuer, string audience)
    {
        _logger.Information("Initializing User Secrets...");
        await InitUserSecretsAsync();

        _logger.Information("Setting Jwt:SecretKey...");
        await SetSecretAsync("Jwt:SecretKey", secretKey);

        _logger.Information("Setting Jwt:Issuer = {Issuer}", issuer);
        await SetSecretAsync("Jwt:Issuer", issuer);

        _logger.Information("Setting Jwt:Audience = {Audience}", audience);
        await SetSecretAsync("Jwt:Audience", audience);

        _logger.Information("User Secrets configured");
    }

    private async Task InitUserSecretsAsync()
    {
        if (_project.HasUserSecrets)
        {
            _logger.Information("User Secrets already initialized");
            return;
        }

        await ExecuteDotnetAsync(
            BuildUserSecretsStartInfo("user-secrets", "init", "--project", _project.ProjectPath));
    }

    private async Task SetSecretAsync(string key, string value)
    {
        await ExecuteDotnetAsync(
            BuildUserSecretsStartInfo("user-secrets", "set", "--project", _project.ProjectPath, key, value));
    }

    private static ProcessStartInfo BuildUserSecretsStartInfo(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private async Task ExecuteDotnetAsync(ProcessStartInfo startInfo)
    {
        startInfo.WorkingDirectory ??= Path.GetDirectoryName(_project.ProjectPath)!;

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        _ = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Error: dotnet {startInfo.ArgumentList.FirstOrDefault()}\n{error}");
    }
}
