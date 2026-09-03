# Third-party notices

## TesseractOCR 5.5.2

FloatingTools uses the `TesseractOCR` .NET wrapper for local English OCR.

- Project: https://github.com/Sicos1977/TesseractOCR
- NuGet: https://www.nuget.org/packages/TesseractOCR/5.5.2
- License: Apache License 2.0

The Apache License 2.0 text is included at `licenses/Apache-2.0.txt`.

## Bundled native Tesseract OCR engine (tesseract55.dll)

The `TesseractOCR` package bundles a compiled native `tesseract55.dll`
(x86 and x64) from the upstream [tesseract-ocr/tesseract](https://github.com/tesseract-ocr/tesseract)
engine project.

- Project: https://github.com/tesseract-ocr/tesseract
- License: Apache License 2.0

The Apache License 2.0 text is included at `licenses/Apache-2.0.txt`.

## Bundled native Leptonica imaging library (leptonica-1.85.0.dll)

The `TesseractOCR` package also bundles a compiled native
`leptonica-1.85.0.dll` (x86 and x64) from the upstream
[DanBloomberg/leptonica](https://github.com/DanBloomberg/leptonica) imaging
library, which Tesseract depends on to read image data.

- Project: https://github.com/DanBloomberg/leptonica
- License: Leptonica's own permissive license (BSD-2-Clause-style, with
  project-specific wording), copyright (C) 2001-2020 Leptonica.

Verified directly from the upstream repository's `1.85.0` tag — the exact
version matching the bundled `leptonica-1.85.0.dll` — at
https://github.com/DanBloomberg/leptonica/blob/1.85.0/leptonica-license.txt.
The license text is included verbatim at `licenses/Leptonica-License.txt`.

## English Tesseract language data

`src/FloatingTools.App/tessdata/eng.traineddata` comes from the official
`tesseract-ocr/tessdata_fast` repository.

- Source: https://github.com/tesseract-ocr/tessdata_fast/blob/main/eng.traineddata
- License: Apache License 2.0
- SHA-256: `7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2`

The Apache License 2.0 text is included at `licenses/Apache-2.0.txt`.

## Open XML SDK 3.5.1

FloatingTools uses the Open XML SDK to generate local Microsoft Word `.docx`
exports without requiring Microsoft Word or COM automation. This pulls in two
NuGet packages from the same upstream project, both MIT-licensed under the
same Microsoft copyright:

- `DocumentFormat.OpenXml` 3.5.1
- `DocumentFormat.OpenXml.Framework` 3.5.1 (a dependency of the package
  above; shipped in the publish output as `DocumentFormat.OpenXml.Framework.dll`)

- Project: https://github.com/dotnet/Open-XML-SDK
- NuGet: https://www.nuget.org/packages/DocumentFormat.OpenXml/3.5.1
- License: MIT
- Copyright: © Microsoft Corporation. All rights reserved.

The license text is included at `licenses/Open-XML-SDK-MIT.txt`.

## CommunityToolkit.Mvvm 8.4.0

FloatingTools uses `CommunityToolkit.Mvvm` (the .NET Community Toolkit's MVVM
library) for observable view-model state and commands.

- Project: https://github.com/CommunityToolkit/dotnet
- NuGet: https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.0
- License: MIT
- Copyright: (c) .NET Foundation and Contributors

The license text is included at `licenses/CommunityToolkit-Mvvm-MIT.txt`,
copied verbatim from the package's own bundled `License.md`.

Note: the package also bundles a broader `ThirdPartyNotices.txt` covering
components used elsewhere in the wider .NET Community Toolkit source tree
(e.g. UWP notification helpers, a Markdown control). Those components are
not part of the `CommunityToolkit.Mvvm`-only feature set FloatingTools
references (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`, and
related MVVM helpers), so that broader notices file is not reproduced here
to avoid attributing components FloatingTools does not actually use. This
could not be fully verified against the package's compiled contents, so it
is noted here rather than asserted as certain.

## PDFsharp-WPF 6.2.4

FloatingTools uses `PDFsharp-WPF` to generate local PDF output.

- Project: https://github.com/empira/PDFsharp
- NuGet: https://www.nuget.org/packages/PDFsharp-WPF/6.2.4
- License: MIT
- Copyright: (c) 2026 empira Software GmbH

The license text is included at `licenses/PDFsharp-WPF-MIT.txt`. The package
itself does not bundle a standalone `LICENSE` file; the copyright holder and
year above are taken from the package's own NuGet metadata (`nuspec`
`<copyright>` field), and the license type is taken from its declared SPDX
license expression (`MIT`).

## Scope of this document

This document covers every third-party NuGet package and native library that
lands in a self-contained `win-x64` publish output, verified directly against
`FloatingTools.App.deps.json` and the publish folder's file listing.

Two thin, Microsoft-published packages are pulled in transitively —
`Microsoft.Extensions.DependencyInjection.Abstractions` and
`Microsoft.Extensions.Logging.Abstractions` (both MIT-licensed, copyright ©
Microsoft Corporation) — along with the .NET and WPF runtime redistributables
themselves (`System.*`, `Presentation*`, `clr*`, `mscor*`, `hostfxr`,
`hostpolicy`, etc.), all part of the .NET SDK/runtime and licensed under the
same MIT terms as .NET itself. These are not given individual notice entries
here, consistent with standard practice for framework/runtime redistributables
in a self-contained .NET publish.
