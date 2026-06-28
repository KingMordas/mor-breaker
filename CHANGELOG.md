# Changelog

All notable changes to **morBreaker** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is the **durable, load-bearing changelog**: every user-visible change adds its
entry under `## [Unreleased]` in the *same* change (see `CLAUDE.md` → *Commits, changelog &
release*). Entries are **not** reconstructed from commit messages at release time — the
`/release` skill simply promotes `## [Unreleased]` to a stamped, versioned heading.

## [Unreleased]

### Added

### Changed

### Fixed

## [1.1.0] - 2026-06-28

### Added

- Press `F12` to quit the game (Windows build), so you no longer need `Alt+F4`.
- An on-screen key guide in the bottom-left corner listing the available shortcuts (`F1` high scores, `F12` quit).

## [1.0.0] - 2026-06-28

### Added

- Initial public release of **morBreaker**, a 2D brick-breaker game.
- Bouncing ball with paddle "english" (where you hit steers the bounce) that speeds up on every wall bounce and every non-breaking brick hit, with the ramp steepening each level (capped, and reset when you lose a ball).
- 10 hand-tuned levels of rising difficulty, with a runtime-generated, colour-coded brick grid.
- Score, lives, level progression, and a win bonus for clearing the final level.
- Optional local high-score list (name + score + level + date + a "beat-the-game" flag) with an in-game top-10 panel (`F1`), stored only on your own device (browser `localStorage` on WebGL) and never transmitted.
- Keyboard and pointer/touch controls (Unity Input System) and a HUD that scales to any screen size.
- Builds for Windows (Standalone, x86_64) and WebGL.
