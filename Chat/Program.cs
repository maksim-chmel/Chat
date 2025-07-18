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
            Console.Title = "🔐 Анонимный P2P Чат";
            PrintWelcome();

            string localIp = GetLocalIp();
            WriteColor($"Ваш локальный IP: {localIp}", ConsoleColor.Green);
            Console.WriteLine("Если вы запускаете на этом же компьютере, используйте 127.0.0.1\n");

            while (true)
            {
                WriteColor("Выберите действие: (h)остить, (c)оннектиться, (q)уит: ", ConsoleColor.Cyan);
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
                        WriteColor("Выход из программы. До встречи!", ConsoleColor.Yellow);
                        return;
                    default:
                        WriteColor("❌ Неверный выбор. Введите 'h', 'c' или 'q'.", ConsoleColor.Red);
                        break;
                }

                Console.WriteLine();
            }
        }

        private static void PrintWelcome()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Добро пожаловать в 🔐 Анонимный P2P Чат!");
            Console.WriteLine("Введите 'q' в главном меню, чтобы выйти.");
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

            WriteColor($"Хост запущен. Ожидаем подключение (таймаут {timeoutSeconds} сек)...", ConsoleColor.Yellow);
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
                    WriteColor("❌ Время ожидания истекло.", ConsoleColor.Red);
                    return;
                }

                using var client = await acceptTask;
                Console.WriteLine();
                WriteColor("✅ Клиент подключился!", ConsoleColor.Green);
                await using var stream = client.GetStream();

                // Передача RSA-ключа
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

                WriteColor("💬 Чат начался! Введите /help для команд.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                WriteColor($"Ошибка: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task ConnectToHost()
        {
            WriteColor("Введите IP хоста (например, 127.0.0.1): ", ConsoleColor.Cyan);
            if (!IPAddress.TryParse(Console.ReadLine(), out IPAddress? ip))
            {
                WriteColor("❌ Некорректный IP адрес.", ConsoleColor.Red);
                return;
            }

            try
            {
                using var client = new TcpClient();
                WriteColor($"🔌 Подключение к {ip}:{Port}...", ConsoleColor.Yellow);
                await client.ConnectAsync(ip, Port);

                Console.WriteLine();
                WriteColor("✅ Успешно подключено!", ConsoleColor.Green);
                await using var stream = client.GetStream();

                // Получение RSA-ключа
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

                WriteColor("💬 Чат начался! Введите /help для команд.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                WriteColor($"Ошибка подключения: {e.Message}", ConsoleColor.Red);
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
                    WriteColor("[Собеседник отключился]", ConsoleColor.Yellow);
                    break;
                }

                string encrypted = Encoding.UTF8.GetString(buffer, 0, len);
                string decrypted = aes.Decrypt(encrypted);

                if (decrypted == "__exit__")
                {
                    Console.WriteLine();
                    WriteColor("[Собеседник завершил чат]", ConsoleColor.Yellow);
                    break;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[Друг]: ");
                Console.ResetColor();
                Console.WriteLine(decrypted);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("[Вы]: ");
                Console.ResetColor();
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение при /q
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            WriteColor($"[Ошибка при приеме сообщений: {ex.Message}]", ConsoleColor.Red);
        }
        finally
        {
            receiverCompleted.SetResult();
        }
    });

    while (isRunning)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[Вы]: ");
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

            // Останавливаем приём
            isRunning = false;
            cts.Cancel();
            break;
        }

        if (msg.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(" /q     - Выйти");
            Console.WriteLine(" /help  - Помощь");
            Console.WriteLine(" /clear - Очистить экран");
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
            WriteColor($"❌ Ошибка отправки: {ex.Message}", ConsoleColor.Red);
            isRunning = false;
            cts.Cancel();
            break;
        }
    }

    // Ждём завершения приёмника
    await receiverCompleted.Task;
    WriteColor("🔚 Вы вышли из чата\n", ConsoleColor.Cyan);
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
                Console.Write($"\r[{bar}] {seconds - i} сек осталось  ");
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