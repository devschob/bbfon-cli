namespace BBFon.Services;

public static class ConsoleLog
{
    private static readonly object Lock = new();

    public static void Info(string msg)    => Write(msg, ConsoleColor.Gray);
    public static void Success(string msg) => Write(msg, ConsoleColor.Green);
    public static void Warning(string msg) => Write(msg, ConsoleColor.Yellow);
    public static void Error(string msg)   => Write(msg, ConsoleColor.Red);
    public static void Alarm(string msg)   => Write(msg, ConsoleColor.Red);
    public static void Debug(string msg)   => Write(msg, ConsoleColor.Cyan);

    // Für rollende \r-Zeile in AudioMonitorService
    public static void Inline(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        lock (Lock)
        {
            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ResetColor();
        }
    }

    private static void Write(string msg, ConsoleColor color)
    {
        lock (Lock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }
}
