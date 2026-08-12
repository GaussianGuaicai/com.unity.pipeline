# Editor lifecycle & observability commands

Commands to control Editor play mode, focus, and menus, and to observe the Editor's state, console, and performance.

### `editor_play`
Enter Unity Editor play mode.

No parameters.

**Returns:** `string`

### `editor_stop`
Exit Unity Editor play mode.

No parameters.

**Returns:** `string`

### `editor_pause`
Toggle pause state of Unity Editor play mode (calling it again while paused resumes play mode).

No parameters.

**Returns:** `string`

### `editor_status`
Get detailed Unity Editor status and state information.

No parameters.

**Returns:** `StatusResponse`

### `editor_focus`
Bring the Unity Editor window to the foreground.

No parameters.

**Returns:** `string`
**Notes:** `MainThreadRequired = true`.

### `menu`
Execute an Editor menu item by path, or list available items when no path is given.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | no | `–` | Menu item path to execute, e.g. "Assets/Reimport All". Omit to list available menu items. |

**Returns:** `MenuResponse`
**Notes:** `MainThreadRequired = true`.

### `screenshot`
Capture the Scene or Game view as a PNG and return its file path.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `view` | no | `game` | Which view to capture: 'game' (default) or 'scene' |
| `output` | no | `–` | Output PNG path (absolute, or relative to the project root). Defaults to a timestamped file under <project>/Temp/pipeline-screenshots/. |
| `width` | no | `0` | Output width in pixels. 0 (default) uses the view camera's current width. |
| `height` | no | `0` | Output height in pixels. 0 (default) uses the view camera's current height. |

**Returns:** `ScreenshotResponse`
**Notes:** `MainThreadRequired = true`.

### `set_autotick`
Keep the editor ticking while unfocused by forcing EditorApplication.SignalTick at a throttled rate.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `enable` | no | `true` | Enable (true) or disable (false) auto-tick mode |
| `interval_ms` | no | `16` | Minimum milliseconds between forced ticks. 0 = every update (max rate, pegs a CPU core). Default 16 (~60Hz). |
| `persist` | no | `true` | Persist this choice to SessionState so it survives a domain reload. Set `false` for a one-off/expensive setting (e.g. `interval_ms=0`) that should revert to the last persisted choice (or the default) after the next recompile instead of sticking for the rest of the session. |

**Returns:** `string`
**Notes:** `MainThreadRequired = true`. Persisted in SessionState by default: your enabled/interval choice survives a domain reload (dies with the editor process/session).

Use `persist=false` for a change you only want in effect until the next unrelated recompile — e.g. a short profiling run at `interval_ms=0` (pegs a CPU core) or a one-off test/CI script — so it can't outlive its purpose and silently burn CPU for the rest of the session. Leave `persist` at its default `true` for a setting you want to remain in effect across recompiles, such as the normal always-on 16ms tick.

### `get_console_logs`
Read recently captured Editor console logs (structured).

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `severity` | no | `all` | Filter: all | log | warning | error. 'all' = every entry; 'log' = Log only; 'warning' = Warning only; 'error' = Error/Exception/Assert only. |
| `limit` | no | `100` | Max entries to return (most-recent first), capped at 1000. |

**Returns:** `object`

### `clear_console`
Clear the captured log buffer and the Unity Editor console.

No parameters.

**Returns:** `object`

### `get_performance_stats`
Read render, memory, and frame-timing stats (structured, read-only).

No parameters.

**Returns:** `PerformanceStats`

### `audit`
Run a Project Auditor static-analysis scan. Returns immediately; poll `audit_status` until status is `completed`, then read the CSV.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `categories` | no | `–` | Comma-separated issue categories to scan (e.g. `Code,ProjectSetting,Texture`). Default: all categories. An unknown name is rejected with an error listing the valid values. |
| `output` | no | `–` | CSV output path (absolute, or relative to the project root). Defaults to `<project>/Temp/pipeline-audit/<scanId>.csv`. |

**Returns:** `object` — `{ status, scanId, csvPath }` on start, or `{ status: "unavailable" | "error" | "busy", message }`.
**Notes:** Requires Project Auditor in the Editor. Only one scan runs at a time (a second call returns `busy`). Not cancellable: stop polling to abandon a scan.

### `audit_status`
Get the status of the last audit: `idle` | `scanning` | `completed` | `failed` | `interrupted` | `unavailable`.

No parameters.

**Returns:** `AuditStatus` (JSON) — `issueCount` and `csvPath` are populated when `completed`; `error` when `failed`; `message` when `unavailable`.
**Notes:** `MainThreadRequired = false`, so it answers while a scan holds the main thread (Code analysis compiles assemblies). `interrupted` means a domain reload killed an in-flight scan — re-run `audit`.

#### Project Auditor prerequisites

`audit` needs both Project Auditor **and** its rules:

- **Project Auditor absent** — `audit` returns `unavailable` immediately.
- **Rules absent** — when Project Auditor ships as a built-in editor module, its rules (descriptors,
  API/obsolete databases, Roslyn analyzers) come from the separate `com.unity.project-auditor-rules`
  package. Without it Project Auditor registers no analysis modules and cannot analyze anything, so
  `audit` returns `scanning` and the first `audit_status` poll reports `unavailable` with a message
  naming the package to install. It never reports an empty `completed` scan, which would read as a
  clean project.

The CSV columns are `Category, Severity, Areas, Description, RelativePath, Line, DescriptorId, Recommendation`;
only diagnostics (things to fix) are emitted, not raw inventory rows.

### `get_authoring_root`
Get the base folder (under Assets/) that bare authoring paths resolve against.

No parameters.

**Returns:** `object`

### `set_authoring_root`
Set the base folder (under Assets/) that bare authoring paths resolve against and are confined to. Use 'Assets' for full project access.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `root` | yes | `–` | Project-relative folder under Assets/, e.g. Assets/AgentWork. Use 'Assets' to allow the whole project. |

**Returns:** `object`

See [Creating commands](../creating-commands.md) and [Connectivity](../connectivity.md).
