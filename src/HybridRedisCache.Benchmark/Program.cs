// See https://aka.ms/new-console-template for more information

using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace HybridRedisCache.Benchmark;

public class Program
{
    private static readonly BenchmarkManager Manager = new() { RepeatCount = 1000 };

    private static async Task Main()
    {
        Console.Title = "Redis vs. Mem cache benchmark";
#if !DEBUG
        await Task.Yield();
        BenchmarkDotNet.Running.BenchmarkRunner.Run<BenchmarkManager>();
#else
        var timesOfExecutions = new Dictionary<string, double>();
        var sw = new System.Diagnostics.Stopwatch();
        Manager.GlobalSetup();
        Manager.RepeatCount = 10000;

        Console.WriteLine($"Repeating each test {Manager.RepeatCount} times");
        Console.WriteLine("\n");
        PrintHeader();

        var methods = typeof(BenchmarkManager).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            if (method?.GetCustomAttribute(typeof(BenchmarkAttribute)) != null)
            {
                sw.Restart();
                PrintBenchmarkMethod(method.Name);
                if (method.Invoke(Manager, null) is Task task)
                    await task;
                sw.Stop();
                timesOfExecutions.Add(method!.Name, sw.ElapsedMilliseconds);
                PrintBenchmarkValue(sw.ElapsedMilliseconds);
            }
        }

        PrintSortedResult(timesOfExecutions);
#endif
        Console.ReadLine();
    }


    public static void ClearConsole()
    {
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = false;
        for (int y = 0; y < Console.BufferHeight; y++)
            Console.Write(new String(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = true;
    }


    private static void PrintSortedResult(Dictionary<string, double> methodDurations)
    {
        ClearHost();
        PrintHeader();
        foreach (var method in methodDurations.OrderBy(m => m.Key.Substring(0, 3)).ThenBy(m => m.Value))
        {
            PrintBenchmark(method.Key, method.Value);
        }

        PrintLine();
    }

    private static void PrintHeader()
    {
        PrintLine();
        var headerDesc = "|".PadRight(18) + "Test Method Name".PadRight(32) + "|  Duration (milisecond)".PadRight(24) +
                         "  |";
        Console.WriteLine(headerDesc);
    }

    private static void PrintBenchmark(string method, double durMs)
    {
        PrintBenchmarkMethod(method);
        PrintBenchmarkValue(durMs);
    }

    private static void PrintBenchmarkMethod(string method)
    {
        PrintLine();
        var leftDesc = $"| {method} ".PadRight(50);
        Console.Write(leftDesc);
    }

    private static void PrintBenchmarkValue(double durMs)
    {
        var rightDesc = $"|   {durMs:N0}ms ".PadRight(25);
        Console.WriteLine(rightDesc + $" |");
    }

    private static void PrintLine()
    {
        Console.WriteLine(" " + new string('-', 75));
    }

    public static void ClearHost()
    {
        Console.Write("\f\u001bc\x1b[3J");
    }
}