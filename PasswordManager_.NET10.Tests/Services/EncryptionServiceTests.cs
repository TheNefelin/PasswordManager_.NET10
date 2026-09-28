using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Implementation;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager_.NET10.Tests.Services;

public class EncryptionServiceTests
{
    private readonly EncryptionService _encryptionService = new();

    // El IV de los secretos de la nube viaja como Base64, no como los 16 bytes
    // crudos que usa el cache local.
    private static readonly string CloudIv = Convert.ToBase64String(new byte[16]);

    private static CoreSecretData CreateSecret() => new()
    {
        Data_Id = Guid.NewGuid(),
        User_Id = Guid.NewGuid(),
        Data01 = "usuario@example.com",
        Data02 = "clave-con-simbolos-!@#",
        Data03 = "Una contraseña de ejemplo con ñ y acentos"
    };

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsTheSameText()
    {
        const string plainText = "contraseña-local";

        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_DoesNotReturnThePlainText()
    {
        const string plainText = "contraseña-local";

        var encrypted = _encryptionService.Encrypt(plainText);

        Assert.NotEqual(plainText, encrypted);
        Assert.True(_encryptionService.IsEncrypted(encrypted));
    }

    [Fact]
    public void Encrypt_UsesTheLocalKeySoItDependsOnConstants()
    {
        // Si BIOMETRIC_KEY dejara de tener 32 caracteres, aes.Key reventaria.
        // Esta es la unica red que hay: el fallo aparece en runtime, no al compilar.
        var encrypted = _encryptionService.Encrypt("texto");

        // Sin BIOMETRIC_KEY valida, Encrypt habria lanzado CryptographicException.
        Assert.NotNull(encrypted);
    }

    [Fact]
    public void Decrypt_WithTextThatIsNotBase64_ThrowsFormatException()
    {
        // Primer fallo posible: el texto ni siquiera es Base64.
        Assert.Throws<FormatException>(
            () => _encryptionService.Decrypt("no-es-ciphertext-valido!!!"));
    }

    [Fact]
    public void Decrypt_WithValidBase64ThatIsNotCiphertext_ThrowsCryptographicException()
    {
        // Segundo fallo posible: es Base64 valido pero el padding no cuadra, y ahi
        // el que revienta es AES. Decrypt lanza DOS tipos distintos segun el
        // punto en que falle, por eso AuthService.GetSavedPasswordAsync captura
        // Exception y no un tipo concreto: si se angosta ese catch, una cache
        // corrupta dejaria de ser recuperable y romperia el arranque de sesion.
        Assert.Throws<CryptographicException>(
            () => _encryptionService.Decrypt(Convert.ToBase64String(new byte[32])));
    }

    [Fact]
    public void EncryptData_ThenDecryptData_RoundTripsEveryField()
    {
        var original = CreateSecret();
        var password = "contraseña-maestra";

        var encrypted = _encryptionService.EncryptData(original, password, CloudIv);
        var decrypted = _encryptionService.DecryptData(encrypted, password, CloudIv);

        Assert.Equal("usuario@example.com", decrypted.Data01);
        Assert.Equal("clave-con-simbolos-!@#", decrypted.Data02);
        Assert.Equal("Una contraseña de ejemplo con ñ y acentos", decrypted.Data03);
    }

    [Fact]
    public void EncryptData_EncryptsTheFieldsInPlace()
    {
        var secret = CreateSecret();
        var password = "contraseña-maestra";

        _encryptionService.EncryptData(secret, password, CloudIv);

        Assert.NotEqual("usuario@example.com", secret.Data01);
        Assert.NotEqual("clave-con-simbolos-!@#", secret.Data02);
        Assert.NotEqual("Una contraseña de ejemplo con ñ y acentos", secret.Data03);
    }

    [Fact]
    public void EncryptDataCollection_ThenDecryptDataCollection_RoundTripsEveryItem()
    {
        var password = "contraseña-maestra";
        var original = new[] { CreateSecret(), CreateSecret(), CreateSecret() };

        var encrypted = _encryptionService
            .EncryptDataCollection(original, password, CloudIv)
            .ToList();
        var decrypted = _encryptionService
            .DecryptDataCollection(encrypted, password, CloudIv)
            .ToList();

        Assert.Equal(3, decrypted.Count);
        for (var i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i].Data01, decrypted[i].Data01);
            Assert.Equal(original[i].Data02, decrypted[i].Data02);
            Assert.Equal(original[i].Data03, decrypted[i].Data03);
            Assert.Equal(original[i].Data_Id, decrypted[i].Data_Id);
            Assert.Equal(original[i].User_Id, decrypted[i].User_Id);
        }
    }

    [Theory]
    [InlineData("corta")]                       // 5  -> se duplica hasta 32
    [InlineData("exactamente32caracteres!!")]     // 32 -> ya tiene el largo
    public void GetAesKey_PadsAnyPasswordTo32Bytes(string password)
    {
        // GetAesKey duplica el string hasta llegar a 32 bytes. "corta" (5) queda
        // como "corticorta..." y "exactamente32caracteres!!" entra justo. Los dos
        // tienen que poder cifrar y descifrar.
        var secret = CreateSecret();

        var encrypted = _encryptionService.EncryptData(secret, password, CloudIv);
        var decrypted = _encryptionService.DecryptData(encrypted, password, CloudIv);

        Assert.Equal("usuario@example.com", decrypted.Data01);
    }

    [Fact]
    public void DecryptData_WithAnotherPassword_DoesNotReturnTheOriginal()
    {
        var encrypted = _encryptionService.EncryptData(CreateSecret(), "correcta", CloudIv);

        // Con otra clave el padding no cuadra: falla, en vez de devolver basura.
        Assert.Throws<CryptographicException>(
            () => _encryptionService.DecryptData(encrypted, "otra-clave", CloudIv));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hola")]
    [InlineData("no-es-base64-valido!!!")]
    public void IsEncrypted_ReturnsFalseForPlainText(string text)
    {
        Assert.False(_encryptionService.IsEncrypted(text));
    }
}
