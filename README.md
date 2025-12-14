# Password Manager .NET 10

### Dependency
- CommunityToolkit.Mvvm 8.4.0
- CommunityToolkit.Maui 13.0.0

### Structure
```
PasswordManager_.NET10/
│
├── 📁 Behaviors/
│   ├── Common/
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
│   │   ├── SecureStorageService.cs
│   │   └── ThemeService.cs
│   │
│   └── Interfaces/
│       ├── IApiService.cs
│       ├── IAuthService.cs
│       ├── ICoreDataService.cs
│       ├── ISecureStorageService.cs
│       └── IThemeService.cs
│
├── 📁 ViewModels/
│   ├── BaseViewModel.cs
│   ├── LoginViewModel.cs
│   ├── PasswordDetailsViewModel.cs
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
