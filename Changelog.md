# Transition to net standard change log

## Configuration

- Configuration locations file (loadPaths.json) doesn't support `$(prefix)` anymore. Use `GIGYA_CONFIG_ROOT` environment variable instead.
- Default `ConfigRoot` now is now $CurrentWorkingDir/config/ instead of beging hardcoded.
- Updated Newtonsoft.Json to 12.0.3