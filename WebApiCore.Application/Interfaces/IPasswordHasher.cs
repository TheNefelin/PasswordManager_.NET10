namespace WebApiCore.Application.Interfaces;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    string HashPassword(string password, string salt);
    bool VerifyPassword(string password, string hashedPassword, string salt);
}