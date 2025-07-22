namespace Chat;

public class ConsoleHelper
{
    private static Task<ConsoleColor> ChangeConsoleColor(int timeoutSeconds)
    {
        ConsoleColor color;

        switch (timeoutSeconds)
        {
            case > 20:
                color = ConsoleColor.Green;
                break;
            case 20 or > 10:
                color = ConsoleColor.Yellow;
                break;
            default:
                color = ConsoleColor.Red;
                break;
        }

        return Task.FromResult(color);
    }
    public void WriteColor(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
    public async Task ShowCountdown(int seconds, CancellationToken token)
    {
        const int barLength = 30;

        for (int i = 0; i <= seconds; i++)
        {
            if (token.IsCancellationRequested) break;

            int fill = (int)((i / (float)seconds) * barLength);
            string bar = new string('█', fill) + new string('░', barLength - fill);
            Console.ForegroundColor = await ChangeConsoleColor(seconds-i);
            Console.Write($"\r[{bar}] {seconds - i} sec remaining  ");
            Console.ResetColor();
            try { await Task.Delay(1000, token); }
            catch (TaskCanceledException) { break; }
        }
        Console.WriteLine();
    }

    public void GetStartMessage(string localIp)
    {
        Console.Clear();
        Console.Title = "🔐 Anonymous P2P Chat";
        WriteColor($"Your local IP: {localIp}", ConsoleColor.Cyan);
        WriteColor("Choose action: (h)ost, (c)onnect, (q)uit: ", ConsoleColor.Cyan);
    }
    public void PrintHelp()
    {
        Console.WriteLine(" /q     - Quit");
        Console.WriteLine(" /help  - Help");
        Console.WriteLine(" /clear - Clear screen");
    }
}