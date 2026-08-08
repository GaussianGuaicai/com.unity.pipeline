# Unity Pipeline Package

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/releases/editor/whats-new/2022.3.0)

Transform Unity Editor into a programmable automation element for CI/CD pipelines and development workflows. The package exposes a running Unity Editor (or development Player) over a local HTTP API so external tools, scripts, or agents can execute commands remotely.

## Compatibility focus

This repository maintains Unity Pipeline compatibility for Unity 2022.3 LTS and newer, so projects that cannot yet move to Unity 6 can keep using the same Editor automation, runtime command, and Mono hot-reload workflows. Unity 6-specific APIs retain compatible fallbacks for Unity 2022.3.

Input simulation is optional: it is available when the Input System package is installed and enabled; otherwise its commands return a structured unavailable result without preventing the package from loading. Hot reload remains supported on Mono only and is not available for IL2CPP builds.

## Install the Unity CLI

The Pipeline package is driven through the `unity` CLI.

macOS / Linux
```
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

Windows (PowerShell)
```
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

For more details, see the [CLI documentation](https://github.com/Unity-Technologies/unity-hub/tree/dev/src/cli).

## Install the Pipeline package

Install the package through the Unity Package Manager using its Git URL:

1. Open **Window > Package Manager** in Unity (or **Window > Package Management > Package Manager** in newer Unity versions).
2. Open the install/add menu and select **Install package from Git URL** / **Add package from Git URL**.
3. Enter the following URL and install the package:

   ```text
   https://github.com/GaussianGuaicai/com.unity.pipeline.git
   ```

For details, see Unity's official documentation for [installing a package from a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

After installation, the package automatically starts its HTTP server and registers the built-in commands when the project is open in the Unity Editor.

## Connect to a running Editor

Run `unity command` with no command name to connect to a Unity instance and list its available commands:

```bash
# Auto-discover a Unity instance from the current directory
unity command

# Connect to a specific project
unity command --project-path /path/to/your/unity/project
```

Click [here](Documentation~/connectivity.md) for more details on Connectivity troubleshooting.

## Connect to a running Player (Runtime)

To target a running development Player instead of the Editor, use `--runtime` (by process name) or `--runtime-path` (by the location of the runtime port file). These options go **after** `command` and **before** the command name.

```bash
# By Player process/executable name
unity command --runtime MyGame.exe runtime_status

# By the path where the runtime port file is located
unity command --runtime-path <path> runtime_status
```

`--runtime-path` points to where the `.unity-pipeline-runtime-port` file lives:

- **Windows** — the port file sits next to the Player executable:

  ```bash
  unity command --runtime-path "C:\Builds\MyGame" runtime_status
  ```

- **macOS** — pass the path to the `.app` bundle:

  ```bash
  unity command --runtime-path "/Users/me/Builds/MyGame.app" runtime_status
  ```

Click [here](Documentation~/connectivity.md) for more details on Connectivity troubleshooting.

## Documentation

For the full command reference, connectivity details, runtime setup, and hot-reload guides, see the [Pipeline documentation](Documentation~/index.md).

## License

com.unity.package is licensed under the Unity Companion License. See [LICENSE.md](LICENSE.md) for more legal information.

## Contributions to this repository

We are not accepting pull requests at this time. If you find an issue with the package or would like to request a new feature, please submit a [GitHub issue](https://github.com/Unity-Technologies/com.unity.pipeline/issues).
