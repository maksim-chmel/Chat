using System.Net.Sockets;
using System.Text;
using Spectre.Console;

namespace Chat
{
    public class ChatService
    {
        private readonly ConsoleHelper consoleHelper = new ConsoleHelper();
        public async Task ChatLoop(NetworkStream stream, AesEncryption aes)
        {
            var cts = new CancellationTokenSource();
            var receiverCompleted = new TaskCompletionSource();

            _ = StartReceiverLoop(stream, aes, cts, receiverCompleted);

            try
            {
                await HandleUserInputAsync(stream, aes, cts);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]❌ Fatal error in chat loop: {ex.Message}[/]");
            }

            await receiverCompleted.Task;

            AnsiConsole.Write(new Panel("[cyan]🔚 You left the chat[/]")
                .Border(BoxBorder.Double)
                .BorderStyle(new Style(Color.Cyan1)));
        }

        private async Task StartReceiverLoop(NetworkStream stream, AesEncryption aes, CancellationTokenSource cts,
            TaskCompletionSource receiverCompleted)
        {
            await Task.Run(async () =>
            {
                byte[] buffer = new byte[2048];

                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        int len = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                        if (len == 0)
                        {
                            AnsiConsole.MarkupLine("\n[yellow]⚠️ Peer disconnected[/]");
                            cts.Cancel();
                            break;
                        }

                        string encrypted;
                        try
                        {
                            encrypted = Encoding.UTF8.GetString(buffer, 0, len);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]❌ Error decoding message: {ex.Message}[/]");
                            continue;
                        }

                        string decrypted;
                        try
                        {
                            decrypted = aes.Decrypt(encrypted);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]❌ Decryption failed: {ex.Message}[/]");
                            continue;
                        }

                        if (decrypted == "__exit__")
                        {
                            AnsiConsole.MarkupLine("\n[yellow]👋 Peer exited the chat[/]");
                            cts.Cancel();
                            break;
                        }

                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine($"[green]Friend:[/] {decrypted}");
                        PrintPrompt();
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal shutdown
                }
                catch (ObjectDisposedException)
                {
                    AnsiConsole.MarkupLine("[red]❌ Stream was disposed[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]❌ Unexpected receive error: {ex.Message}[/]");
                }
                finally
                {
                    receiverCompleted.TrySetResult();
                }
            });
        }

        private async Task HandleUserInputAsync(NetworkStream stream, AesEncryption aes, CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                PrintPrompt();

                string? msg = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(msg))
                    continue;

                if (msg.Equals("/q", StringComparison.OrdinalIgnoreCase))
                {
                    await consoleHelper.ShowDisconnectingAnimation();
                    await SendExitMessageAsync(stream, aes);
                    cts.Cancel();
                    break;
                }

                if (msg.Equals("/help", StringComparison.OrdinalIgnoreCase))
                {
                    consoleHelper.PrintHelp();
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
                    byte[] data = Encoding.UTF8.GetBytes(encrypted);
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch (ObjectDisposedException)
                {
                    AnsiConsole.MarkupLine("[red]❌ Cannot send: stream has been closed[/]");
                    cts.Cancel();
                    break;
                }
                catch (IOException ex)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]❌ Network error while sending: {ex.Message}[/]");
                    cts.Cancel();
                    break;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]❌ Unexpected send error: {ex.Message}[/]");
                    cts.Cancel();
                    break;
                }
            }
        }

        private void PrintPrompt()
        {
            AnsiConsole.Markup("[green]You: [/]");
        }

        

        private async Task SendExitMessageAsync(NetworkStream stream, AesEncryption aes)
        {
            try
            {
                string exitMsg = aes.Encrypt("__exit__");
                byte[] data = Encoding.UTF8.GetBytes(exitMsg);
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch
            {
                // Fail silently on disconnect
            }
        }
    }
}