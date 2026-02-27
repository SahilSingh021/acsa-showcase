using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace acsa_web.Services
{
    public static class ServerHost
    {
        private static readonly ConcurrentDictionary<int, (Process Proc, int Port, string ArgsFile)> _procs = new();

        public static async Task<int> StartAsync(
            int lobbyId,
            string workingDir,
            string argsText,
            int port,
            string lobbyName)
        {
            Directory.CreateDirectory(Path.Combine(workingDir, "config"));
            string fileSafe = SafeFileName(lobbyName);
            string argsFile = $"{fileSafe}.txt";
            string argsPath = Path.Combine(workingDir, "config", argsFile);

            await File.WriteAllTextAsync(argsPath, argsText, new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(workingDir, "bin_win32", "ac_server.exe"),
                Arguments = $"-Cconfig/{argsFile}",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var p = Process.Start(psi) ?? throw new InvalidOperationException("ac_server.exe failed to start.");

            _ = Task.Run(async () =>
            {
                var logPath = Path.Combine(workingDir, "logs", $"lobby_{lobbyId}_stdout.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                var text = await p.StandardOutput.ReadToEndAsync();
                await File.WriteAllTextAsync(logPath, text);
            });

            _ = Task.Run(async () =>
            {
                var logPath = Path.Combine(workingDir, "logs", $"lobby_{lobbyId}_stderr.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                var text = await p.StandardError.ReadToEndAsync();
                await File.WriteAllTextAsync(logPath, text);
            });

            _procs[lobbyId] = (p, port, argsFile);

            p.EnableRaisingEvents = true;
            p.Exited += (_, __) =>
            {
                Cleanup(workingDir, lobbyId);
            };

            return p.Id;
        }

        public static async Task StopAsync(string workingDir, int lobbyId)
        {
            if (!_procs.TryRemove(lobbyId, out var info)) return;

            try
            {
                if (!info.Proc.HasExited)
                {
                    info.Proc.Kill(entireProcessTree: true);
                    await info.Proc.WaitForExitAsync();
                }
            }
            catch { /* ignore */ }
            finally
            {
                TryDelete(Path.Combine(workingDir, "config", info.ArgsFile));
                // free port
                if (PortStore.PortDict.ContainsKey(info.Port)) PortStore.PortDict[info.Port] = "unused";
            }
        }

        public static bool IsProcessRunning(int? pid)
        {
            if (pid is null || pid <= 0) return false;

            try
            {
                var p = Process.GetProcessById(pid.Value);
                return !p.HasExited;
            }
            catch
            {
                return false; // process doesn't exist
            }
        }

        private static void Cleanup(string workingDir, int lobbyId)
        {
            if (_procs.TryRemove(lobbyId, out var info))
            {
                TryDelete(Path.Combine(workingDir, "config", info.ArgsFile));
                if (PortStore.PortDict.ContainsKey(info.Port)) PortStore.PortDict[info.Port] = "unused";
            }
        }

        private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

        private static string SafeFileName(string s)
        {
            var bad = Path.GetInvalidFileNameChars();
            return new string(s.Select(c => bad.Contains(c) ? '_' : c).ToArray());
        }
    }
}
