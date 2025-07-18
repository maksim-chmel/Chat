using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Chat
{

    internal abstract class Program
    {
        private static async Task Main(string[] args)
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Вы хотите (h)остить, (c)оннектиться или (q)уит? ");
                Console.ResetColor();

                var choice = Console.ReadLine();

                if (choice == "h")
                    await StartHost();
                else if (choice == "c")
                    await ConnectToHost();
                else if (choice == "q")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Выход из программы...");
                    Console.ResetColor();
                    break;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неверный выбор.");
                    Console.ResetColor();
                }
            }
        }

        private static async Task StartHost()
        {
            var rsa = new RsaEncryption();
            var listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();

            int timeoutSeconds = 30;

            var cts = new CancellationTokenSource();
            var countdownTask = ShowCountdown(timeoutSeconds, cts.Token);

            try
            {
                var acceptTask = listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

                var completedTask = await Task.WhenAny(acceptTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("\n");
                    WriteColoredLine("❌ Время ожидания истекло. Клиент не подключился.", ConsoleColor.Red);
                    return;
                }

                await cts.CancelAsync();

                using var client = await acceptTask;
                Console.WriteLine("\n");
                WriteColoredLine("✅ Клиент подключился.", ConsoleColor.Green);

                await using var stream = client.GetStream();

                // Отправка публичного RSA ключа
                var publicKey = rsa.GetPublicKey();
                var publicKeyBytes = Encoding.UTF8.GetBytes(publicKey + "\n");
                await stream.WriteAsync(publicKeyBytes);

                // Прием зашифрованного AES ключа и IV
                byte[] buffer = new byte[512];
                int len = await stream.ReadAsync(buffer);
                var encryptedKeyIv = buffer.Take(len).ToArray();

                var decryptedKeyIv = rsa.Decrypt(encryptedKeyIv);
                var key = decryptedKeyIv.Take(32).ToArray();
                var iv = decryptedKeyIv.Skip(32).Take(16).ToArray();

                var aes = new AesEncryption(key, iv);

                await ChatLoop(stream, aes);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n");
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

                await Task.Delay(1000, token);
            }

            Console.WriteLine();
        }

        private static async Task ConnectToHost()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Введите IP собеседника: ");
            Console.ResetColor();

            var ip = Console.ReadLine();

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(ip), 9000);
                Console.WriteLine();
                WriteColoredLine("Подключено!", ConsoleColor.Green);

                await using var stream = client.GetStream();

                // Получаем RSA публичный ключ
                byte[] buffer = new byte[1024];
                int len = await stream.ReadAsync(buffer);
                string publicKey = Encoding.UTF8.GetString(buffer, 0, len).Trim();

                var rsa = new RsaEncryption();
                rsa.LoadPublicKey(publicKey);

                // Генерация AES ключа и IV
                using var aesAlg = Aes.Create();
                var key = aesAlg.Key;
                var iv = aesAlg.IV;

                // Шифруем AES-ключ и IV
                var combined = key.Concat(iv).ToArray();
                var encryptedKeyIv = rsa.Encrypt(combined);
                await stream.WriteAsync(encryptedKeyIv);

                var aes = new AesEncryption(key, iv);
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

                        // Переводим на новую строку, чтобы не портить ввод
                        Console.WriteLine();

                        // Пишем сообщение собеседника
                        WriteColored($"[Друг]: ", ConsoleColor.Green);
                        Console.WriteLine(decrypted);

                        // Восстанавливаем приглашение для ввода, если чат ещё открыт
                        if (isRunning)
                            PrintInputPrompt();

                    }
                    catch
                    {
                        Console.WriteLine();
                        WriteColoredLine("[Ошибка или собеседник отключился] /q - выход в меню", ConsoleColor.Red);
                        isRunning = false;
                        break;
                    }
                }
            });

            while (isRunning)
            {
                PrintInputPrompt();
                string msg = Console.ReadLine();

                if (msg == "/q")
                {
                    string encryptedExit = aes.Encrypt("__exit__");
                    byte[] exitData = Encoding.UTF8.GetBytes(encryptedExit);
                    await stream.WriteAsync(exitData);
                    break;
                }

                // Отправляем зашифрованное сообщение, но НЕ выводим повторно с префиксом, т.к. ты его уже видел при вводе
                string encrypted = aes.Encrypt(msg);
                byte[] data = Encoding.UTF8.GetBytes(encrypted);
                await stream.WriteAsync(data);
            }

            isRunning = false;
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
    
 