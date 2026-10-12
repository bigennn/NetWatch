using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using NetWatch.Core;

namespace NetWatch.App;

/// <summary>
/// Zeichnet das Tray-Symbol zur Laufzeit als farbigen Punkt, damit keine Icon-Dateien nötig sind.
/// </summary>
internal static class StatusIcon
{
    public static Icon Create(ConnectionState state)
    {
        Color color = state switch
        {
            ConnectionState.Connected => Color.FromArgb(46, 160, 67),
            ConnectionState.Outage => Color.FromArgb(218, 54, 51),
            _ => Color.FromArgb(140, 140, 140),
        };

        Size size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(color))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillEllipse(brush, 1, 1, size.Width - 3, size.Height - 3);
        }

        // GetHicon liefert ein Windows-Handle, das .NET nicht selbst freigibt.
        // Clone erzeugt eine eigene Kopie; das Original-Handle geben wir sofort frei.
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
