using System;

namespace CameraAnalyzer.bl.Utils
{

    public static class Logger
    {

        public static int CurrentLogLevel { get; set; } = 1;
        public static void LogError(string message)
        {
            if (CurrentLogLevel < 0) return;
            Console.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[ERROR]");
            Console.ResetColor();
            Console.WriteLine($" {message}");
        }
        public static void LogInfo(string message)
        {

            if (CurrentLogLevel < 1) return;
            Console.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[INFO]");
            Console.ResetColor();
            Console.WriteLine($" {message}");
        }


        public static void LogWarning(string message)
        {
            if (CurrentLogLevel < 2) return;
            Console.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[WARNING]");
            Console.ResetColor();
            Console.WriteLine($" {message}");
        }

        public static void LogDebug(string message)
        {
            if (CurrentLogLevel < 3) return;
            Console.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[DEBUG]");
            Console.ResetColor();
            Console.WriteLine($" {message}");
        }
    }
}
