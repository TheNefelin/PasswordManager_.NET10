namespace WebApiCore.Application.Interfaces;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword, string salt);
}