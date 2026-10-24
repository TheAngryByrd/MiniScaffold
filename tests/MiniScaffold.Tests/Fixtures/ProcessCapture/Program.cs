using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

static class Program
{
    static readonly string ParentOutput = "parent-stdout\r\nline\rbare\n\U0001F680-tail";
    static readonly string ParentError = "parent-stderr\r\nline\rbare\n\u03a9-tail";
    static readonly string ExactOutput = new string('o', 8191) + "\U0001F680\r\nstdout\rtail\nno-newline";
    static readonly string ExactError = new string('e', 4095) + "\U0001F680\r\nstderr\rtail\nno-newline";

    static void Publish(string directory, string name, string value)
    {
        var destination = Path.Combine(directory, name);
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, value);
        File.Move(temporary, destination);
    }

    static bool WaitForFile(string path)
    {
        var elapsed = Stopwatch.StartNew();
        while (!File.Exists(path) && elapsed.Elapsed < TimeSpan.FromSeconds(60))
            Thread.Sleep(10);
        return File.Exists(path);
    }

    static int Main(string[] args)
    {
        if (args[0] == "descendant")
        {
            using (var output = Console.OpenStandardOutput())
            using (var error = Console.OpenStandardError())
            {
                Publish(args[1], "ready", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                WaitForFile(Path.Combine(args[1], "release"));
            }
            Publish(args[1], "complete", "completed naturally");
            Thread.Sleep(250);
            return 0;
        }

        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        using var stderr = new StreamWriter(Console.OpenStandardError(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };

        if (args[0] == "exact" || args[0] == "running")
        {
            stdout.Write(ExactOutput);
            stderr.Write(ExactError);
            if (args[0] == "running")
            {
                Publish(args[1], "running-ready", "ready");
                WaitForFile(Path.Combine(args[1], "release"));
            }
            return 0;
        }

        if (args[0] == "faulting")
        {
            stdout.Write(ParentOutput);
            stderr.Write(ParentError);
            Publish(args[1], "fault-ready", "ready");
            WaitForFile(Path.Combine(args[1], "release"));
            using var output = Console.OpenStandardOutput();
            output.WriteByte(0xff);
            return 19;
        }

        if (args[0] == "parent")
        {
            var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("descendant");
            start.ArgumentList.Add(args[1]);
            using var descendant = Process.Start(start);
            if (!WaitForFile(Path.Combine(args[1], "ready")))
                return 99;
            stdout.Write(ParentOutput);
            stderr.Write(ParentError);
            Publish(args[1], "parent-exiting", "parent exiting");
            return 23;
        }

        Thread.Sleep(int.Parse(args[1]));
        stdout.WriteLine("fixture-stdout");
        stderr.WriteLine("fixture-stderr");
        return int.Parse(args[2]);
    }
}
