using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager_.NET10.Services.Implementation;

// ==================== IMPLEMENTACIÓN ====================
// EncryptionService.cs
public class EncryptionService : IEncryptionService
{
    /// <summary>
    /// Encripta un objeto CoreSecretData
    /// </summary>
    public CoreSecretData EncryptData(CoreSecretData coreSecretData, string password, string iv)
    {
        byte[] aesKey = GetAesKey(password);
        coreSecretData.Data01 = Encrypt(coreSecretData.Data01, aesKey, iv);
        coreSecretData.Data02 = Encrypt(coreSecretData.Data02, aesKey, iv);
        coreSecretData.Data03 = Encrypt(coreSecretData.Data03, aesKey, iv);
        return coreSecretData;
    }

    /// <summary>
    /// Desencripta un objeto CoreSecretData
    /// </summary>
    public CoreSecretData DecryptData(CoreSecretData coreSecretData, string password, string iv)
    {
        byte[] aesKey = GetAesKey(password);
        coreSecretData.Data01 = Decrypt(coreSecretData.Data01, aesKey, iv);
        coreSecretData.Data02 = Decrypt(coreSecretData.Data02, aesKey, iv);
        coreSecretData.Data03 = Decrypt(coreSecretData.Data03, aesKey, iv);
        return coreSecretData;
    }

    /// <summary>
    /// Encripta una colección completa
    /// </summary>
    public IEnumerable<CoreSecretData> EncryptDataCollection(IEnumerable<CoreSecretData> collection, string password, string iv)
    {
        byte[] aesKey = GetAesKey(password);
        var encryptedCollection = new List<CoreSecretData>();

        foreach (var item in collection)
        {
            var encryptedItem = new CoreSecretData
            {
                Data_Id = item.Data_Id,
                Data01 = Encrypt(item.Data01, aesKey, iv),
                Data02 = Encrypt(item.Data02, aesKey, iv),
                Data03 = Encrypt(item.Data03, aesKey, iv),
                User_Id = item.User_Id
            };
            encryptedCollection.Add(encryptedItem);
        }

        return encryptedCollection;
    }

    /// <summary>
    /// Desencripta una colección completa
    /// </summary>
    public IEnumerable<CoreSecretData> DecryptDataCollection(IEnumerable<CoreSecretData> collection, string password, string iv)
    {
        byte[] aesKey = GetAesKey(password);
        var decryptedCollection = new List<CoreSecretData>();

        foreach (var item in collection)
        {
            var decryptedItem = new CoreSecretData
            {
                Data_Id = item.Data_Id,
                Data01 = Decrypt(item.Data01, aesKey, iv),
                Data02 = Decrypt(item.Data02, aesKey, iv),
                Data03 = Decrypt(item.Data03, aesKey, iv),
                User_Id = item.User_Id
            };
            decryptedCollection.Add(decryptedItem);
        }

        return decryptedCollection;
    }

    /// <summary>
    /// Encripta un string usando AES
    /// </summary>
    private string Encrypt(string plainText, byte[] key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(iv);

        ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        byte[] encrypted = ms.ToArray();
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Desencripta un string usando AES
    /// </summary>
    private string Decrypt(string encryptedText, byte[] key, string iv)
    {
        byte[] cipherBytes = Convert.FromBase64String(encryptedText);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(iv);

        ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    /// <summary>
    /// Genera la clave AES de 32 bytes a partir de la contraseña
    /// Nota: Este método está fijo porque los datos ya fueron encriptados de esta manera
    /// </summary>
    private byte[] GetAesKey(string pass)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(pass);
        while (keyBytes.Length < 32)
            keyBytes = keyBytes.Concat(keyBytes).ToArray();
        return keyBytes.Take(32).ToArray();
    }

    public bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            Convert.FromBase64String(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
