using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Chat
{
    internal static class Program
    {
        private const int Port = 9000;

        public static async Task Main()
        {
            Console.Title = "🔐 Anonymous P2P Chat";
            PrintWelcome();

            string localIp = GetLocalIp();
            WriteColor($"Your local IP: {localIp}", ConsoleColor.Green);
            Console.WriteLine("If running on the same machine, use 127.0.0.1\n");

            while (true)
            {
                WriteColor("Choose action: (h)ost, (c)onnect, (q)uit: ", ConsoleColor.Cyan);
                string? choice = Console.ReadLine()?.Trim().ToLower();

                switch (choice)
                {
                    case "h":
                        await StartHost();
                        break;
                    case "c":
                        await ConnectToHost();
                        break;
                    case "q":
                        WriteColor("Exiting the program. See you!", ConsoleColor.Yellow);
                        return;
                    default:
                        WriteColor("❌ Invalid choice. Enter 'h', 'c' or 'q'.", ConsoleColor.Red);
                        break;
                }

                Console.WriteLine();
            }
        }

        private static void PrintWelcome()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Welcome to 🔐 Anonymous P2P Chat!");
            Console.WriteLine("Type 'q' in the main menu to quit.");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static string GetLocalIp()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        private static async Task StartHost()
        {
            var rsa = new RsaEncryption();
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            const int timeoutSeconds = 30;
            var cts = new CancellationTokenSource();

            WriteColor($"Hosting started. Waiting for connection (timeout {timeoutSeconds} sec)...", ConsoleColor.Yellow);
            var countdownTask = ShowCountdown(timeoutSeconds, cts.Token);

            try
            {
                var acceptTask = listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

                var completed = await Task.WhenAny(acceptTask, timeoutTask);
                cts.Cancel();

                if (completed == timeoutTask)
                {
                    Console.WriteLine();
                    WriteColor("❌ Timeout exceeded.", ConsoleColor.Red);
                    return;
                }

                using var client = await acceptTask;
                Console.WriteLine();
                WriteColor("✅ Client connected!", ConsoleColor.Green);
                await using var stream = client.GetStream();

                // Send RSA key
                string publicKey = rsa.GetPublicKey();
                byte[] publicKeyBytes = Encoding.UTF8.GetBytes(publicKey + "\n");
                await stream.WriteAsync(publicKeyBytes);

                byte[] buffer = new byte[512];
                int len = await stream.ReadAsync(buffer);
                byte[] encryptedKeyIv = buffer[..len];
                byte[] decrypted = rsa.Decrypt(encryptedKeyIv);
                byte[] key = decrypted[..32];
                byte[] iv = decrypted[32..];

                var aes = new AesEncryption(key, iv);

                WriteColor("💬 Chat started! Type /help for commands.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                WriteColor($"Error: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task ConnectToHost()
        {
            WriteColor("Enter host IP (e.g., 127.0.0.1): ", ConsoleColor.Cyan);
            if (!IPAddress.TryParse(Console.ReadLine(), out IPAddress? ip))
            {
                WriteColor("❌ Invalid IP address.", ConsoleColor.Red);
                return;
            }

            try
            {
                using var client = new TcpClient();
                WriteColor($"🔌 Connecting to {ip}:{Port}...", ConsoleColor.Yellow);
                await client.ConnectAsync(ip, Port);

                Console.WriteLine();
                WriteColor("✅ Connected successfully!", ConsoleColor.Green);
                await using var stream = client.GetStream();

                // Receive RSA key
                byte[] buffer = new byte[1024];
                int len = await stream.ReadAsync(buffer);
                string publicKey = Encoding.UTF8.GetString(buffer, 0, len).Trim();

                var rsa = new RsaEncryption();
                rsa.LoadPublicKey(publicKey);

                using var aesAlg = Aes.Create();
                byte[] combined = aesAlg.Key.Concat(aesAlg.IV).ToArray();
                byte[] encrypted = rsa.Encrypt(combined);

                await stream.WriteAsync(encrypted);

                var aes = new AesEncryption(aesAlg.Key, aesAlg.IV);

                WriteColor("💬 Chat started! Type /help for commands.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                WriteColor($"Connection error: {e.Message}", ConsoleColor.Red);
            }
        }

        private static async Task ChatLoop(NetworkStream stream, AesEncryption aes)
        {
            bool isRunning = true;
            var cts = new CancellationTokenSource();
            var receiverCompleted = new TaskCompletionSource();

            var receiver = Task.Run(async () =>
            {
                byte[] buffer = new byte[2048];
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        int len = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                        if (len == 0)
                        {
                            Console.WriteLine();
                            WriteColor("[Peer disconnected]", ConsoleColor.Yellow);
                            break;
                        }

                        string encrypted = Encoding.UTF8.GetString(buffer, 0, len);
                        string decrypted = aes.Decrypt(encrypted);

                        if (decrypted == "__exit__")
                        {
                            Console.WriteLine();
                            WriteColor("[Peer exited the chat]", ConsoleColor.Yellow);
                            cts.Cancel();
                            break;
                        }

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("[Friend]: ");
                        Console.ResetColor();
                        Console.WriteLine(decrypted);

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("[You]: ");
                        Console.ResetColor();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal termination
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    WriteColor($"[Error receiving messages: {ex.Message}]", ConsoleColor.Red);
                }
                finally
                {
                    receiverCompleted.SetResult();
                }
            });

            while (isRunning)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("[You]: ");
                Console.ResetColor();

                string? msg = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(msg)) continue;

                if (msg.Equals("/q", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string exitMsg = aes.Encrypt("__exit__");
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(exitMsg));
                    }
                    catch { }

                    isRunning = false;
                    cts.Cancel();
                    break;
                }

                if (msg.Equals("/help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(" /q     - Quit");
                    Console.WriteLine(" /help  - Help");
                    Console.WriteLine(" /clear - Clear screen");
                    continue;
                }

                if (msg.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                    continue;
                }

                try
                {
                    string encrypted = aes.Encrypt(msg);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(encrypted));
                }
                catch (Exception ex)
                {
                    WriteColor($"❌ Send error: {ex.Message}", ConsoleColor.Red);
                    isRunning = false;
                    cts.Cancel();
                    break;
                }
            }

            await receiverCompleted.Task;
            WriteColor("🔚 You left the chat\n", ConsoleColor.Cyan);
        }

        private static async Task ShowCountdown(int seconds, CancellationToken token)
        {
            const int barLength = 30;

            for (int i = 0; i <= seconds; i++)
            {
                if (token.IsCancellationRequested) break;

                int fill = (int)((i / (float)seconds) * barLength);
                string bar = new string('█', fill) + new string('░', barLength - fill);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write($"\r[{bar}] {seconds - i} sec remaining  ");
                Console.ResetColor();

                try { await Task.Delay(1000, token); }
                catch (TaskCanceledException) { break; }
            }
            Console.WriteLine();
        }

        private static void WriteColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}