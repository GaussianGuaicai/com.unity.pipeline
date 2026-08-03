# Workflow Guidelines

`sync-unity-pipeline.yml` mirrors the official `com.unity.pipeline` UPM archive into this repository and opens or updates one review PR. It must validate registry metadata, archive integrity, archive paths, and package identity before changing the checkout.

Keep the job fail-closed: failed downloads or validation must not create a PR. The deletion-based sync must always exclude `.git/`, `.github/`, and the repository contributor guide files.
