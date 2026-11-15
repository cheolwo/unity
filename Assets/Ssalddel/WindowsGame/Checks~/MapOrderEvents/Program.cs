using System;
using Ssalddel.WindowsGame;

// dotnet run --project Checks~/MapOrderEvents
internal static class Program
{
    private static int Main()
    {
        var results = 지도새주문표현Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"Hongdal map order event checks: {results.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
