# GitHub Automation Guidelines

This directory contains repository automation that is not part of the Unity package payload. Upstream package synchronization must preserve `.github/` while mirroring the downloaded `package/` directory.

Workflows must acquire package content only through the official Unity Package Registry over HTTPS. They must not start a Unity session and must use `GITHUB_TOKEN` only for repository writes.
