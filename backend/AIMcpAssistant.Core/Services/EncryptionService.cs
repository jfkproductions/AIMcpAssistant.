using AIMcpAssistant.Data.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AIMcpAssistant.Core.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private const string ENCRYPTION_PREFIX = "[ENC]";

    public EncryptionService(IConfiguration configuration)
    {
        var secret = configuration["EncryptionKey"];
        if (string.IsNullOrEmpty(secret) || secret.Length < 32)
        {
            throw new ArgumentException("EncryptionKey must be at least 32 characters long.");
        }
        _key = Encoding.UTF8.GetBytes(secret.Substring(0, 32));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using (var aes = Aes.Create())
        {
            aes.Key = _key;
            aes.GenerateIV();
            var iv = aes.IV;

            using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
            using (var ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length);
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }
                return ENCRYPTION_PREFIX + Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText) || !cipherText.StartsWith(ENCRYPTION_PREFIX))
            return cipherText;

        var data = Convert.FromBase64String(cipherText.Substring(ENCRYPTION_PREFIX.Length));

        using (var aes = Aes.Create())
        {
            aes.Key = _key;
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(data, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream(data, iv.Length, data.Length - iv.Length))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }

    public bool IsEncrypted(string text)
    {
        return !string.IsNullOrEmpty(text) && text.StartsWith(ENCRYPTION_PREFIX);
    }
}