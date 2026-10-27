# Repository Guidelines

## Project Structure & Module Organization

MyClock is a Windows desktop application built with WPF on .NET 10. The solution is `WpfApp1.sln`, and the application project is `MyClock.csproj`. Keep presentation and behavior in their existing areas: `Views/` contains XAML pages and code-behind, `ViewModels/` contains bindable presentation state, `Models/` contains data types, `Services/` contains clock, timing, and Windows window-tracking logic, and `Commands/` contains command implementations. Shared styles belong in `Resources/Theme.xaml`. There is currently no test project; `bin/` and `obj/` are generated build output.

## Build, Test, and Development Commands

Run commands from the repository root on Windows with the .NET 10 SDK installed:

```powershell
dotnet restore
dotnet build .\MyClock.csproj
dotnet run --project .\MyClock.csproj
```

Restore resolves NuGet packages, build compiles the WPF app and XAML, and run launches it. No automated test suite is configured. For behavior changes, build the project and manually verify the affected page or monitor behavior in the Windows app.

## Coding Style & Naming Conventions

Follow the existing C# and XAML patterns. Use four spaces for indentation, nullable reference types, and the existing MVVM split between views, view models, models, and services. Use PascalCase for types and public members, and camelCase for local variables and private fields. Name view and view-model files after their feature (for example, `ClockView.xaml` and `ClockViewModel.cs`). Keep UI markup in XAML and place reusable visual definitions in `Resources/Theme.xaml`; keep code-behind focused on view lifecycle and interaction wiring.

## Testing Guidelines

There are no test projects or coverage requirements at present. If adding tests, create a clearly named test project and cover service behavior independently from WPF UI where practical. Ensure the app still builds with `dotnet build .\MyClock.csproj` after changes.

## Commit & Pull Request Guidelines

Git history is not available in this checkout, so no repository-specific commit convention can be confirmed. Use short, imperative commit subjects that describe one change. Pull requests should summarize user-visible and technical changes, list validation performed, link related issues when applicable, and include screenshots for UI changes.

## Configuration & Platform Notes

This project targets `net10.0-windows` and uses WPF and Windows APIs; builds and runtime checks require a Windows-capable .NET environment. Do not commit generated `bin/` or `obj/` files; they are ignored by `.gitignore`.
