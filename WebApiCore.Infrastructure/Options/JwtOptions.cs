namespace WebApiCore.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "JWT";

    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Subject { get; set; }
    public int ExpireMin { get; set; } = 60;
}