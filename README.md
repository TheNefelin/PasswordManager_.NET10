# Password Manager .NET 10

### Dependency
- CommunityToolkit.Maui 13.0.0
- CommunityToolkit.Mvvm 8.4.0
- Microsoft.Extensions.Logging.Debug 10.0.1
- Microsoft.Maui.Controls 10.0.1
- Microsoft.NET.ILLink.Tasks 10.0.1
- Plugin.Maui.Biometric 0.1.0

### Structure
```
PasswordManager_.NET10/
│
├── 📁 Behaviors/
│   └── PasswordDetails/
│       └── MenuAnimationBehavior.cs
│
├── 📁 Converters/
│   ├── InvertedBoolConverter.cs
│   └── StringNotEmptyToBoolConverter.cs
│
├── 📁 DTOs/
│   ├── Request/
│   │   ├── CoreDataDeleteRequest.cs
│   │   ├── CoreDataRequest.cs
│   │   ├── CoreUserIVRequest.cs
│   │   ├── CoreUserRequest.cs
│   │   └── LoginRequest.cs
│   └── Response/
│       └── LoginResponse.cs
│
├── 📁 Exceptions/
│   └── ApiException.cs
│
├── 📁 Helpers/
│   ├── Constants.cs
│   └── Constants_demo.cs
│
├── 📁 Models/
│   ├── ApiResponse.cs
│   ├── CoreSecretData.cs
│   ├── CoreUserIV.cs
│   ├── SessionData.cs
│   └── User.cs
│
├── 📁 Services/
│   ├── Implementation/
│   │   ├── ApiService.cs
│   │   ├── AuthService.cs
│   │   ├── CoreDataService.cs
│   │   ├── EncryptionService.cs
│   │   ├── SecureStorageService.cs
│   │   └── ThemeService.cs
│   │
│   └── Interfaces/
│       ├── IApiService.cs
│       ├── IAuthService.cs
│       ├── ICoreDataService.cs
│       ├── IEncryptionService.cs
│       ├── ISecureStorageService.cs
│       └── IThemeService.cs
│
├── 📁 ViewModels/
│   ├── BaseViewModel.cs
│   ├── LoginViewModel.cs
│   ├── PasswordDetailsViewModel.cs
│   ├── PasswordFormViewModel.cs
│   ├── SettingsViewModel.cs
│   └── TestingViewModel.cs
│
├── 📁 Views/
│   ├── Authentication/
│   │   └── LoginPage.xaml(.cs)
│   │
│   ├── Components/
│   │   └── TestingPage.xaml(.cs)
│   │
│   └── Main/
│       ├── PasswordDetailsPage.xaml
│       ├── PasswordFormPage.xaml
│       ├── PasswordPromptPage.xaml
│       └── SettingsPage.xaml
│
├── 📁 Extensions/
│   └── #NADA AUN
│
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
├── AppShell.xaml.cs
├── MauiProgram.cs
│
└── PasswordManager.Maui.csproj
```
