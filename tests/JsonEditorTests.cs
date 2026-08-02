using System.Text.Json;
using dotkit.Models;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class JsonEditorTests : IDisposable
{
    private readonly TempDir _tmp;
    private readonly string _templatesDir;
    private readonly string _appSettingsPath;
    private readonly ProjectInfo _project;
    private readonly JsonEditor _editor;

    public JsonEditorTests()
    {
        _tmp = new TempDir();
        _templatesDir = System.IO.Path.Combine(_tmp.Path, "Templates");
        Directory.CreateDirectory(_templatesDir);
        _appSettingsPath = System.IO.Path.Combine(_tmp.Path, "appsettings.json");
        _project = new ProjectInfo
        {
            Name = "TestProject",
            ProjectPath = System.IO.Path.Combine(_tmp.Path, "test.csproj"),
            ProgramPath = System.IO.Path.Combine(_tmp.Path, "Program.cs"),
            AppSettingsPath = _appSettingsPath,
            IsWebApi = true
        };
        _editor = new JsonEditor(_project) { TemplatesPath = _templatesDir };
    }

    public void Dispose()
    {
        _tmp.Dispose();
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_Throws_WhenTemplateMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _editor.UpdateAppSettingsAsync());
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_Throws_WhenTemplateInvalidJson()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"), "not json");
        await Assert.ThrowsAnyAsync<JsonException>(() => _editor.UpdateAppSettingsAsync());
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_Throws_WhenTemplateMissingJwtSection()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"), "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => _editor.UpdateAppSettingsAsync());
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_MergesJwt_IntoExistingAppSettings()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"),
            "{\"Jwt\":{\"SecretKey\":\"key123\",\"Issuer\":\"iss\",\"Audience\":\"aud\"}}");
        File.WriteAllText(_appSettingsPath,
            "{\"Logging\":{\"LogLevel\":{\"Default\":\"Information\"}}}");
        await _editor.UpdateAppSettingsAsync();
        var result = JsonDocument.Parse(File.ReadAllText(_appSettingsPath));
        Assert.True(result.RootElement.TryGetProperty("Logging", out _));
        var jwt = result.RootElement.GetProperty("Jwt");
        Assert.Equal("key123", jwt.GetProperty("SecretKey").GetString());
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_CreatesNewAppSettings_WhenNotExists()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"),
            "{\"Jwt\":{\"SecretKey\":\"key123\",\"Issuer\":\"iss\",\"Audience\":\"aud\"}}");
        await _editor.UpdateAppSettingsAsync();
        var result = JsonDocument.Parse(File.ReadAllText(_appSettingsPath));
        var jwt = result.RootElement.GetProperty("Jwt");
        Assert.Equal("key123", jwt.GetProperty("SecretKey").GetString());
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_HandlesEmptyAppSettings()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"),
            "{\"Jwt\":{\"SecretKey\":\"key123\"}}");
        File.WriteAllText(_appSettingsPath, "");
        await _editor.UpdateAppSettingsAsync();
        var result = JsonDocument.Parse(File.ReadAllText(_appSettingsPath));
        Assert.True(result.RootElement.TryGetProperty("Jwt", out _));
    }

    [Fact]
    public async Task UpdateAppSettingsAsync_OverridesValues_WhenProvided()
    {
        File.WriteAllText(System.IO.Path.Combine(_templatesDir, "appsettings.json"),
            "{\"Jwt\":{\"SecretKey\":\"placeholder\",\"Issuer\":\"placeholder\",\"Audience\":\"placeholder\"}}");
        await _editor.UpdateAppSettingsAsync(secretKey: "real-key-12345678901234567890", issuer: "real-issuer", audience: "real-audience");
        var result = JsonDocument.Parse(File.ReadAllText(_appSettingsPath));
        var jwt = result.RootElement.GetProperty("Jwt");
        Assert.Equal("real-key-12345678901234567890", jwt.GetProperty("SecretKey").GetString());
        Assert.Equal("real-issuer", jwt.GetProperty("Issuer").GetString());
        Assert.Equal("real-audience", jwt.GetProperty("Audience").GetString());
    }
}
