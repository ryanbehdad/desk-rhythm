using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskRhythm.Core;

/// <summary>
/// Reads and writes one small JSON file. Writes go to a temporary file first and
/// then replace the real file, so a crash cannot leave a half-written file.
/// A damaged file is kept as "*.bad" and the defaults are used.
/// </summary>
public sealed class JsonFileStore<T> where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Func<T> _createDefault;

    public JsonFileStore(string path, Func<T> createDefault)
    {
        Path = path;
        _createDefault = createDefault;
    }

    public string Path { get; }

    public T Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return _createDefault();
            }

            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? _createDefault();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            TryKeepDamagedCopy();
            return _createDefault();
        }
        catch (IOException)
        {
            return _createDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return _createDefault();
        }
    }

    public void Save(T value)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, Path, overwrite: true);
    }

    private void TryKeepDamagedCopy()
    {
        try
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
