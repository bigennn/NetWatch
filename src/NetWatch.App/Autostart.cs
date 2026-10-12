using Microsoft.Win32;

namespace NetWatch.App;

/// <summary>
/// Autostart über den Run-Schlüssel des angemeldeten Benutzers (keine Adminrechte nötig).
/// Gespeichert wird der Pfad der laufenden .exe; wird sie verschoben, muss der Autostart neu eingeschaltet werden.
/// </summary>
/// <remarks>
/// Windows führt einen Run-Eintrag aus, solange er in der Autostart-Freigabeliste (<c>StartupApproved\Run</c>,
/// dieselbe Liste wie im Task-Manager unter "Autostart-Apps") nicht als deaktiviert markiert ist. Beim
/// Einschalten markiert NetWatch sich dort ausdrücklich als aktiviert, damit es auch nach einer Deaktivierung
/// im Task-Manager wieder startet; das Häkchen zeigt eine solche Deaktivierung als "aus" an.
/// </remarks>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "NetWatch";

    // Format der Freigabeliste: 12 Bytes; erstes Byte gerade (02) = aktiviert, ungerade (03) = im Task-Manager
    // deaktiviert, danach der Zeitpunkt der Deaktivierung. Aktiviert schreibt der Task-Manager 02 und Nullen.
    private static readonly byte[] ApprovedEnabled = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// <c>true</c>, wenn der Autostart auf genau diese .exe zeigt und nicht im Task-Manager deaktiviert ist.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey);
            bool registered = run?.GetValue(ValueName) is string value
                && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);

            using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            bool disabledInTaskManager = approved?.GetValue(ValueName) is byte[] { Length: > 0 } flags && (flags[0] & 1) == 1;

            return registered && !disabledInTaskManager;
        }
    }

    public static void Enable()
    {
        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            run.SetValue(ValueName, Command);
        }

        using RegistryKey approved = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        approved.SetValue(ValueName, ApprovedEnabled, RegistryValueKind.Binary);
    }

    public static void Disable()
    {
        using (RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
