using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Chat
{
    internal abstract class Program
    {
        private const int Port = 9000;

        private static async Task Main(string[] args)
        {
            Console.Title = "Консольный чат";

            PrintWelcomeMessage();

            var localIp = GetLocalIPAddress();
            WriteColoredLine($"Ваш локальный IP: {localIp}", ConsoleColor.Green);
            Console.WriteLine("Если вы запускаете на этом же компьютере, используйте 127.0.0.1");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Выберите действие: (h)остить, (c)оннектиться, (q)уит: ");
                Console.ResetColor();

                var choice = Console.ReadLine()?.Trim().ToLower();

                if (choice == "h")
                    await StartHost();
                else if (choice == "c")
                    await ConnectToHost();
                else if (choice == "q")
                {
                    WriteColoredLine("Выход из программы. До свидания!", ConsoleColor.Yellow);
                    break;
                }
                else
                {
                    WriteColoredLine("Неверный выбор. Пожалуйста, введите 'h', 'c' или 'q'.", ConsoleColor.Red);
                }

                Console.WriteLine();
            }
        }

        private static void PrintWelcomeMessage()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Добро пожаловать в консольный чат!");
            Console.WriteLine("Чтобы выйти из программы, введите 'q' в главном меню.");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip;
                }
            }
            return IPAddress.Loopback;
        }

        private static async Task StartHost()
        {
            var rsa = new RsaEncryption();
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            int timeoutSeconds = 30;
            var cts = new CancellationTokenSource();

            WriteColoredLine(
                $"Режим хоста. Ожидаем подключения клиента (таймаут {timeoutSeconds} сек)...",
                ConsoleColor.Yellow);

            var countdownTask = ShowCountdown(timeoutSeconds, cts.Token);

            try
            {
                var acceptTask = listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

                var completedTask = await Task.WhenAny(acceptTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine();
                    WriteColoredLine("❌ Время ожидания истекло. Клиент не подключился.", ConsoleColor.Red);
                    return;
                }

                cts.Cancel();

                using var client = await acceptTask;
                Console.WriteLine();
                WriteColoredLine("✅ Клиент успешно подключился!", ConsoleColor.Green);

                await using var stream = client.GetStream();

                // Отправляем публичный RSA ключ
                var publicKey = rsa.GetPublicKey();
                var publicKeyBytes = Encoding.UTF8.GetBytes(publicKey + "\n");
                await stream.WriteAsync(publicKeyBytes);

                // Получаем зашифрованный AES ключ и IV
                byte[] buffer = new byte[512];
                int len = await stream.ReadAsync(buffer);
                var encryptedKeyIv = buffer.Take(len).ToArray();

                var decryptedKeyIv = rsa.Decrypt(encryptedKeyIv);
                var key = decryptedKeyIv.Take(32).ToArray();
                var iv = decryptedKeyIv.Skip(32).Take(16).ToArray();

                var aes = new AesEncryption(key, iv);

                WriteColoredLine("Чат запущен! Введите /help для списка команд.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                WriteColoredLine($"Ошибка: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                listener.Stop();
                cts.Dispose();
            }
        }

        private static async Task ShowCountdown(int seconds, CancellationToken token)
        {
            for (int i = seconds; i > 0; i--)
            {
                if (token.IsCancellationRequested)
                    break;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\rОжидание подключения: {i} сек...   ");
                Console.ResetColor();

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            Console.WriteLine();
        }

        private static async Task ConnectToHost()
        {
            IPAddress ip;
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Введите IP адрес хоста (например, 127.0.0.1): ");
                Console.ResetColor();

                var ipInput = Console.ReadLine()?.Trim();

                if (IPAddress.TryParse(ipInput, out ip))
                    break;

                WriteColoredLine("Ошибка: Введён некорректный IP адрес. Попробуйте снова.", ConsoleColor.Red);
            }

            try
            {
                using var client = new TcpClient();
                WriteColoredLine($"Подключаемся к {ip}:{Port}...", ConsoleColor.Yellow);
                await client.ConnectAsync(ip, Port);
                Console.WriteLine();
                WriteColoredLine("✅ Подключено!", ConsoleColor.Green);

                await using var stream = client.GetStream();

                // Получаем публичный RSA ключ от хоста
                byte[] buffer = new byte[1024];
                int len = await stream.ReadAsync(buffer);
                string publicKey = Encoding.UTF8.GetString(buffer, 0, len).Trim();

                var rsa = new RsaEncryption();
                rsa.LoadPublicKey(publicKey);

                // Генерируем AES ключ и IV
                using var aesAlg = Aes.Create();
                var key = aesAlg.Key;
                var iv = aesAlg.IV;

                // Шифруем AES ключ и IV с помощью RSA публичного ключа хоста
                var combined = key.Concat(iv).ToArray();
                var encryptedKeyIv = rsa.Encrypt(combined);
                await stream.WriteAsync(encryptedKeyIv);

                var aes = new AesEncryption(key, iv);

                WriteColoredLine("Чат запущен! Введите /help для списка команд.", ConsoleColor.Cyan);
                await ChatLoop(stream, aes);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                WriteColoredLine($"Ошибка подключения: {e.Message}", ConsoleColor.Red);
            }
        }

        private static async Task ChatLoop(NetworkStream stream, AesEncryption aes)
        {
            bool isRunning = true;

            var receiveTask = Task.Run(async () =>
            {
                byte[] buffer = new byte[2048];
                while (isRunning)
                {
                    try
                    {
                        int len = await stream.ReadAsync(buffer);
                        if (len == 0)
                        {
                            Console.WriteLine();
                            WriteColoredLine("[Собеседник отключился]", ConsoleColor.Yellow);
                            isRunning = false;
                            break;
                        }

                        string encrypted = Encoding.UTF8.GetString(buffer, 0, len);
                        string decrypted = aes.Decrypt(encrypted);

                        if (decrypted == "__exit__")
                        {
                            Console.WriteLine();
                            WriteColoredLine("[Собеседник завершил чат] /q - выход в меню", ConsoleColor.Yellow);
                            isRunning = false;
                            break;
                        }

                        Console.WriteLine();
                        WriteColored($"[Друг]: ", ConsoleColor.Green);
                        Console.WriteLine(decrypted);

                        if (isRunning)
                            PrintInputPrompt();
                    }
                    catch
                    {
                        if (isRunning)
                        {
                            Console.WriteLine();
                            WriteColoredLine("[Ошибка или собеседник отключился] /q - выход в меню", ConsoleColor.Red);
                        }
                        isRunning = false;
                        break;
                    }
                }
            });

            while (isRunning)
            {
                PrintInputPrompt();
                string msg = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(msg))
                    continue;

                if (msg.Equals("/q", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string encryptedExit = aes.Encrypt("__exit__");
                        byte[] exitData = Encoding.UTF8.GetBytes(encryptedExit);
                        await stream.WriteAsync(exitData);
                        stream.Close(); // Закрываем поток, чтобы собеседник вышел
                    }
                    catch (Exception ex)
                    {
                        WriteColoredLine($"Ошибка при отправке команды выхода: {ex.Message}", ConsoleColor.Red);
                    }
                    isRunning = false;
                    break;
                }
                else if (msg.Equals("/help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Доступные команды чата:");
                    Console.WriteLine("  /q     - Выйти из чата");
                    Console.WriteLine("  /help  - Показать эту справку");
                    Console.WriteLine("  /clear - Очистить экран");
                }
                else if (msg.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                }
                else
                {
                    try
                    {
                        string encrypted = aes.Encrypt(msg);
                        byte[] data = Encoding.UTF8.GetBytes(encrypted);
                        await stream.WriteAsync(data);
                    }
                    catch (Exception ex)
                    {
                        WriteColoredLine($"Ошибка при отправке сообщения: {ex.Message}", ConsoleColor.Red);
                        isRunning = false;
                        break;
                    }
                }
            }

            await receiveTask;

            Console.WriteLine();
            WriteColoredLine("Вы вышли из чата. Возврат в меню...\n", ConsoleColor.Cyan);
        }

        private static void PrintInputPrompt()
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[Вы]: ");
            Console.ForegroundColor = oldColor;
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = oldColor;
        }

        private static void WriteColoredLine(string text, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = oldColor;
        }
    }
} 