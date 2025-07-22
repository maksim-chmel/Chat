using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;

namespace Chat;

public class SecureChannel
{
    private readonly RsaEncryption rsaEncryption = new RsaEncryption();
    public async Task<AesEncryption> InitializeAsClientAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int len = await stream.ReadAsync(buffer);
        string publicKey = Encoding.UTF8.GetString(buffer, 0, len).Trim();

        var rsa = new RsaEncryption();
        rsa.LoadPublicKey(publicKey);

        using var aesAlg = Aes.Create();
        byte[] combined = aesAlg.Key.Concat(aesAlg.IV).ToArray();
        byte[] encrypted = rsa.Encrypt(combined);

        await stream.WriteAsync(encrypted);

        return new AesEncryption(aesAlg.Key, aesAlg.IV);
    }
    public async Task<AesEncryption> InitializeAsHostAsync(NetworkStream stream)
    {
        string publicKey = rsaEncryption.GetPublicKey();
        byte[] publicKeyBytes = Encoding.UTF8.GetBytes(publicKey + "\n");
        await stream.WriteAsync(publicKeyBytes);

        byte[] buffer = new byte[512];
        int len = await stream.ReadAsync(buffer);
        byte[] encryptedKeyIv = buffer[..len];
        byte[] decrypted = rsaEncryption.Decrypt(encryptedKeyIv);
        byte[] key = decrypted[..32];
        byte[] iv = decrypted[32..];

        return new AesEncryption(key, iv);
    }
}