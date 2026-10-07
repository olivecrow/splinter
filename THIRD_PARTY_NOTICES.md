# Third party notices

Splinter uses the projects listed below. Versions are taken from [Directory.Packages.props](Directory.Packages.props) and the checked-in `packages.lock.json` files. Each component retains its upstream license and copyright; these credits do not assign a license to Splinter itself.

## WinFsp

**WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos.**

- Project: [winfsp/winfsp](https://github.com/winfsp/winfsp).
- Package: `WinFsp.Net` / `winfsp.net` **2.2.26215**, including `winfsp-msil.dll`.
- Role: writable filesystem views for project bases and private range journals.
- License: [GPLv3 with the WinFsp FLOSS exception, release v2.2B4](https://github.com/winfsp/winfsp/blob/v2.2B4/License.txt).

The exception permits linking free and open source software to the listed WinFsp DLLs and distributing unmodified official installers, subject to its conditions. It requires the copyright wording above and a repository link in the software's user interface and user-facing documentation. It also sets conditions on the software's license and on linking or distributing it with proprietary software. A README credit alone does not satisfy the separate user-interface condition. Commercial licensing is available from the upstream author; the full upstream text governs.

## Runtime packages

| Package | Version | Upstream and license |
| --- | --- | --- |
| System.CommandLine | 2.0.12 | [dotnet/command-line-api](https://github.com/dotnet/command-line-api), MIT |
| JsonSchema.Net | 9.4.0 | [json-everything](https://github.com/json-everything/json-everything), MIT source and the published-binary agreement below |
| JsonPointer.Net | 7.0.2 | [json-everything](https://github.com/json-everything/json-everything), MIT source and the published-binary agreement below |
| Json.More.Net | 3.0.1 | [json-everything](https://github.com/json-everything/json-everything), MIT source and the published-binary agreement below |
| Humanizer.Core | 3.0.10 | [Humanizr/Humanizer](https://github.com/Humanizr/Humanizer), [MIT](https://github.com/Humanizr/Humanizer/blob/90f0cb1d7427635f38b46350bb1dc5fca2164f2d/license.txt) |
| Microsoft.Win32.Registry | 5.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime), MIT |
| System.IO.FileSystem.AccessControl | 5.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime), MIT |
| System.Security.AccessControl | 6.0.1 | [dotnet/runtime](https://github.com/dotnet/runtime), MIT |

System.CommandLine and the Microsoft runtime packages carry Microsoft Corporation copyright notices in their package metadata. Humanizer and json-everything identify the .NET Foundation and contributors in their source license notices. Preserve the applicable copyright and permission notices when redistributing their software. The restored Microsoft runtime packages also include `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`.

Humanizer's [license file](https://github.com/Humanizr/Humanizer/blob/90f0cb1d7427635f38b46350bb1dc5fca2164f2d/license.txt) additionally credits [Inflector](https://github.com/srkirkland/Inflector), Copyright (c) 2013 Scott Kirkland, and [ByteSize](https://github.com/omar/ByteSize), Copyright (c) 2013-2014 Omar Khudeira, under MIT terms.

### json-everything published binaries

The pinned JsonSchema.Net, JsonPointer.Net, and Json.More.Net NuGet packages declare `OSMFEULA.txt` as their license file. The source remains under the [MIT license](https://github.com/json-everything/json-everything/blob/399f198431f65cf6896fe6038f833ef6d0b27a39/LICENSE); the published binaries include a separate Open Source Maintenance Fee agreement.

The agreement describes a fee for revenue-generating users with annual gross revenue of at least US$10,000, with exemptions including separate support or maintenance arrangements. It preserves MIT rights, including redistribution with the applicable notices and independently compiling the source. See the complete agreement for the exact applicability and payment terms:

- [JsonSchema.Net 9.4.0 and JsonPointer.Net 7.0.2 agreement](https://github.com/json-everything/json-everything/blob/399f198431f65cf6896fe6038f833ef6d0b27a39/OSMFEULA.txt).
- [Json.More.Net 3.0.1 agreement](https://github.com/json-everything/json-everything/blob/8b8ab34027de5ad9f4ed50808b8e4889ca69cf4d/OSMFEULA.txt).

## Test and development packages

| Packages | Version | Upstream and license |
| --- | --- | --- |
| xunit.v3, xunit.v3.assert, xunit.v3.common, xunit.v3.core.mtp-v2, xunit.v3.extensibility.core, xunit.v3.mtp-v2, xunit.v3.runner.common, xunit.v3.runner.inproc.console | 4.0.1 | [xunit/xunit](https://github.com/xunit/xunit), [Apache 2.0 and the accompanying upstream notices](https://github.com/xunit/xunit/blob/main/LICENSE) |
| xunit.analyzers | 2.1.0 | [xunit/xunit.analyzers](https://github.com/xunit/xunit.analyzers), Apache 2.0 |
| Microsoft.Testing.Platform, Microsoft.Testing.Platform.MSBuild, Microsoft.Testing.Extensions.TrxReport, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Extensions.Telemetry | 2.4.0 | [microsoft/testfx](https://github.com/microsoft/testfx), MIT |
| Microsoft.ApplicationInsights | 2.23.0 | [microsoft/ApplicationInsights-dotnet](https://github.com/microsoft/ApplicationInsights-dotnet), MIT |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime), MIT |

xUnit package metadata identifies the .NET Foundation as the copyright holder. Its source license also preserves an MIT notice for code adapted from MSBuild. Microsoft package metadata identifies Microsoft Corporation. Include the applicable license texts and upstream notices when redistributing these packages; test dependencies are distinct from the product's runtime dependency set.

## Runtime and external tools

The recorded README charts were rendered with **Matplotlib 3.11.2** through
[`scripts/plot-unity-workspace-comparison.py`](scripts/plot-unity-workspace-comparison.py).
The installed Python package's `METADATA` includes the Matplotlib license agreement
and **Copyright (c) 2012- Matplotlib Development Team; All Rights Reserved**.
See the [Matplotlib project](https://matplotlib.org/). This is an optional chart
authoring dependency; the local Python packages are excluded from the repository
and the Splinter product binaries.

| Project or tool | Use in Splinter | License source |
| --- | --- | --- |
| [.NET](https://github.com/dotnet/runtime) | Product runtime and standard APIs, including Brotli through `System.IO.Compression` | [Runtime MIT license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) and [third party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT); use the notices for the actual runtime being redistributed |
| [Brotli](https://github.com/google/brotli) | Compression implementation reached through .NET; no separate Brotli NuGet package is referenced | [Upstream MIT license](https://github.com/google/brotli/blob/master/LICENSE) and the applicable .NET runtime notices |
| [PowerShell](https://github.com/PowerShell/PowerShell) | CLI wrappers and Windows preparation/validation scripts | [MIT license and upstream component notices](https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt); installed separately |
| [Git](https://git-scm.com/) | Local commit/blob access, workspace registration, and Git result operations | [GPLv2](https://github.com/git/git/blob/master/COPYING); installed separately |
| [Unity](https://unity.com/) and [Unity Version Control / Plastic SCM](https://unity.com/products/version-control) | Optional Editor and installed source-control tools; the recorded Plastic adapter loads the installed client's vendor assemblies | [Unity legal terms](https://unity.com/legal); proprietary installations and access are governed by the vendor's applicable terms |

Windows VSS and other Windows APIs are supplied by the operating system. Optional MCP and Unity CLI tools are selected and installed by the user or agent; their licenses belong to those external installations. Splinter does not bundle their installers or the Unity/Plastic vendor binaries in this source checkout.

## Redistribution boundaries

Keep upstream copyright, license, and any required notice files with the corresponding redistributed components. MIT attribution, Apache 2.0 notices, the WinFsp exception, and the json-everything binary agreement have different conditions; a project link by itself is not a replacement for those terms.

This file records dependency credits for the source checkout. It does not establish a distribution license for Splinter, add WinFsp attribution to CLI output, or certify a binary release package. A binary distribution must carry the licenses and notices for its actual contents and satisfy the applicable WinFsp conditions.
