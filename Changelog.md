# Transition to net standard change log

## Configuration

- Configuration locations file (loadPaths.json) doesn't support `$(prefix)` anymore. Use `GIGYA_CONFIG_ROOT` environment variable instead.
- Default `ConfigRoot` now is now $CurrentWorkingDir/config/ instead of beging hardcoded.
- Updated Newtonsoft.Json to 12.0.3
- Requires VS 16.4
- Host configuration is now injected into the host. Host isn't aware of environment anymore.
- Configuration values aren't taken from environemnt variables anymore but are injected with host configuration.
- Product information is now stored in Directory.Build.props