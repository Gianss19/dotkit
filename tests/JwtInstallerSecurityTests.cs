using System.Diagnostics;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class JwtInstallerSecurityTests
{
    [Theory]
    [InlineData("C:\\tmp\\my api\\proj.csproj")]
    [InlineData("C:\\tmp\\bad\";del /q *\\proj.csproj")]
    [InlineData("C:\\tmp\\x&calc\\proj.csproj")]
    [InlineData("C:\\tmp\\$(whoami)\\proj.csproj")]
    [InlineData("C:\\tmp\\`echo hacked`\\proj.csproj")]
    public void BuildDotnetAddStartInfo_PassesArgumentsVerbatim_WithoutShellInterpolation(string projectPath)
    {
        var startInfo = JwtInstaller.BuildDotnetAddStartInfo(
            projectPath, "Microsoft.AspNetCore.Authentication.JwtBearer", "8.0.29");

        var args = startInfo.ArgumentList.ToList();

        Assert.Equal(
            new[] { "add", projectPath, "package", "Microsoft.AspNetCore.Authentication.JwtBearer", "--version", "8.0.29" },
            args);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildDotnetAddStartInfo_OmitsVersion_WhenNullOrEmpty()
    {
        var withNull = JwtInstaller.BuildDotnetAddStartInfo("C:\\tmp\\a.csproj", "Some.Package", null);
        var withEmpty = JwtInstaller.BuildDotnetAddStartInfo("C:\\tmp\\a.csproj", "Some.Package", string.Empty);

        Assert.Equal(new[] { "add", "C:\\tmp\\a.csproj", "package", "Some.Package" }, withNull.ArgumentList);
        Assert.Equal(new[] { "add", "C:\\tmp\\a.csproj", "package", "Some.Package" }, withEmpty.ArgumentList);
    }

    [Fact]
    public void BuildDotnetAddStartInfo_UsesArgumentList_NotFlatArgumentsString()
    {
        var startInfo = JwtInstaller.BuildDotnetAddStartInfo("C:\\tmp\\a.csproj", "p", null);

        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.True(startInfo.ArgumentList.Count > 0);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildDotnetAddStartInfo_AllowsSpaces_AsSingleArgument()
    {
        var startInfo = JwtInstaller.BuildDotnetAddStartInfo("C:\\path with spaces\\a.csproj", "p", "8.0.29");

        Assert.Single(startInfo.ArgumentList.Where(a => a == "C:\\path with spaces\\a.csproj"));
        Assert.DoesNotContain(startInfo.ArgumentList, a => a.Contains("--source"));
    }
}
