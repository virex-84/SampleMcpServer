//https://github.com/virex-84

using System.ComponentModel;
using ModelContextProtocol.Server;
using System.Text;

/// <summary>
/// Tools for basic file operations.
/// </summary>
public class FileOperationsTools
{
    [McpServerTool]
    [Description("Writes content to a file.")]
    public string WriteFile(
        [Description("The file name with full path")] string filename,
        [Description("The content")] string content
    )
    {
        try
        {
            //не будем перезаписывать или дозаписывать
            //в уже существующий файл
            //что бы не испортить его
            if (File.Exists(filename))
                return "Such a file already exists!";

            File.WriteAllText(filename, content);
            return "Сontext write successful.";
        }
        catch (Exception ex)
        {
            return $"Error write context to file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Read file from the specified path.")]
    public string ReadFile(
    [Description("The file name with full path")] string filename)
    {
        //определеляем кодировку utf встроенным в StreamReader методом
        Encoding encoding = Encoding.Unicode;
        using (StreamReader reader = new StreamReader(filename, true))
        {
            //читаем один байт (BOM)
            while (reader.Peek() >= 0)
            {
                encoding = reader.CurrentEncoding;
                break;
            }
            reader.Close();
        }

        return File.ReadAllText(filename, encoding);
    }

    [McpServerTool]
    [Description("Lists files in the specified directory.")]
    public string ListFiles(
        [Description("The path of the directory to list files from")] string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return $"Error: Directory '{path}' does not exist.";
            }

            var files = Directory.GetFiles(path);
            var directories = Directory.GetDirectories(path);

            var result = new List<string>();
            result.Add("Files:");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                result.Add($"  {fileInfo.Name} ({fileInfo.Length} bytes)");
            }

            result.Add("\nDirectories:");
            foreach (var directory in directories)
            {
                var dirInfo = new DirectoryInfo(directory);
                result.Add($"  {dirInfo.Name}/");
            }

            return string.Join("\n", result);
        }
        catch (Exception ex)
        {
            return $"Error listing files: {ex.Message}";
        }
    }
}
