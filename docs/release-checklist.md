# Release Checklist

## Before publishing

- Run `dotnet test tests/ChashApp.Tests/ChashApp.Tests.csproj`
- Run `dotnet build src/ChashApp/ChashApp.csproj -c Release`
- Run `publish\publish-win-x64.ps1`
- Run `installer\build-installer.ps1`
- Test file encryption, decryption, verify, note encryption, and history export
- Test installer, launch, and uninstall flow on a clean machine or VM

## Before pushing to GitHub

- Do not commit `artifacts/`
- Do not commit generated installer `.exe` files
- Do not commit local history or exported CSV/JSON files
- Confirm README screenshots and wording match the current UI
- Confirm release notes match the actual supported KDF options

## Commercial-readiness reminders

- Add code signing for the app and installer
- Add a stronger release QA checklist
- Add more integration coverage for publish and installer flows
- Review security wording before every public release
