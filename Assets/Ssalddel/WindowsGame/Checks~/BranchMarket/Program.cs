using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Ssalddel.WindowsGame.Kernel;

// dotnet run --project Checks~/BranchMarket                      → 검사
// dotnet run --project Checks~/BranchMarket -- --sim <판.json> <결과 폴더> [회차=30]   → 모의(share.csv · news.txt)
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--sim") return Sim(args[1], args[2], args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 30);
        var results = 지사경쟁Checks.Run();
        var failed = 0;
        foreach (var (name, ok, detail) in results)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }
        Console.WriteLine($"Hongdal branch market checks: {results.Count - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    internal static 지사경쟁판 Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var root = doc.RootElement;
        var rules = new 지사경쟁Rules();
        if (root.TryGetProperty("rules", out var r))
            foreach (var p in r.EnumerateObject())
            {
                var field = typeof(지사경쟁Rules).GetField(p.Name);
                if (field == null) throw new InvalidDataException("모르는 규칙 이름: " + p.Name);
                if (field.FieldType == typeof(double)) field.SetValue(rules, p.Value.GetDouble());
                else if (field.FieldType == typeof(long)) field.SetValue(rules, p.Value.GetInt64());
                else if (field.FieldType == typeof(int)) field.SetValue(rules, p.Value.GetInt32());
            }
        var dongs = new List<경쟁동>();
        foreach (var d in root.GetProperty("dongs").EnumerateArray())
        {
            var dong = new 경쟁동 { Code = d.GetProperty("code").GetString(), 이름 = d.GetProperty("name").GetString(),
                회차주문 = d.GetProperty("orders").GetDouble(), 음식점수 = d.GetProperty("restaurants").GetInt32() };
            foreach (var n in d.GetProperty("neighbors").EnumerateArray()) dong.이웃.Add(n.GetString());
            dongs.Add(dong);
        }
        var ops = new List<경쟁운영자>();
        var starts = new List<(string op, string dong, int riders, int partners)>();
        foreach (var o in root.GetProperty("operators").EnumerateArray())
        {
            var op = new 경쟁운영자 { Id = o.GetProperty("id").GetString(), 이름 = o.GetProperty("name").GetString(),
                전략 = (경쟁전략)Enum.Parse(typeof(경쟁전략), o.GetProperty("strategy").GetString()),
                자본 = o.GetProperty("funds").GetInt64(), 배달비점수 = o.GetProperty("feeScore").GetDouble() };
            ops.Add(op);
            foreach (var s in o.GetProperty("start").EnumerateArray())
                starts.Add((op.Id, s.GetProperty("dong").GetString(), s.GetProperty("riders").GetInt32(), s.GetProperty("partners").GetInt32()));
        }
        var board = 지사경쟁Policy.새판(rules, dongs, ops, root.TryGetProperty("seed", out var seed) ? seed.GetUInt64() : 1UL);
        foreach (var s in starts)
            if (지사경쟁Policy.처음자리(board, s.op, s.dong, s.riders, s.partners) == null) throw new InvalidDataException("처음 자리를 놓지 못함: " + s.op + " " + s.dong);
        return board;
    }

    private static int Sim(string scenario, string outDir, int turns)
    {
        var board = Load(scenario);
        Directory.CreateDirectory(outDir);
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("turn,operator,name,strategy,funds,research,branches,orders,served,missed,guShare\n");
        var seats = new StringBuilder("turn,operator,dong,share,riders,partners,trust,served,missed\n");
        var total = board.동들.Sum(d => d.회차주문);
        for (var t = 0; t < turns; t++)
        {
            지사경쟁Policy.회차넘기기(board);
            foreach (var o in board.운영자들)
            {
                var mine = board.운영자자리(o.Id).ToList();
                csv.Append(board.회차 - 1).Append(',').Append(o.Id).Append(',').Append(o.이름).Append(',').Append(o.전략).Append(',').Append(o.자본).Append(',')
                    .Append(o.연구점수).Append(',').Append(mine.Count).Append(',').Append(mine.Sum(s => s.받을주문).ToString("0.0", inv)).Append(',')
                    .Append(mine.Sum(s => s.받은주문).ToString("0.0", inv)).Append(',').Append(mine.Sum(s => s.놓친주문).ToString("0.0", inv)).Append(',')
                    .Append((mine.Sum(s => s.받은주문) / total).ToString("0.0000", inv)).Append('\n');
            }
            foreach (var s in board.자리들)
                seats.Append(board.회차 - 1).Append(',').Append(s.운영자).Append(',').Append(s.동).Append(',').Append(s.몫.ToString("0.0000", inv)).Append(',')
                    .Append(s.기사).Append(',').Append(s.입점).Append(',').Append(s.신뢰.ToString("0.000", inv)).Append(',')
                    .Append(s.받은주문.ToString("0.0", inv)).Append(',').Append(s.놓친주문.ToString("0.0", inv)).Append('\n');
        }
        File.WriteAllText(Path.Combine(outDir, "share.csv"), csv.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, "seats.csv"), seats.ToString(), new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(outDir, "news.txt"), board.소식.Select(n => n.회차 + "\t" + board.운영자(n.운영자).이름 + "\t" + n.글), new UTF8Encoding(false));
        Console.WriteLine("sim " + turns + " turns · fingerprint " + 지사경쟁Policy.지문(board) + " · news " + board.소식.Count);
        foreach (var o in board.운영자들)
            Console.WriteLine(o.이름 + " " + o.전략 + " 자본 " + o.자본 + " 지사 " + 지사경쟁Policy.지사수(board, o.Id) + " 구 몫 " +
                (board.운영자자리(o.Id).Sum(s => s.받은주문) / total * 100).ToString("0.0", inv) + "%");
        return 0;
    }
}
