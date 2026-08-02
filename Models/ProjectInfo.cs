namespace dotkit.Models;

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;
    public string AppSettingsPath { get; set; } = string.Empty;
    public bool HasUserSecrets { get; set; } = false;
    public bool IsWebApi { get; set; } = false;
    public List<string> TargetFrameworks { get; set; } = new();
    public int LowestTargetFrameworkMajor { get; set; } = 0;

    public ProjectInfo()
    {
    }
}
