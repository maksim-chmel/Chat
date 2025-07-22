using System.Net.Sockets;
using System.Text;

namespace Chat;

public class ChatService
{
    private readonly ConsoleHelper consoleHelper =  new ConsoleHelper();
        public async Task ChatLoop(NetworkStream stream, AesEncryption aes)
        {
            var cts = new CancellationTokenSource();
            var receiverCompleted = new TaskCompletionSource();

            _ = StartReceiverLoop(stream, aes, cts, receiverCompleted);

            await HandleUserInputAsync(stream, aes, cts);

            await receiverCompleted.Task;
            consoleHelper.WriteColor("🔚 You left the chat\n", ConsoleColor.Cyan);
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
                            Console.WriteLine();
                            consoleHelper.WriteColor("[Peer disconnected]", ConsoleColor.Yellow);
                            break;
                        }

                        string encrypted = Encoding.UTF8.GetString(buffer, 0, len);
                        string decrypted = aes.Decrypt(encrypted);

                        if (decrypted == "__exit__")
                        {
                            Console.WriteLine();
                            consoleHelper.WriteColor("[Peer exited the chat]", ConsoleColor.Yellow);
                            cts.Cancel();
                            break;
                        }

                        PrintMessageFromPeer(decrypted);
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal termination
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    consoleHelper.WriteColor($"[Error receiving messages: {ex.Message}]", ConsoleColor.Red);
                }
                finally
                {
                    receiverCompleted.SetResult();
                }
            });
        }

        private async Task HandleUserInputAsync(NetworkStream stream, AesEncryption aes, CancellationTokenSource cts)
        {
            while (true)
            {
                PrintPrompt();

                string? msg = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(msg)) continue;

                if (msg.Equals("/q", StringComparison.OrdinalIgnoreCase))
                {
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
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(encrypted));
                }
                catch (Exception ex)
                {
                    consoleHelper.WriteColor($"❌ Send error: {ex.Message}", ConsoleColor.Red);
                    cts.Cancel();
                    break;
                }
            }
        }

        private void PrintMessageFromPeer(string decrypted)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[Friend]: ");
            Console.ResetColor();
            Console.WriteLine(decrypted);
        }

        private void PrintPrompt()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[You]: ");
            Console.ResetColor();
        }

        private async Task SendExitMessageAsync(NetworkStream stream, AesEncryption aes)
        {
            try
            {
                string exitMsg = aes.Encrypt("__exit__");
                await stream.WriteAsync(Encoding.UTF8.GetBytes(exitMsg));
            }
            catch
            {
                // ignored
            }
        }

       
    }

