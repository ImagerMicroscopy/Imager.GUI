using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia
{
    internal class ImagerStartup
    {

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

            using var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();

            _ = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync() is { } errorLine)
                {
                    Console.Error.WriteLine(errorLine);
                }
            }, cancellationToken);

            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                Console.WriteLine(line);

                if (line.Contains(expectedText, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Process exited before output '{expectedText}' was received.");
        }
    }
}
