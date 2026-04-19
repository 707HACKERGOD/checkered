#if TOOLS
using Godot;
using System.Text;
using System.Collections.Generic;

[Tool]
public partial class ProjectDumperPlugin : EditorPlugin
{
    private static readonly HashSet<string> TextExtensions = new HashSet<string>
    {
        ".cs", ".gdshader"
    };

    private bool _menuAdded = false;

    public override void _EnterTree()
    {
        // Do nothing here – we'll add the menu in _Ready to ensure editor is fully initialized
    }

    public override void _Ready()
    {
        base._Ready();
        CallDeferred(nameof(AddMenuLater)); // Use CallDeferred to avoid any timing issues
    }

    private void AddMenuLater()
    {
        try
        {
            if (!_menuAdded)
            {
                AddToolMenuItem("Dump Project Info", new Callable(this, nameof(DumpProjectInfo)));
                _menuAdded = true;
                GD.Print("Project Dumper plugin initialized successfully.");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Failed to add menu item: {e.Message}");
        }
    }

    public override void _ExitTree()
    {
        try
        {
            if (_menuAdded)
            {
                RemoveToolMenuItem("Dump Project Info");
                _menuAdded = false;
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Failed to remove menu item: {e.Message}");
        }
    }

    private void DumpProjectInfo()
    {
        try
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("=== GODOT PROJECT DUMP ===\n");
            output.AppendLine($"Generated: {System.DateTime.Now}\n");

            output.AppendLine("=== FILE TREE AND SOURCE CONTENTS ===\n");
            DumpDirectory("res://", output, 0);

            output.AppendLine("\n=== PROJECT SETTINGS ===\n");
            foreach (var property in ProjectSettings.Singleton.GetPropertyList())
            {
                string name = property["name"].AsString();
                var value = ProjectSettings.Singleton.GetSetting(name);
                output.AppendLine($"{name} = {value}");
            }

            // Safely attempt to dump editor settings
            if (Engine.IsEditorHint())
            {
                output.AppendLine("\n=== EDITOR SETTINGS ===\n");
                var editorSettings = EditorInterface.Singleton.GetEditorSettings();
                if (editorSettings != null)
                {
                    foreach (var property in editorSettings.GetPropertyList())
                    {
                        string name = property["name"].AsString();
                        var value = editorSettings.Get(name);
                        output.AppendLine($"{name} = {value}");
                    }
                }
                else
                {
                    output.AppendLine("(Editor settings not available)");
                }
            }

            // Save to D:\project_dump.txt
            string filePath = "D:/project_dump.txt";
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"Failed to open file for writing: {filePath}");
                return;
            }
            file.StoreString(output.ToString());
            GD.Print($"Project dump saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Error during dump: {e.Message}\n{e.StackTrace}");
        }
    }

    private void DumpDirectory(string path, StringBuilder sb, int indentLevel)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null)
        {
            sb.AppendLine($"{new string(' ', indentLevel * 2)}[Failed to open: {path}]");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (fileName == "." || fileName == "..")
            {
                fileName = dir.GetNext();
                continue;
            }

            // ✅ Simply concatenate – path already ends with '/'
            string fullPath = path + fileName;
            string indent = new string(' ', indentLevel * 2);

            if (dir.CurrentIsDir())
            {
                sb.AppendLine($"{indent}+ {fileName}/");
                // ✅ Pass the full path with a trailing slash for the next level
                DumpDirectory(fullPath + "/", sb, indentLevel + 1);
            }
            else
            {
                sb.AppendLine($"{indent}- {fileName}");

                // Check extension using Godot's string method
                string ext = fileName.GetExtension().ToLower();
                if (!string.IsNullOrEmpty(ext) && TextExtensions.Contains("." + ext))
                {
                    string content = ReadFileContent(fullPath);  // fullPath is correct res://path
                    if (content != null)
                    {
                        string[] lines = content.Split('\n');
                        foreach (string line in lines)
                        {
                            sb.AppendLine($"{indent}  {line}");
                        }
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine($"{indent}  [Error reading file]");
                    }
                }
            }

            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
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