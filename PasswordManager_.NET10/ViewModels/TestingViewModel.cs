using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Models;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

public partial class TestingViewModel : BaseViewModel
{
    private readonly ILogger<TestingViewModel> _logger;
    private readonly ICoreDataService _coreDataService;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string resultText = "Presiona el botón";

    public TestingViewModel(
        ILogger<TestingViewModel> logger,
        ICoreDataService coreDataService)
    {
        _logger = logger;
        _coreDataService = coreDataService;

        Title = "Testing";
        _logger.LogInformation("TestingViewModel creado");
    }

    [RelayCommand]
    public async Task ExecuteFetch()
    {
        _logger.LogInformation("BOTÓN PRESIONADO!");

        try
        {
            _logger.LogInformation("[TestingViewModel-ExecuteFetchAsync] Starting fetch test");

            IsLoading = true;
            ResultText = "Cargando datos...";

            var coreData = new CoreSecretData
            {
                Data_Id = Guid.Parse("4AA6D6AB-1EFE-4C86-BB29-7216B2762BAE"),
                Data01 = "Prueba2",
                Data02 = "Prueba2",
                Data03 = "Prueba2",
                User_Id = new Guid()
            };

            // Ejecutar fetch
            var result = await _coreDataService.DeleteCoreDataAsync(coreData.Data_Id);

            ResultText = $"✅ Fetch completed: {result}";
            //ResultText = $"✅ Fetch completed:\nIV: {result.Data01}";
            //ResultText = "";
            //foreach (CoreSecretData item in result)
            //{
            //    ResultText = ResultText + $"Id: {item.Data_Id.ToString()} - Data01: {item.Data01.ToString()}\n";
            //}

            _logger.LogInformation("[TestingViewModel-ExecuteFetchAsync] Fetch completed successfully");
        }
        catch (Exception ex)
        {
            ResultText = $"❌ Error:\n{ex.Message}";
            _logger.LogError(ex, "[TestingViewModel-ExecuteFetchAsync] Fetch failed: {Message}", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
