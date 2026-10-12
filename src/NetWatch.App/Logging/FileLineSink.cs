using System.Text;
using NetWatch.Core.Logging;

namespace NetWatch.App.Logging;

/// <summary>
/// Hängt Zeilen an eine CSV-Datei an (UTF-8 mit BOM, Kopfzeile beim Anlegen).
/// Ist die Datei gesperrt, z. B. weil sie in Excel offen ist, wird nichts geschrieben und <c>false</c> geliefert.
/// </summary>
internal sealed class FileLineSink(string path, string header) : ILineSink
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public bool TryAppend(IReadOnlyList<string> lines)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            bool isNew = stream.Length == 0;

            // StreamWriter schreibt die BOM nur, wenn er am Dateianfang beginnt, also nur bei einer neuen Datei.
            using var writer = new StreamWriter(stream, Utf8WithBom);
            var text = new StringBuilder();
            if (isNew)
            {
                text.AppendLine(header);
            }

            foreach (string line in lines)
            {
                text.AppendLine(line);
            }

            writer.Write(text.ToString());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
