using System.Net;
using System.Net.Sockets;

namespace Chat;

public class ChatHost
{
    private readonly ConsoleHelper consoleHelper =  new ConsoleHelper();
    private readonly ChatService chatService =  new ChatService();
    private readonly SecureChannel secureChannel =  new SecureChannel();
    
     public async Task StartHost(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            var cts = new CancellationTokenSource();

            consoleHelper.WriteColor($"Hosting started. Waiting for connection...", ConsoleColor.Cyan);
            
            try
            {
              var client =  await WaitForClientWithTimeoutAsync(listener, cts);

              if (client != null)
              {
                  Console.WriteLine();
                  consoleHelper.WriteColor("✅ Client connected!", ConsoleColor.Green);
                  await using var stream = client.GetStream();

                  var aes =  await secureChannel.InitializeAsHostAsync(stream);
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
            catch (Exception ex)
            {
                Console.WriteLine();
                consoleHelper.WriteColor($"Error: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                listener.Stop();
            }
        }
        private async Task<TcpClient?> WaitForClientWithTimeoutAsync(TcpListener listener, CancellationTokenSource token)
        {
            var countdownTask = consoleHelper.ShowCountdown(Program.timeoutSeconds, token.Token);
            var acceptTask = listener.AcceptTcpClientAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(Program.timeoutSeconds));

            var completed = await Task.WhenAny(acceptTask, timeoutTask);
            token.Cancel();
            await countdownTask;
            return completed == acceptTask ? acceptTask.Result : null;
        }
}
