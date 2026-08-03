# Workflow Guidelines

`sync-unity-pipeline.yml` mirrors the official `com.unity.pipeline` UPM archive into this repository and opens one review PR for each new upstream version. It must validate registry metadata, archive integrity, archive paths, and package identity before changing the checkout. Synchronization PRs must request review from `GaussianGuaicai`.

Keep the job fail-closed: failed downloads or validation must not create a PR. The deletion-based sync must always exclude `.git/`, `.github/`, and the repository contributor guide files. Never overwrite an active synchronization PR because its branch may contain compatibility work under review, and never resynchronize an already-current package version because doing so would revert repository-maintained compatibility patches.
