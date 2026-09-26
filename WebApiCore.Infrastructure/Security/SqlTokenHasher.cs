using System.Security.Cryptography;
using System.Text;

namespace WebApiCore.Infrastructure.Security;

public static class SqlTokenHasher
{
    /// <summary>
    /// Devuelve el SHA-256 en hexadecimal minúsculas del token de sesión.
    /// El token crudo nunca se persiste: la base solo guarda este hash.
    /// </summary>
    /// <remarks>
    /// El formato de entrada es el canónico de GUID ("D", con guiones y en
    /// minúsculas), que es exactamente lo que produce
    /// <c>CONVERT(VARCHAR(36), SqlToken)</c> en SQL Server. La salida en
    /// minúsculas coincide con <c>HASHBYTES(..., 2)</c> para que la
    /// comparación en SQL no dependa de la collation de la base.
    /// Ver SqlServer_Migrate_SqlTokenHash.sql.
    /// </remarks>
    public static string Hash(Guid sqlToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sqlToken.ToString("D")));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
