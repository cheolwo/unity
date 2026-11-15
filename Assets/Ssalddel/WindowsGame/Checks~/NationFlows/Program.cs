using System;
using Ssalddel.WindowsGame;

// dotnet run --project Checks~/NationFlows
internal static class Program
{
    private static int Main()
    {
        var results = 전국흐름표현Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"Hongdal nation flow checks: {results.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
