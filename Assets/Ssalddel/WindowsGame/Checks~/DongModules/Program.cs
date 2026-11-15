using System;
using Ssalddel.WindowsGame;

// dotnet run --project Checks~/DongModules
internal static class Program
{
    private static int Main()
    {
        var results = 행정동모듈Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"Hongdal dong module checks: {results.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
