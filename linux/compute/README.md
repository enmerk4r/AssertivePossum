# Linux Rhino.Compute Deployment

Scripts and packages for installing the Assertive Possum Grasshopper plugin on a Linux Rhino.Compute instance.

## Contents

```
packages/                          # .yak package(s) — auto-populated by Release builds
install-compute-plugin.sh          # Installer script
```

## Usage

The script can be run from any directory — it resolves paths relative to its own location. It expects a `.yak` package to be present in the `packages/` subfolder. This is populated automatically when building the plugin in Release mode (`dotnet build src/AssertivePossum -c Release`).

```bash
sudo ./install-compute-plugin.sh
```

This will:
1. Find the `.yak` package in `packages/`
2. Uninstall any previous version of Assertive Possum
3. Install the package via `yak` for the Compute service user
4. Enable Grasshopper loading in the Compute environment file

## Options

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `SERVICE_USER` | `rhino-compute` | Linux user running the Compute service |
| `COMPUTE_ENV_FILE` | `/etc/rhino-compute/environment` | Compute environment config file |
| `RESTART_SERVICE` | `false` | Set to `true` to restart the service after install |

### Example with options

```bash
sudo RESTART_SERVICE=true SERVICE_USER=myuser ./install-compute-plugin.sh
```

## Prerequisites

- `yak` CLI on PATH
- A Linux Rhino.Compute service user already configured
- Run as root (`sudo`)
