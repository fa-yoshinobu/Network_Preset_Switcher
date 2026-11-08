## Download
- [Windows x64 ZIP]({{DOWNLOAD_URL}})
- [SHA-256 checksum]({{CHECKSUM_URL}})

## Getting started
Extract the ZIP file, then run `NetworkPresetSwitcher.exe`.

Requirements: Windows x64. The .NET runtime is included. Changing network settings may require administrator privileges.

## Changes
### Changed
- Migrated the app, tests and build workflows to .NET 10. The Windows executable includes .NET 10.0.14; no separate runtime installation is required.
- Redesigned the main window and application icon, with a responsive layout, clearer adapter status, preset details and inline validation.
- Added an apply preview and confirmation dialog, active-preset markers and subnet prefix input such as `/24`.
- Display actual bundled library versions in the version information window.

### Fixed
- Speed up startup and adapter refresh by querying device information once for all adapters.
- Limit adapter candidates to those shown in Windows Network Connections, excluding hidden filter layers and no-longer-present device entries. If adapter discovery is unavailable, retain the existing fallback list.
- Prevent Enter in editor or Ping fields from applying a preset without confirmation.
- Correct disconnected status badges and button padding.
- Prevent duplicate build output directories and consolidate redundant DHCP test coverage.

<!-- virustotal:start -->
{{VIRUSTOTAL_CONTENT}}
<!-- virustotal:end -->
