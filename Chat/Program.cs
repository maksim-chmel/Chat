using Spectre.Console;

namespace Chat
{
    internal static class Program
    {
        public const int timeoutSeconds = 30;

        private const int Port = 9000;
        private static NetworkHelper networkHelper = new NetworkHelper();
        private static ConsoleHelper consoleHelper = new ConsoleHelper();
        private static ChatHost chatHost = new ChatHost();
        private static ChatClient chatClient = new ChatClient();

        public static async Task Main()
        {
            await consoleHelper.ShowStartupAnimation();

            string localIp = networkHelper.GetLocalIp();

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(
                    new FigletText("P2P Chat")
                        .Centered()
                        .Color(Color.Green));
                AnsiConsole.MarkupLine($"[bold yellow]Your local IP: [green]{localIp}[/][/]");
                AnsiConsole.MarkupLine("[bold cyan]Choose mode:[/]\n");

                
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Select an option:[/]")
                        .PageSize(5)
                        .MoreChoicesText("[grey](Move up and down to reveal more)[/]")
                        .AddChoices(new[] { "Host (h)", "Client (c)", "Quit (q)" }));

                switch (choice.ToLower()[0])
                {
                    case 'h':
                        await chatHost.StartHost(Port);
                        break;

                    case 'c':
                        await chatClient.ConnectToHost(Port);
                        break;

                    case 'q':
                        consoleHelper.ShowGoodbyeAndExit();
                        return;

                    default:
                        AnsiConsole.MarkupLine("[red]❌ Invalid choice. Enter 'h', 'c' or 'q'.[/]");
                        break;
                }

                AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }

       

        
    }
}