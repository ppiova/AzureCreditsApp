# MSIX packaging

This repo includes an MSIX packaging project and GitHub Actions workflows that build the package for Windows.

| Workflow | Trigger | Output |
| --- | --- | --- |
| `CI` | Every pull request and push to `main` | Builds and tests the app, and builds the MSIX unsigned to check packaging still works. |
| `Build MSIX` | A `v*` tag, or run manually | Signed `.msix` for sideloading, attached to the GitHub Release when run from a tag. |
| `Build MSIX (Store)` | Run manually with a version | Unsigned `.msixupload` to upload to Partner Center. |

The packaging project needs the Visual Studio MSIX tooling, so it is built with MSBuild on the runner rather than with `dotnet build`.

## Release flow

Create and push a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The `Build MSIX` workflow builds the package and uploads it to the GitHub Release.

## Installing a GitHub Release

Download `CloudCreditsManager-MSIX-<version>.zip` from the release, extract it, then run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Install-CloudCreditsManager.ps1
```

The script imports the included `.cer` certificate into `Cert:\CurrentUser\TrustedPeople` and then installs the `.msix` package.

If you install the `.msix` directly and see `0x800B010A`, Windows does not trust the signing certificate yet. Import the `.cer` first:

```powershell
Import-Certificate -FilePath .\AzureCreditsApp.Package_1.0.0.0_x64.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage .\AzureCreditsApp.Package_1.0.0.0_x64.msix
```

## Signing

MSIX packages must be signed to be installed outside the Store. The `Build MSIX` workflow supports two modes:

1. If no secrets are configured, it creates a temporary self-signed certificate in CI. This is useful for testing, but users must trust the included `.cer` before installing the package.
2. For real distribution, configure these GitHub repository secrets:

| Secret | Description |
| --- | --- |
| `MSIX_PFX_BASE64` | Base64-encoded `.pfx` signing certificate. |
| `MSIX_PFX_PASSWORD` | Password for the `.pfx` certificate. |
| `MSIX_PUBLISHER` | Certificate subject, for example `CN=Your Company`. Must match the PFX subject. |

To convert a PFX to base64:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("path\to\certificate.pfx")) | Set-Clipboard
```

Never commit `.pfx` files or certificate passwords to the repository.

## Microsoft Store submission

1. In Partner Center, reserve the app name and open **Product identity**.
2. Copy **Package/Identity/Name** into `Identity Name` in `AzureCreditsApp.Package/Package.appxmanifest`. The publisher is set by the workflow (from the `MSIX_PUBLISHER` secret, or the default in the workflow file) and must match **Package/Identity/Publisher**.
3. Run **Build MSIX (Store)** from the Actions tab with the version to submit (the fourth segment is forced to `0`, as the Store requires).
4. Download the `CloudCreditsManager-Store-<version>` artifact and upload the `.msixupload` in the submission's **Packages** step.
5. Fill in the listing from [docs/microsoft-store-listing-en-us.md](docs/microsoft-store-listing-en-us.md), including the notes for certification.

The Store re-signs the package, so no certificate is involved in that workflow.
