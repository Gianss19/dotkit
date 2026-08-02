using dotkit.Models;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class ProgramEditorTests
{
    [Fact]
    public void ContainsJwtConfiguration_ReturnsTrue_WhenBothPresent()
    {
        var code = "blah AddAuthentication blah AddJwtBearer blah";
        Assert.True(ProgramEditor.ContainsJwtConfiguration(code));
    }

    [Fact]
    public void ContainsJwtConfiguration_ReturnsTrue_WhenFullConfigPresent()
    {
        var code = """
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => { });
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<JwtService>();
""";
        Assert.True(ProgramEditor.ContainsJwtConfiguration(code));
    }

    [Fact]
    public void ContainsJwtConfiguration_ReturnsFalse_WhenOnlyAddAuthentication()
    {
        var code = "blah AddAuthentication blah";
        Assert.False(ProgramEditor.ContainsJwtConfiguration(code));
    }

    [Fact]
    public void ContainsJwtConfiguration_ReturnsFalse_WhenOnlyAddJwtBearer()
    {
        var code = "blah AddJwtBearer blah";
        Assert.False(ProgramEditor.ContainsJwtConfiguration(code));
    }

    [Fact]
    public void ContainsJwtConfiguration_ReturnsFalse_WhenNeitherPresent()
    {
        var code = "Console.WriteLine();";
        Assert.False(ProgramEditor.ContainsJwtConfiguration(code));
    }

    [Fact]
    public void InjectJwtConfiguration_AddsUsing_WhenMissing()
    {
        var code = """
using Microsoft.AspNetCore.Builder;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Run();
""";
        var result = ProgramEditor.InjectJwtConfiguration(code, "MyApp");
        Assert.Contains("using MyApp.Configuration;", result);
        Assert.Contains("using MyApp.Services;", result);
    }

    [Fact]
    public void InjectJwtConfiguration_DoesNotAddDuplicateUsing()
    {
        var code = """
using MyApp.Configuration;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Run();
""";
        var result = ProgramEditor.InjectJwtConfiguration(code, "MyApp");
        Assert.Equal(1, CountOccurrences(result, "using MyApp.Configuration;"));
        Assert.Equal(1, CountOccurrences(result, "using MyApp.Services;"));
    }

    [Fact]
    public void InjectJwtConfiguration_InjectsAfterLastServicesAdd()
    {
        var code = """
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSwagger();
var app = builder.Build();
app.Run();
""";
        var result = ProgramEditor.InjectJwtConfiguration(code, "MyApp");
        Assert.Contains("AddJwtBearer", result);
        Assert.Contains("Configure<JwtSettings>", result);
        Assert.Contains("AddScoped<JwtService>", result);
        var addSwaggerIdx = result.IndexOf("AddSwagger");
        var jwtIdx = result.IndexOf("AddJwtBearer");
        Assert.True(jwtIdx > addSwaggerIdx);
    }

    [Fact]
    public void InjectJwtConfiguration_AddsUseAuthenticationBeforeBuild()
    {
        var code = """
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Run();
""";
        var result = ProgramEditor.InjectJwtConfiguration(code, "MyApp");
        var useAuthIdx = result.IndexOf("UseAuthentication");
        var buildIdx = result.IndexOf("builder.Build");
        Assert.True(useAuthIdx > buildIdx, "UseAuthentication should appear after Build");
    }

    [Fact]
    public void InjectJwtConfiguration_HandlesNoExistingUsings()
    {
        var code = """
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Run();
""";
        var result = ProgramEditor.InjectJwtConfiguration(code, "MyApp");
        Assert.Contains("using MyApp.Configuration;", result);
        Assert.Contains("using MyApp.Services;", result);
    }

    [Fact]
    public async Task UpdateProgramAsync_WritesUpdatedContent()
    {
        using var tmp = new TempDir();
        var programPath = System.IO.Path.Combine(tmp.Path, "Program.cs");
        var original = """
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.Run();
""";
        File.WriteAllText(programPath, original);
        var project = new ProjectInfo
        {
            Name = "TestProject",
            ProjectPath = System.IO.Path.Combine(tmp.Path, "test.csproj"),
            ProgramPath = programPath,
            IsWebApi = true
        };
        var editor = new ProgramEditor(project);
        var result = await editor.UpdateProgramAsync();
        Assert.Contains("AddJwtBearer", result);
        Assert.Contains("Configure<JwtSettings>", result);
        Assert.Contains("AddScoped<JwtService>", result);
        Assert.Contains("using TestProject.Configuration;", result);
        Assert.Contains("using TestProject.Services;", result);
    }

    [Fact]
    public async Task UpdateProgramAsync_Skips_WhenAlreadyConfigured()
    {
        using var tmp = new TempDir();
        var programPath = System.IO.Path.Combine(tmp.Path, "Program.cs");
        var alreadyConfigured = """
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication().AddJwtBearer();
var app = builder.Build();
app.Run();
""";
        File.WriteAllText(programPath, alreadyConfigured);
        var project = new ProjectInfo
        {
            Name = "Test",
            ProjectPath = System.IO.Path.Combine(tmp.Path, "test.csproj"),
            ProgramPath = programPath,
            IsWebApi = true
        };
        var editor = new ProgramEditor(project);
        var result = await editor.UpdateProgramAsync();
        Assert.Equal(alreadyConfigured, result);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
