using System.Net;
using System.Security.Cryptography;

namespace Chat
{
    internal static class Program
    {
        private const int Port = 9000;
        private static NetworkHelper networkHelper = new NetworkHelper();
        private static ConsoleHelper consoleHelper = new ConsoleHelper();
        private static ChatHost chatHost = new ChatHost();
        private static ChatClient chatClient = new ChatClient();
        public static async Task Main()
        {
            string localIp = networkHelper.GetLocalIp();
            while (true)
            {
                consoleHelper.GetStartMessage(localIp);
                string? choice = Console.ReadLine()?.Trim().ToLower();

                switch (choice)
                {
                    case "h":
                        await chatHost.StartHost(Port);
                        break;
                    case "c":
                        await chatClient.ConnectToHost(Port);
                        break;
                    case "q":
                        consoleHelper.WriteColor("Exiting the program. See you!", ConsoleColor.Yellow);
                        return;
                    default:
                        consoleHelper.WriteColor("❌ Invalid choice. Enter 'h', 'c' or 'q'.", ConsoleColor.Red);
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}