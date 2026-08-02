using dotkit.Models;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class ProjectDetectorTests
{
    [Fact]
    public void Detect_Throws_WhenDirectoryNotFound()
    {
        var detector = new ProjectDetector(@"C:\nonexistent_dir_12345");
        Assert.Throws<DirectoryNotFoundException>(() => detector.Detect());
    }

    [Fact]
    public void Detect_Throws_WhenNoCsproj()
    {
        using var tmp = new TempDir();
        var detector = new ProjectDetector(tmp.Path);
        Assert.Throws<FileNotFoundException>(() => detector.Detect());
    }

    [Fact]
    public void Detect_Throws_WhenNoProgramCs()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        var detector = new ProjectDetector(tmp.Path);
        Assert.Throws<FileNotFoundException>(() => detector.Detect());
    }

    [Fact]
    public void Detect_Throws_WhenNotWebApi()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\">");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        Assert.Throws<InvalidOperationException>(() => detector.Detect());
    }

    [Fact]
    public void Detect_ReturnsProjectInfo_WhenValidProject()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "Console.WriteLine();");
        var detector = new ProjectDetector(tmp.Path);
        var info = detector.Detect();
        Assert.Equal("test", info.Name);
        Assert.Contains("test.csproj", info.ProjectPath);
        Assert.Contains("Program.cs", info.ProgramPath);
        Assert.True(info.IsWebApi);
        Assert.False(info.HasUserSecrets);
    }

    [Fact]
    public void Detect_DetectsUserSecrets()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><UserSecretsId>abc123</UserSecretsId></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        var info = detector.Detect();
        Assert.True(info.HasUserSecrets);
    }

    [Fact]
    public void Detect_FallsBackToDefaultAppSettingsPath()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        var info = detector.Detect();
        Assert.EndsWith("appsettings.json", info.AppSettingsPath);
    }

    [Fact]
    public void Detect_Throws_WhenTargetFrameworkBelowNet6()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net5.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        var ex = Assert.Throws<InvalidOperationException>(() => detector.Detect());
        Assert.Contains("net5.0", ex.Message);
        Assert.Contains(".NET 6.0", ex.Message);
    }

    [Fact]
    public void Detect_AcceptsNet6Project()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net6.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        var info = detector.Detect();
        Assert.Equal(6, info.LowestTargetFrameworkMajor);
        Assert.Contains("net6.0", info.TargetFrameworks);
    }

    [Fact]
    public void Detect_ComputesLowestMajor_ForMultiTarget()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFrameworks>net8.0;net6.0;net10.0</TargetFrameworks></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        var info = detector.Detect();
        Assert.Equal(6, info.LowestTargetFrameworkMajor);
        Assert.Equal(3, info.TargetFrameworks.Count);
    }

    [Fact]
    public void Detect_Throws_WhenMultiTargetIncludesOlderTfm()
    {
        using var tmp = new TempDir();
        File.WriteAllText(Path.Combine(tmp.Path, "test.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFrameworks>net6.0;netstandard2.0</TargetFrameworks></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(tmp.Path, "Program.cs"), "");
        var detector = new ProjectDetector(tmp.Path);
        Assert.Throws<InvalidOperationException>(() => detector.Detect());
    }

    [Fact]
    public void ExtractTargetFrameworks_IgnoresAttributesAndPlatformSuffixes()
    {
        var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework Condition="'$(Foo)' == 'bar'">net8.0-windows</TargetFramework>
  </PropertyGroup>
</Project>
""";
        var tfms = ProjectDetector.ExtractTargetFrameworks(content);
        Assert.Single(tfms);
        Assert.Equal("net8.0-windows", tfms[0]);
        Assert.Equal(8, ProjectDetector.TryGetNetMajor("net8.0-windows"));
    }

    [Fact]
    public void TryGetNetMajor_ReturnsZero_ForNonNetTfm()
    {
        Assert.Equal(0, ProjectDetector.TryGetNetMajor("netstandard2.0"));
        Assert.Equal(0, ProjectDetector.TryGetNetMajor("netcoreapp3.1"));
        Assert.Equal(10, ProjectDetector.TryGetNetMajor("net10.0"));
    }
}

public class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { }
    }
}
