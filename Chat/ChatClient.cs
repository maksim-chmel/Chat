using System.Net;
using System.Net.Sockets;

namespace Chat;

public class ChatClient
{
    private readonly ConsoleHelper consoleHelper = new ConsoleHelper();
    private readonly ChatService chatService = new ChatService();
    private readonly SecureChannel secureChannel = new SecureChannel();

    public async Task ConnectToHost(int port)
    {
        consoleHelper.WriteColor("Enter host IP (e.g., 127.0.0.1): ", ConsoleColor.Cyan);
        if (!IPAddress.TryParse(Console.ReadLine(), out IPAddress? ip))
        {
            consoleHelper.WriteColor("❌ Invalid IP address.", ConsoleColor.Red);
            return;
        }

        try
        {
            var cts = new CancellationTokenSource();
            consoleHelper.WriteColor($"🔌 Connecting to {ip}:{port}...", ConsoleColor.Yellow);

            var client = await ConnectWithRetryAsync(ip, port, cts);

            if (client != null)
            {
                await consoleHelper.ShowConnectingAnimation();
                await using var stream = client.GetStream();

                var aes = await secureChannel.InitializeAsClientAsync(stream);

                consoleHelper.WriteColor("💬 Chat started! Type /help for commands.", ConsoleColor.Cyan);
                await chatService.ChatLoop(stream, aes);
            }
            else
            {
                Console.WriteLine();
                consoleHelper.WriteColor("❌ Timeout exceeded.", ConsoleColor.Red);
                await Task.Delay(1000);
                Console.Clear();
            }
        }
        catch (Exception e)
        {
            consoleHelper.WriteColor($"\nConnection error: {e.Message}", ConsoleColor.Red);
        }
    }

    private async Task<TcpClient?> ConnectWithRetryAsync(IPAddress serverIp, int port, CancellationTokenSource cts)
    {
        int timeoutSeconds = Program.timeoutSeconds;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var countdownTask = consoleHelper.ShowCountdown(timeoutSeconds, cts.Token);

        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds && !cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = new TcpClient();
                var connectTask = client.ConnectAsync(serverIp, port);
                
                var remainingTime = TimeSpan.FromSeconds(timeoutSeconds) - stopwatch.Elapsed;
                if (remainingTime <= TimeSpan.Zero)
                {
                    client.Dispose();
                    break;
                }

                var timeoutTask = Task.Delay(remainingTime, cts.Token);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask)
                {
                    if (client.Connected)
                    {
                        cts.Cancel(); 
                        await countdownTask;
                        return client;
                    }
                    client.Dispose();
                }
                else
                {
                    client.Dispose();
                    break;
                }
            }
            catch (Exception)
            {
                await Task.Delay(500, cts.Token);
            }
        }
        cts.Cancel();
        await countdownTask;
        return null;
    }
}