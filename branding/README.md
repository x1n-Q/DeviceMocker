# DeviceMocker branding

`DeviceMocker-icon.svg` and `DeviceMocker-logo-full.svg` are the canonical logo sources.

Run the generator from the repository root after changing either SVG:

```powershell
.\tools\Generate-Branding.ps1
```

The script regenerates the application ICO/PNG files and the Microsoft Store assets in `branding/store/`. Rendering is handled by the small .NET tool in `tools/BrandingRenderer`, so output is deterministic and does not depend on a browser or image editor.
