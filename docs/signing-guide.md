# Signing Guide

## Windows code signing

To sign `ChashApp.exe` and the installer, you need:

- a valid code-signing certificate,
- `signtool.exe`,
- secure secret handling in CI.

## Recommended flow

1. Publish the Windows build.
2. Sign `ChashApp.exe`.
3. Build the installer.
4. Sign the installer executable.
5. Upload signed artifacts.

## Notes

- This repository does not include a certificate.
- Signing should be added only when you have a real certificate and a protected CI secret setup.
