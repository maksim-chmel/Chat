using System.Security.Cryptography;

namespace Chat;

internal class RsaEncryption
{
    private RSA rsa;

    public RsaEncryption()
    {
        rsa = RSA.Create(2048);
    }

    public string GetPublicKey()
    {
        return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    public void LoadPublicKey(string base64PublicKey)
    {
        var keyBytes = Convert.FromBase64String(base64PublicKey);
        rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
    }

    public byte[] Encrypt(byte[] data)
    {
        return rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
    }

    public byte[] Decrypt(byte[] data)
    {
        return rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);
    }
}