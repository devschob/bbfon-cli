using System.Runtime.InteropServices;

namespace BBFon.Services;

public sealed class SleepPreventionService : IDisposable
{
    private const uint ES_CONTINUOUS      = 0x80000000u;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001u;

    public SleepPreventionService()
    {
        uint result = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        if (result == 0)
            ConsoleLog.Warning("[BBFon] Schlafmodus konnte nicht deaktiviert werden (SetThreadExecutionState fehlgeschlagen).");
        else
            ConsoleLog.Info("[BBFon] Schlafmodus deaktiviert – System bleibt wach.");
    }

    public void Dispose()
    {
        SetThreadExecutionState(ES_CONTINUOUS);
        ConsoleLog.Info("[BBFon] Schlafmodus wieder aktiviert.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);
}
