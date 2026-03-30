using BenchmarkDotNet.Running;
using System;
using System.IO;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var localTemp = Path.Combine(AppContext.BaseDirectory, "bench-temp");
            Directory.CreateDirectory(localTemp);
            Environment.SetEnvironmentVariable("TMP", localTemp);
            Environment.SetEnvironmentVariable("TEMP", localTemp);

            var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
