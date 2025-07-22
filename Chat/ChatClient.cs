using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;

namespace Chat;

public class ChatClient
{
    private readonly ConsoleHelper consoleHelper =  new ConsoleHelper();
    private readonly ChatService chatService =  new ChatService();
    private readonly SecureChannel secureChannel =  new SecureChannel();
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
            using var client = new TcpClient();
            consoleHelper.WriteColor($"🔌 Connecting to {ip}:{port}...", ConsoleColor.Yellow);
            await client.ConnectAsync(ip, port);

            Console.WriteLine();
            consoleHelper.WriteColor("✅ Connected successfully!", ConsoleColor.Green);
            await using var stream = client.GetStream();
                
            var aes = await secureChannel.InitializeAsClientAsync(stream);

            consoleHelper.WriteColor("💬 Chat started! Type /help for commands.", ConsoleColor.Cyan);
            await chatService.ChatLoop(stream, aes);
        }
        catch (Exception e)
        {
            Console.WriteLine();
            consoleHelper.WriteColor($"Connection error: {e.Message}", ConsoleColor.Red);
        }
    }
}