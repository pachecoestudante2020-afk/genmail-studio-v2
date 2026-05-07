# AGENTS.md

## Project

This repository is a .NET WPF desktop application named GenMail Studio.

The app reads a local `.txt` file containing names or internal usernames and generates local username/email candidate files. It must be used only for lawful internal data processing, account migration, identity testing, or data normalization.

The app must never include SMTP sending, email verification, crawling, scraping, proxy, captcha bypass, bulk messaging, or phishing-related features.

## Required commands

Before claiming the task is complete, always run:

```bash
dotnet restore
dotnet build GenMailStudio.sln -c Release
dotnet test GenMailStudio.sln -c Release
```

If any command fails, do not say the task is complete. Fix the errors first.

## Hard rules

- All `.csproj` files must be valid SDK-style XML.
- `GenMailStudio.sln` must be a valid Visual Studio solution file with proper line breaks.
- Do not compress source files into one line.
- Do not omit generic type arguments.
- Do not leave placeholder WPF XAML files empty.
- Do not create stub implementations and claim they are complete.
- Do not ignore compiler errors.
- Do not say “build should pass”; actually run the commands.
- If WPF is built on Linux, use `<EnableWindowsTargeting>true</EnableWindowsTargeting>` in the WPF project file.

## Required architecture

Use this structure:

```text
src/
  GenMail.Core/
    Models/
    IO/
    Normalization/
    Generation/
    Numbering/
    Quality/
    Emailing/
    Dedupe/
    Reports/
    Safety/
    Pipeline/
  GenMail.Wpf/
    ViewModels/
    Commands/
    Services/
tests/
  GenMail.Core.Tests/
```

## Completion checklist

A task is complete only when:

- `dotnet restore` passes.
- `dotnet build GenMailStudio.sln -c Release` passes.
- `dotnet test GenMailStudio.sln -c Release` passes.
- WPF project has valid `App.xaml`, `MainWindow.xaml`, and code-behind.
- Core project has real implementation, not stubs.
- Tests are meaningful and pass.
- README explains build, test, publish, safety limits, and usage.

## Coding style

- Use C#.
- Use .NET 8.
- Use WPF for the desktop UI.
- Use MVVM.
- Nullable must be enabled.
- Prefer small files and small classes.
- Prefer records for immutable models.
- Do not create a God class.
- Do not put all code into one file.
