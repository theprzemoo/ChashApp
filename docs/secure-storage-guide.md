# Secure Storage Guide

## Current state

Application preferences are stored in JSON settings for portability and cross-platform simplicity.

## Recommended future upgrade

Use native secure storage for secrets or sensitive preferences:

- Windows Credential Manager
- macOS Keychain
- Linux Secret Service / Keyring

## Suggested approach

- Keep non-sensitive preferences in JSON.
- Store only secrets in OS-managed secure storage.
- Fall back gracefully when secure storage is unavailable.
