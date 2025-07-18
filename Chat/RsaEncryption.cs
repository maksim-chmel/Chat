using System.Security.Cryptography;

namespace Chat;

internal class RsaEncryption
{
    private RSA _rsa = RSA.Create(2048);

    public string GetPublicKey()
    {
        return Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
    }

    public void LoadPublicKey(string base64PublicKey)
    {
        var keyBytes = Convert.FromBase64String(base64PublicKey);
        _rsa = RSA.Create();
        _rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
    }

    public byte[] Encrypt(byte[] data)
    {
        return _rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
    }

    public byte[] Decrypt(byte[] data)
    {
        return _rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);
    }
}