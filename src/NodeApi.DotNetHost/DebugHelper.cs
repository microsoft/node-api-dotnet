// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi;

// Only checking the environment variable for debugging.
#pragma warning disable RS1035 // The symbol 'Environment' is banned for use by analyzers.

internal class DebugHelper
{
    [Conditional("DEBUG")]
    public static void AttachDebugger(string environmentVariableName)
    {
        string? debugValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.Equals(debugValue, "VS", StringComparison.OrdinalIgnoreCase))
        {
            // Launch the Visual Studio debugger.
            Debugger.Launch();
        }
        else if (!string.IsNullOrEmpty(debugValue))
        {
            Process currentProcess = Process.GetCurrentProcess();
            string processName = currentProcess.ProcessName;
            int processId = currentProcess.Id;
            Console.WriteLine("###################### DEBUG ######################");

            int waitSeconds = 20;
            string waitingMessage = string.Empty;
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(
                    $"Process \"{processName}\" ({processId}) is " +
                    $"waiting {waitSeconds} seconds for debugger.");
            }
            else
            {
                Console.WriteLine(
                    $"Process \"{processName}\" ({processId}) is waiting for debugger.");
                waitingMessage = "Press any key to continue without debugging... ";
                Console.Write(waitingMessage + $"({waitSeconds})");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int remainingSeconds = waitSeconds;
            while (!Debugger.IsAttached)
            {
                if (!Console.IsOutputRedirected && Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                    Console.WriteLine();
                    return;
                }
                else if (stopwatch.Elapsed > TimeSpan.FromSeconds(waitSeconds))
                {
                    Console.WriteLine(
                        $"Debugger did not attach after {waitSeconds} seconds. Continuing.");
                    return;
                }

                Thread.Sleep(100);

                if (remainingSeconds > waitSeconds - (int)stopwatch.Elapsed.TotalSeconds)
                {
                    remainingSeconds = waitSeconds - (int)stopwatch.Elapsed.TotalSeconds;

                    if (!Console.IsOutputRedirected)
                    {
                        Console.CursorLeft = waitingMessage.Length;
                        Console.Write($"({remainingSeconds:D2})");
                    }
                }
            }

            if (!Console.IsOutputRedirected)
            {
                Console.CursorLeft = waitingMessage.Length;
                Console.WriteLine("    ");
            }

            Debugger.Break();
        }
    }
}
