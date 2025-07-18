using System.Security.Cryptography;
using System.Text;
namespace Chat;

public class RsaEncryption
{
    private RSA Rsa { get; } = RSA.Create(2048);

    public string GetPublicKey()
    {
        return Convert.ToBase64String(Rsa.ExportRSAPublicKey());
    }

    public void LoadPublicKey(string base64Key)
    {
        var publicKey = Convert.FromBase64String(base64Key);
        Rsa.ImportRSAPublicKey(publicKey, out _);
    }

    public byte[] Encrypt(byte[] data)
    {
        return Rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
    }

    public byte[] Decrypt(byte[] data)
    {
        return Rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
    }
}