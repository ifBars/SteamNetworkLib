using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace SteamNetworkLib.Tests.Integration
{
    /// <summary>
    /// Multi-process integration tests for P2P messaging using Goldberg Steam Emulator.
    /// Each test spawns separate host and client worker processes to test true P2P communication.
    /// </summary>
    [Collection("Multi-Process P2P Tests")]
    public class P2PMultiProcessTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private string? _testDir;
        private bool _disposed;

        public P2PMultiProcessTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TextMessage_Event_HostReceivesFromClient()
        {
            // Arrange
            _output.WriteLine("=== Test: TextMessage OnP2PMessageReceived event ===");
            
            var testId = Guid.NewGuid().ToString("N");
            var (hostDir, clientDir, sharedDir) = SetupTestDirectories(testId);

            var hostSteamId = 76561197960265728UL;
            var clientSteamId = 76561197960265729UL;

            // Act
            var (hostExitCode, clientExitCode) = await RunHostAndClientAsync(
                hostDir, clientDir, sharedDir,
                hostSteamId, clientSteamId,
                "text_message_event",
                47600, 47601);

            // Assert
            hostExitCode.Should().Be(0, "host should exit successfully");
            clientExitCode.Should().Be(0, "client should exit successfully");
            
            _output.WriteLine("✓ Test passed: TextMessage event triggered correctly");
        }

        [Fact]
        public async Task TextMessage_RegisteredHandler_HostReceivesFromClient()
        {
            // Arrange
            _output.WriteLine("=== Test: TextMessage registered handler ===");
            
            var testId = Guid.NewGuid().ToString("N");
            var (hostDir, clientDir, sharedDir) = SetupTestDirectories(testId);

            var hostSteamId = 76561197960265730UL;
            var clientSteamId = 76561197960265731UL;

            // Act
            var (hostExitCode, clientExitCode) = await RunHostAndClientAsync(
                hostDir, clientDir, sharedDir,
                hostSteamId, clientSteamId,
                "text_message_handler",
                47602, 47603);

            // Assert
            hostExitCode.Should().Be(0, "host should exit successfully");
            clientExitCode.Should().Be(0, "client should exit successfully");
            
            _output.WriteLine("✓ Test passed: TextMessage handler was called correctly");
        }

        [Fact]
        public async Task CustomMessage_DroppedByHost_KnownLimitation()
        {
            // Arrange
            _output.WriteLine("=== Test: Custom message dropped (demonstrates current limitation) ===");
            
            var testId = Guid.NewGuid().ToString("N");
            var (hostDir, clientDir, sharedDir) = SetupTestDirectories(testId);

            var hostSteamId = 76561197960265732UL;
            var clientSteamId = 76561197960265733UL;

            // Act
            var (hostExitCode, clientExitCode) = await RunHostAndClientAsync(
                hostDir, clientDir, sharedDir,
                hostSteamId, clientSteamId,
                "custom_message_dropped",
                47604, 47605);

            // Assert
            hostExitCode.Should().Be(0, "host should confirm message was NOT received (as expected)");
            clientExitCode.Should().Be(0, "client should send message successfully");
            
            _output.WriteLine("✓ Test passed: Confirmed custom message type limitation");
            _output.WriteLine("  Note: Custom messages are sent but not received due to hardcoded switch in ProcessSteamNetworkLibMessage");
        }

        private (string hostDir, string clientDir, string sharedDir) SetupTestDirectories(string testId)
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SteamNetworkLib.P2P", testId);
            
            var hostDir = Path.Combine(_testDir, "host");
            var clientDir = Path.Combine(_testDir, "client");
            var sharedDir = Path.Combine(_testDir, "shared");

            Directory.CreateDirectory(hostDir);
            Directory.CreateDirectory(clientDir);
            Directory.CreateDirectory(sharedDir);

            _output.WriteLine($"Test directories:");
            _output.WriteLine($"  Host:   {hostDir}");
            _output.WriteLine($"  Client: {clientDir}");
            _output.WriteLine($"  Shared: {sharedDir}");

            // Copy worker executable and dependencies to both directories
            var workerOutputDir = GetWorkerOutputDirectory();
            CopyWorkerFiles(workerOutputDir, hostDir);
            CopyWorkerFiles(workerOutputDir, clientDir);

            return (hostDir, clientDir, sharedDir);
        }

        private string GetWorkerOutputDirectory()
        {
            // Find the P2P worker output directory
            // Current directory is typically: tests/SteamNetworkLib.Tests/bin/Mono/net6.0
            // Worker is at: tests/SteamNetworkLib.P2PWorker/bin/Mono/net6.0
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var testsDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
            var workerDir = Path.Combine(testsDir, "SteamNetworkLib.P2PWorker", "bin", "Mono", "net6.0");
            
            if (!Directory.Exists(workerDir))
            {
                throw new DirectoryNotFoundException($"Worker output directory not found: {workerDir}. Current dir: {currentDir}, Tests dir: {testsDir}");
            }

            return workerDir;
        }

        private void CopyWorkerFiles(string sourceDir, string targetDir)
        {
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, overwrite: true);
            }

            _output.WriteLine($"Copied worker files to {targetDir}");
        }

        private async Task<(int hostExitCode, int clientExitCode)> RunHostAndClientAsync(
            string hostDir,
            string clientDir,
            string sharedDir,
            ulong hostSteamId,
            ulong clientSteamId,
            string testCase,
            int hostPort,
            int clientPort)
        {
            var hostExeFile = Path.Combine(hostDir, "SteamNetworkLib.P2PWorker.exe");
            var clientExeFile = Path.Combine(clientDir, "SteamNetworkLib.P2PWorker.exe");

            // Start host process
            _output.WriteLine($"Starting host process...");
            var hostProcess = StartWorkerProcess(
                hostExeFile, hostDir,
                "host", hostSteamId, "TestHost", hostPort, sharedDir, testCase);

            // Give host time to initialize and create lobby (15 seconds for Goldberg to stabilize)
            _output.WriteLine($"Waiting 15 seconds for host to initialize and lobby to stabilize...");
            await Task.Delay(15000);

            // Start client process
            _output.WriteLine($"Starting client process...");
            var clientProcess = StartWorkerProcess(
                clientExeFile, clientDir,
                "client", clientSteamId, "TestClient", clientPort, sharedDir, testCase);

            // Wait for both processes with timeout (increased to account for 15s delay + test execution)
            var timeout = TimeSpan.FromSeconds(60);
            var hostExited = await WaitForProcessAsync(hostProcess, timeout);
            var clientExited = await WaitForProcessAsync(clientProcess, timeout);

            if (!hostExited)
            {
                _output.WriteLine("⚠ Host process timeout - killing");
                hostProcess.Kill();
            }

            if (!clientExited)
            {
                _output.WriteLine("⚠ Client process timeout - killing");
                clientProcess.Kill();
            }

            var hostExitCode = hostProcess.ExitCode;
            var clientExitCode = clientProcess.ExitCode;

            _output.WriteLine($"Host exit code: {hostExitCode}");
            _output.WriteLine($"Client exit code: {clientExitCode}");

            // Dump output if there were failures
            if (hostExitCode != 0 || clientExitCode != 0)
            {
                _output.WriteLine("=== Host Output ===");
                _output.WriteLine(await File.ReadAllTextAsync(Path.Combine(hostDir, "stdout.txt")));
                _output.WriteLine("=== Host Errors ===");
                _output.WriteLine(await File.ReadAllTextAsync(Path.Combine(hostDir, "stderr.txt")));
                
                _output.WriteLine("=== Client Output ===");
                _output.WriteLine(await File.ReadAllTextAsync(Path.Combine(clientDir, "stdout.txt")));
                _output.WriteLine("=== Client Errors ===");
                _output.WriteLine(await File.ReadAllTextAsync(Path.Combine(clientDir, "stderr.txt")));
            }

            return (hostExitCode, clientExitCode);
        }

        private Process StartWorkerProcess(
            string exePath,
            string workingDir,
            string role,
            ulong steamId,
            string name,
            int listenPort,
            string sharedDir,
            string testCase)
        {
            var args = $"--role {role} --steamId {steamId} --name {name} --listenPort {listenPort} --sharedDir \"{sharedDir}\" --testCase {testCase}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            
            // Capture output to files
            var stdoutFile = Path.Combine(workingDir, "stdout.txt");
            var stderrFile = Path.Combine(workingDir, "stderr.txt");
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    File.AppendAllText(stdoutFile, e.Data + Environment.NewLine);
                    _output.WriteLine($"[{role}] {e.Data}");
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    File.AppendAllText(stderrFile, e.Data + Environment.NewLine);
                    _output.WriteLine($"[{role}] ERROR: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _output.WriteLine($"Started {role} process (PID: {process.Id})");
            return process;
        }

        private async Task<bool> WaitForProcessAsync(Process process, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            process.Exited += (sender, args) => tcs.TrySetResult(true);
            process.EnableRaisingEvents = true;

            if (process.HasExited)
            {
                return true;
            }

            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            return completedTask == tcs.Task;
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_testDir != null && Directory.Exists(_testDir))
                {
                    // Attempt to clean up test directories
                    // (may fail if processes are still running - that's okay)
                    try
                    {
                        Directory.Delete(_testDir, recursive: true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Collection definition to ensure multi-process tests don't run in parallel.
    /// </summary>
    [CollectionDefinition("Multi-Process P2P Tests", DisableParallelization = true)]
    public class P2PMultiProcessCollection
    {
    }
}
