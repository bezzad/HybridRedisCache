// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Attributes;
using HybridRedisCache.Benchmark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public class Program
{
    static BenchmarkManager Manager = new() { RepeatCount = 1000 };

    private static async Task Main()
    {
        Console.Title = "Redis vs. Mem cache benchmark";

#if !DEBUG
        BenchmarkDotNet.Running.BenchmarkRunner.Run<BenchmarkManager>();
#else
        var timesOfExecutions = new Dictionary<string, double>();
        var sw = new System.Diagnostics.Stopwatch();
        Manager.GlobalSetup();

        Console.WriteLine($"Repeating each test {Manager.RepeatCount} times");
        Console.WriteLine("\n");
        PrintHeader();

        var methods = typeof(BenchmarkManager).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            if (method.GetCustomAttribute(typeof(BenchmarkAttribute)) != null)
            {
                sw.Restart();
                if (method.ReturnType == typeof(Task))
                {
                    await (Task)method.Invoke(Manager, null);
                }
                else
                {
                    method.Invoke(Manager, null);
                }
                sw.Stop();
                timesOfExecutions.Add(method.Name, sw.ElapsedMilliseconds);
                PrintBenchmark(method.Name, sw.ElapsedMilliseconds);
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
        foreach (var method in methodDurations.OrderBy(m => m.Key.Substring(0,3)).ThenBy(m => m.Value))
        {
            PrintBenchmark(method.Key, method.Value);
        }
        PrintLine();
    }

    private static void PrintHeader()
    {
        PrintLine();
        var headerDesc = "|".PadRight(18) + "Test Method Name".PadRight(32) + "|  Duration (milisecond)".PadRight(24) + "  |";
        Console.WriteLine(headerDesc);
    }

    private static void PrintBenchmark(string method, double durMs)
    {
        PrintLine();
        var leftDesc = $"| {method} ".PadRight(50);
        var rightDesc = $"|   {durMs:N0}ms ".PadRight(25);
        Console.WriteLine(leftDesc + rightDesc + $" |");
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