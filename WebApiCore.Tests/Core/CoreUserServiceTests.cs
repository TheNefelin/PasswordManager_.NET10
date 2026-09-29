using WebApiCore.Application.Common;
using WebApiCore.Application.DTOs;
using WebApiCore.Application.Services;
using WebApiCore.Domain.Entities;
using WebApiCore.Infrastructure.Repositories;
using WebApiCore.Infrastructure.Security;
using WebApiCore.Tests.Helpers;
using System.Security.Cryptography;

namespace WebApiCore.Tests.Core;

[Collection("Database")]
public class CoreUserServiceTests : IntegrationTestBase
{
    private static CoreUserService CreateService() => new(
        new CoreUserRepository(TestDb.CreateContext()),
        new PasswordHasher());

    private static CoreUserPasswordCreate CreatePassword(string password, CoreUserRequest coreUser)
        => new() { Password = password, CoreUser = coreUser };

    private static string NewSalt()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_ThenGetCoreUserIV_ReturnsSameIV()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        var registerResult = await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("SecretPM", coreUser),
            CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(registerResult.IV));

        var ivResult = await service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "SecretPM", CoreUser = coreUser },
            CancellationToken.None);

        Assert.Equal(registerResult.IV, ivResult.IV);
    }

    [Fact]
    public async Task GetCoreUserIVAsync_WithInvalidSession_ThrowsUserSessionInvalidException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UserSessionInvalidException>(() => service.GetCoreUserIVAsync(Guid.NewGuid(), new CoreUserPassword
        {
            Password = "SecretPM",
            CoreUser = new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() }
        }, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterCoreUserPasswordAsync_Twice_ThrowsCorePasswordAlreadyExistsException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("SecretPM", coreUser),
            CancellationToken.None);

        await Assert.ThrowsAsync<CorePasswordAlreadyExistsException>(() => service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("SecretPM", coreUser),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetCoreUserIVAsync_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("CorrectPassword", coreUser),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "WrongPassword", CoreUser = coreUser },
            CancellationToken.None));
    }

    [Fact]
    public async Task ChangeCorePasswordAsync_WithWrongOldPassword_ThrowsInvalidCredentialsException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("OldMasterPass", coreUser),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.ChangeCorePasswordAsync(
            userId,
            new ChangeCorePasswordRequest
            {
                OldPassword = "WrongOldPass",
                NewPassword = "NewMasterPass",
                Salt = NewSalt(),
                CoreUser = coreUser,
                Records = new List<CoreDataReplacement>()
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ChangeCorePasswordAsync_WithInvalidSession_ThrowsUserSessionInvalidException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UserSessionInvalidException>(() => service.ChangeCorePasswordAsync(
            Guid.NewGuid(),
            new ChangeCorePasswordRequest
            {
                OldPassword = "OldMasterPass",
                NewPassword = "NewMasterPass",
                Salt = NewSalt(),
                CoreUser = new CoreUserRequest { User_Id = Guid.NewGuid(), SqlToken = Guid.NewGuid() },
                Records = new List<CoreDataReplacement>()
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ChangeCorePasswordAsync_WhenNotConfigured_ThrowsCorePasswordNotConfiguredException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await Assert.ThrowsAsync<CorePasswordNotConfiguredException>(() => service.ChangeCorePasswordAsync(
            userId,
            new ChangeCorePasswordRequest
            {
                OldPassword = "OldMasterPass",
                NewPassword = "NewMasterPass",
                Salt = NewSalt(),
                CoreUser = coreUser,
                Records = new List<CoreDataReplacement>()
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ChangeCorePasswordAsync_WithInvalidSalt_ThrowsRequestValidationException()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("OldMasterPass", coreUser),
            CancellationToken.None);

        // Base64 inválido: se rechaza antes de usar la sal en el hash.
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ChangeCorePasswordAsync(
            userId,
            new ChangeCorePasswordRequest
            {
                OldPassword = "OldMasterPass",
                NewPassword = "NewMasterPass",
                Salt = "not-base64!",
                CoreUser = coreUser,
                Records = new List<CoreDataReplacement>()
            },
            CancellationToken.None));

        // Base64 válido pero de largo incorrecto: también se rechaza.
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ChangeCorePasswordAsync(
            userId,
            new ChangeCorePasswordRequest
            {
                OldPassword = "OldMasterPass",
                NewPassword = "NewMasterPass",
                Salt = Convert.ToBase64String(new byte[8]),
                CoreUser = coreUser,
                Records = new List<CoreDataReplacement>()
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ChangeCorePasswordAsync_Success_ReplacesRecordsAndRotatesToken()
    {
        var (userId, sqlToken) = await CreateUserDirectAsync(NewEmail());
        var service = CreateService();
        var coreUser = new CoreUserRequest { User_Id = userId, SqlToken = sqlToken };

        await service.RegisterCoreUserPasswordAsync(
            userId,
            CreatePassword("OldMasterPass", coreUser),
            CancellationToken.None);

        var dataRepository = new CoreDataRepository(Context);
        var inserted = await dataRepository.InsertAsync(
            new CoreData { Data01 = "a", Data02 = "b", Data03 = "c", User_Id = userId },
            CancellationToken.None);

        var newRecord = new CoreDataReplacement { Data_Id = Guid.NewGuid(), Data01 = "x", Data02 = "y", Data03 = "z" };
        var changeResult = await service.ChangeCorePasswordAsync(
            userId,
            new ChangeCorePasswordRequest
            {
                OldPassword = "OldMasterPass",
                NewPassword = "NewMasterPass",
                Salt = NewSalt(),
                CoreUser = coreUser,
                Records = new List<CoreDataReplacement> { newRecord }
            },
            CancellationToken.None);

        // El IV devuelto es exactamente la sal provista por el cliente.
        Assert.Equal(NewSalt(), changeResult.IV);
        Assert.False(string.IsNullOrEmpty(changeResult.IV));
        Assert.NotEqual(Guid.Empty, changeResult.SqlToken);
        Assert.NotEqual(sqlToken, changeResult.SqlToken);

        // El token viejo quedó invalidado: la sesión de la rotación pasada ya no abre.
        await Assert.ThrowsAsync<UserSessionInvalidException>(() => service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "NewMasterPass", CoreUser = coreUser },
            CancellationToken.None));

        // La nueva contraseña con el token rotado funciona y devuelve el nuevo IV.
        var newCoreUser = new CoreUserRequest { User_Id = userId, SqlToken = changeResult.SqlToken };
        var ivResult = await service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "NewMasterPass", CoreUser = newCoreUser },
            CancellationToken.None);

        Assert.Equal(changeResult.IV, ivResult.IV);

        // La vieja contraseña ya no es válida ni con el token nuevo.
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.GetCoreUserIVAsync(
            userId,
            new CoreUserPassword { Password = "OldMasterPass", CoreUser = newCoreUser },
            CancellationToken.None));

        // Los datos fueron reemplazados: queda solo el registro nuevo.
        var all = (await dataRepository.GetAllAsync(new CoreData { User_Id = userId }, CancellationToken.None)).ToList();
        Assert.Single(all);
        Assert.Equal(newRecord.Data_Id, all[0].Data_Id);
        Assert.DoesNotContain(all, x => x.Data_Id == inserted.Data_Id);
    }
}