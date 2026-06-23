#if TOOLS
using Godot;
using System.Text;
using System.Collections.Generic;

[Tool]
public partial class ProjectDumperPlugin : EditorPlugin
{
    // Only dump these file types
    private static readonly HashSet<string> TextExtensions = new HashSet<string>
    {
        ".cs", ".gd", ".gdshader"
    };

    // Skip directories that contain generated, build, or editor‑only data
    private static readonly HashSet<string> SkippedDirs = new HashSet<string>
    {
        ".godot", ".git", "addons",
        "android",      // Android export/build folder
        "builds",       // Exported builds and screenshots
        "terrain_data", // Terrain data files
        ".gradle",      // Gradle cache
        "build",        // Android build intermediates (top‑level, if any)
        "libs",         // Native libraries (usually generated)
        "res",          // Android resources
        "src",          // Android Java sources (not relevant)
        "outputs",      // Gradle outputs
        "intermediates",// Gradle intermediates
        "reports",      // Gradle reports
        "tmp",          // Gradle temp
        "generated",    // Generated code
        "assets"        // Lower‑case Android assets (distinct from your Assets/ folder)
    };

    private bool _menuAdded = false;

    public override void _EnterTree() { }

    public override void _Ready()
    {
        base._Ready();
        CallDeferred(nameof(AddMenuLater));
    }

    private void AddMenuLater()
    {
        if (!_menuAdded)
        {
            AddToolMenuItem("Dump Project Info", new Callable(this, nameof(DumpProjectInfo)));
            _menuAdded = true;
        }
    }

    public override void _ExitTree()
    {
        if (_menuAdded)
        {
            RemoveToolMenuItem("Dump Project Info");
            _menuAdded = false;
        }
    }

    private void DumpProjectInfo()
    {
        StringBuilder output = new StringBuilder();
        output.AppendLine("=== GODOT PROJECT CODE DUMP ===");
        output.AppendLine($"Generated: {System.DateTime.Now}");
        output.AppendLine();

        DumpDirectory("res://", output, 0);

        string filePath = "D:/project_code_dump.txt";
        using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Failed to open file for writing: {filePath}");
            return;
        }
        file.StoreString(output.ToString());
        GD.Print($"Project code dump saved to: {filePath}");
    }

    private void DumpDirectory(string path, StringBuilder sb, int indentLevel)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null)
            return;

        var directories = new List<string>();
        var files = new List<string>();

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (fileName == "." || fileName == "..")
            {
                fileName = dir.GetNext();
                continue;
            }

            if (dir.CurrentIsDir())
            {
                if (!SkippedDirs.Contains(fileName))
                    directories.Add(fileName);
            }
            else
            {
                files.Add(fileName);
            }

            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        string indent = new string(' ', indentLevel * 2);
        directories.Sort();
        files.Sort();

        foreach (string dirName in directories)
        {
            sb.AppendLine($"{indent}+ {dirName}/");
            DumpDirectory(path + dirName + "/", sb, indentLevel + 1);
        }

        foreach (string file in files)
        {
            sb.AppendLine($"{indent}- {file}");

            string ext = file.GetExtension().ToLower();
            if (!string.IsNullOrEmpty(ext) && TextExtensions.Contains("." + ext))
            {
                string content = ReadFileContent(path + file);
                if (content != null)
                {
                    sb.AppendLine($"{indent}  ```");
                    foreach (string line in content.Split('\n'))
                        sb.AppendLine($"{indent}  {line}");
                    sb.AppendLine($"{indent}  ```");
                }
                else
                {
                    sb.AppendLine($"{indent}  [Error reading file]");
                }
            }
        }
    }

    private string ReadFileContent(string resPath)
    {
        using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;
        return file.GetAsText();
    }
}
#endif