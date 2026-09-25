# Cloud Credits Manager

Cloud Credits Manager is a Windows desktop app for tracking Azure credits across Microsoft accounts and subscriptions without opening the Azure portal.

> Work in progress. See the [issues](../../issues) and [milestones](../../milestones) for the roadmap.

## Planned features

- Sign in with several Microsoft accounts (personal and work/school) and stay signed in.
- List every subscription each account can see, across tenants.
- Detect the offer type (Azure Sponsorship, Visual Studio monthly credit, Pay-As-You-Go) and the spending limit.
- Remaining credit, original amount and expiration date per credit lot.
- Cost history and top resources by cost.
- Usage alerts (for example at 80%) with Windows notifications and a tray icon.
- Warnings for subscriptions that are billed to a card with active resources.
- Read and create Azure budgets.

## Requirements

- Windows 10 version 1809 or later.
- Read access to the subscriptions and billing profiles you want to track.

## Development

Building from source requires the .NET 10 SDK.

Run locally:

```powershell
dotnet run --project .\AzureCreditsApp\AzureCreditsApp.csproj
```

Build and test:

```powershell
dotnet build .\AzureCreditsApp\AzureCreditsApp.csproj
dotnet test .\AzureCreditsApp.Tests\AzureCreditsApp.Tests.csproj
```

### Solution layout

| Project | Purpose |
| --- | --- |
| `AzureCreditsApp` | WPF app (.NET 10) with MVVM view models, shared styles in `Themes/` and the app icon in `Assets/`. |
| `AzureCreditsApp.Package` | Windows Application Packaging Project that builds the MSIX for sideloading and the Microsoft Store. |
| `AzureCreditsApp.Tests` | xUnit tests. |

The packaging project needs the Visual Studio MSIX tooling (DesktopBridge targets), so it is built with MSBuild in GitHub Actions rather than with `dotnet build`.

## License

This project is licensed under the [MIT License](LICENSE).
