using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia
{
    public class ImagerStartup
    {
        public static Process? ImagerProcess;

        public static async Task WaitForImagerStartup(
            string executable,
            string expectedText,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            var workDir = workingDirectory ?? AppContext.BaseDirectory;
            var fullExePath = Path.IsPathRooted(executable)
                ? executable
                : Path.Combine(workDir, executable);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fullExePath,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };
            process.Start();
            ImagerProcess = process;

            var startupSignal = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Register cancellation so a caller-supplied token can still abort the wait.
            using var ctr = cancellationToken.Register(() =>
                startupSignal.TrySetCanceled(cancellationToken));

            // stderr: unchanged, already a fire-and-forget background task.
            _ = Task.Run(async () =>
            {
                try
                {
                    while (await process.StandardError.ReadLineAsync() is { } errorLine)
                    {
                        Console.Error.WriteLine(errorLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"stderr reader stopped: {ex.Message}");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    while (await process.StandardOutput.ReadLineAsync() is { } line)
                    {
                        Console.WriteLine(line);

                        if (!startupSignal.Task.IsCompleted &&
                            line.Contains(expectedText, StringComparison.Ordinal))
                        {
                            startupSignal.TrySetResult();
                        }
                    }

                    startupSignal.TrySetException(
                        new InvalidOperationException(
                            $"Process exited before output '{expectedText}' was received."));
                }
                catch (Exception ex)
                {
                    startupSignal.TrySetException(ex);
                }
            });

       
            await startupSignal.Task;
        }
    }
}