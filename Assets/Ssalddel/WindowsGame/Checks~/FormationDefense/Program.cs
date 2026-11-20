using System;
using System.IO;
using System.Text;
using Ssalddel.WindowsGame.Kernel;

// dotnet run --project Checks~/FormationDefense -- --result <file>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--result" || string.IsNullOrWhiteSpace(args[1])))
        {
            Console.Error.WriteLine("사용법: FormationDefenseChecks [--result <결과 파일>]");
            return 2;
        }

        var results = 진형방어Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            var status = ok ? "[PASS] " : "[FAIL] ";
            Console.WriteLine(status + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"옥상 진형 방어: {results.Count - failed}건 통과, {failed}건 실패");
        if (failed != 0) return 1;

        if (args.Length == 2)
            File.WriteAllText(args[1], 진형방어Checks.RepresentativeResult(), new UTF8Encoding(false));
        return 0;
    }
}
