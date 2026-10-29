# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.8.0] - 2026-10-29
### Added
- Support exposed references from all sources.

### Changed
- Minimum Unity version is now 2022.3.
- Depend on `com.unity.timeline` 1.7.0.
- Track bind info is now lazy loaded.
- Exposed reference bind info uses the property name.

### Fixed
- Bindings of sub timelines.
- Releasing and clearing a timeline.
- Example sample.
