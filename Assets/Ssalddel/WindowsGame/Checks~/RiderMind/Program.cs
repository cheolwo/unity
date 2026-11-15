using System;
using Ssalddel.WindowsGame.Kernel;

// dotnet run --project Checks~/RiderMind
internal static class Program
{
    private static int Main()
    {
        var results = 기사무념Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"Hongdal rider mind checks: {results.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
