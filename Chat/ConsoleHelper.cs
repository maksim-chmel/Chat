using Spectre.Console;
using System.Threading;

namespace Chat;

public class ConsoleHelper
{
    public async Task ShowCountdown(int seconds, CancellationToken token)
    {
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                
                new RemainingTimeColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Waiting...[/]", maxValue: seconds);

                for (int elapsed = 0; elapsed <= seconds && !token.IsCancellationRequested; elapsed++)
                {
                    int remaining = seconds - elapsed;
                    task.Description = $"[green]⏳ Countdown:[/]";
                    task.Value = elapsed; 

                    if (remaining == 0)
                    {
                        AnsiConsole.MarkupLine("[bold green]✅ Countdown finished![/]");
                        break;
                    }

                    try
                    {
                        await Task.Delay(1000, token);
                    }
                    catch (TaskCanceledException)
                    {
                        AnsiConsole.MarkupLine("[bold yellow]⚠️ Countdown cancelled.[/]");
                        break;
                    }
                }
            });
    }
    
    

    public void WriteColor(string text, ConsoleColor color)
    {
        var ansiColor = color switch
        {
            ConsoleColor.Black => Color.Black,
            ConsoleColor.Blue => Color.Blue,
            ConsoleColor.Cyan => Color.Cyan1,
            ConsoleColor.DarkBlue => Color.DarkBlue,
            ConsoleColor.DarkCyan => Color.Teal,
            ConsoleColor.DarkGray => Color.Grey,
            ConsoleColor.DarkGreen => Color.Green,
            ConsoleColor.DarkMagenta => Color.Purple,
            ConsoleColor.DarkRed => Color.Red,
            ConsoleColor.DarkYellow => Color.Orange3,
            ConsoleColor.Gray => Color.Silver,
            ConsoleColor.Green => Color.GreenYellow,
            ConsoleColor.Magenta => Color.Magenta1,
            ConsoleColor.Red => Color.Red1,
            ConsoleColor.White => Color.White,
            ConsoleColor.Yellow => Color.Yellow,
            _ => Color.Default,
        };
        AnsiConsole.MarkupLine($"[{ansiColor}]{text}[/]");
    }

    
    public void PrintHelp()
    {
        var table = new Table().Border(TableBorder.Rounded).Title("📖 [yellow]Chat Commands[/]");

        table.AddColumn("[blue]Command[/]");
        table.AddColumn("[green]Description[/]");
        table.AddRow("/help", "Show this help message");
        table.AddRow("/clear", "Clear the screen");
        table.AddRow("/q", "Quit the chat");

        AnsiConsole.Write(table);
    }
    public async Task ShowStartupAnimation()
    {
        AnsiConsole.MarkupLine("[bold green]🔐 Secure P2P Chat Started![/]");
        await Task.Delay(700);
        AnsiConsole.MarkupLine("[grey]Loading components...[/]");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Initializing secure environment...", async ctx =>
            {
                await Task.Delay(500);
                ctx.Status("Setting up encryption...");
                await Task.Delay(800);
                ctx.Status("Preparing UI...");
                await Task.Delay(600);
                ctx.Status("Finalizing...");
                await Task.Delay(500);
            });

        AnsiConsole.MarkupLine("[bold green]✅ Ready![/]");
    }
    public async Task ShowConnectingAnimation()
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBar)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Connecting to peer...", async ctx =>
            {
                await Task.Delay(400);
                ctx.Status("Handshaking...");
                await Task.Delay(500);
                ctx.Status("Exchanging keys...");
                await Task.Delay(600);
                ctx.Status("Secure channel established.");
                await Task.Delay(300);
            });

        AnsiConsole.MarkupLine("[bold green]🔗 Connected successfully![/]");
    }
    public async Task ShowDisconnectingAnimation()
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("red"))
            .StartAsync("Disconnecting...", async ctx =>
            {
                ctx.Status("Closing session...");
                await Task.Delay(600);
                ctx.Status("Cleaning up resources...");
                await Task.Delay(500);
                ctx.Status("Goodbye.");
                await Task.Delay(400);
            });

        AnsiConsole.MarkupLine("[bold red]❌ Disconnected[/]");
    }
   public void ShowGoodbyeAndExit()
    {
        AnsiConsole.MarkupLine("[bold red]👋 Exiting... See you soon![/]");
    
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Line)
            .SpinnerStyle(Style.Parse("red"))
            .Start("Finishing up...", ctx =>
            {
                ctx.Status("Saving data...");
                Thread.Sleep(800);
                ctx.Status("Releasing resources...");
                Thread.Sleep(800);
                ctx.Status("Shutting down...");
                Thread.Sleep(800);
            });

        AnsiConsole.MarkupLine("[bold grey]Application closed successfully.[/]");
        Thread.Sleep(600);

        Environment.Exit(0); 
    }
}