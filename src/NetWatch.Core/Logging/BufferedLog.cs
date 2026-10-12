namespace NetWatch.Core.Logging;

/// <summary>
/// Ziel, an das Zeilen angehängt werden (in der App: eine CSV-Datei).
/// </summary>
public interface ILineSink
{
    /// <summary>
    /// Hängt alle Zeilen an. Liefert <c>false</c>, wenn das gerade nicht geht (z. B. Datei in Excel geöffnet);
    /// dann darf keine der Zeilen geschrieben worden sein.
    /// </summary>
    bool TryAppend(IReadOnlyList<string> lines);
}

/// <summary>
/// Puffert Zeilen, solange das Ziel gesperrt ist, und schreibt sie später in der ursprünglichen Reihenfolge.
/// </summary>
public sealed class BufferedLog(ILineSink sink)
{
    private readonly List<string> _pending = [];

    /// <summary>Zeilen, die noch nicht geschrieben werden konnten.</summary>
    public IReadOnlyList<string> Pending => _pending;

    public void Add(string line)
    {
        _pending.Add(line);
        Flush();
    }

    /// <summary>Versucht, alle gepufferten Zeilen zu schreiben.</summary>
    /// <returns><c>true</c>, wenn danach nichts mehr gepuffert ist.</returns>
    public bool Flush()
    {
        if (_pending.Count == 0)
        {
            return true;
        }

        if (!sink.TryAppend(_pending))
        {
            return false;
        }

        _pending.Clear();
        return true;
    }
}
