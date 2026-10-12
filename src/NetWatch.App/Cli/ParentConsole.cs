using System.Runtime.InteropServices;

namespace NetWatch.App.Cli;

/// <summary>
/// Eine WinForms-App hat kein eigenes Konsolenfenster. Für Befehlszeilen-Modi hängt sie sich an das
/// Konsolenfenster des aufrufenden Terminals, damit <see cref="System.Console"/>-Ausgaben dort erscheinen.
/// </summary>
internal static class ParentConsole
{
    private const int AttachParentProcess = -1;

    public static void Attach() => AttachConsole(AttachParentProcess);

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
