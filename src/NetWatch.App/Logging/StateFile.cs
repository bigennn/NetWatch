using System.Text;
using System.Text.Json;
using NetWatch.Core.Logging;

namespace NetWatch.App.Logging;

/// <summary>
/// Liest und schreibt state.json. Geschrieben wird erst in eine temporäre Datei, die dann die alte ersetzt,
/// damit bei einem Absturz mitten im Schreiben keine halbe Datei übrig bleibt.
/// </summary>
internal sealed class StateFile(string path)
{
    public RunState? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return RunState.FromJson(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (JsonException ex)
        {
            // Beschädigte Datei aufheben statt sie stillschweigend zu verwerfen.
            ErrorLog.Write($"state.json nicht lesbar: {ex.Message}");
            File.Move(path, path + ".corrupt", overwrite: true);
            return null;
        }
    }

    public void Save(RunState? state)
    {
        try
        {
            if (state is null)
            {
                File.Delete(path);
                return;
            }

            string temp = path + ".tmp";
            File.WriteAllText(temp, state.ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write($"state.json nicht geschrieben: {ex.Message}");
        }
    }
}
