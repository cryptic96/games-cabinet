# Vendored: ASP.NET Core SignalR browser client

`signalr.min.js` in this directory is Microsoft's official SignalR JavaScript client for browsers, copied unchanged from the published npm package. It lets every open page receive the collection status the moment the server broadcasts it.

| Item | Value |
| --- | --- |
| Package | `@microsoft/signalr` |
| Version | 10.0.11 |
| Source | the npm registry (`https://registry.npmjs.org/`), file `package/dist/browser/signalr.min.js` inside the package tarball |
| Tarball integrity (npm) | `sha512-FulOJ2EEtKvLQcswe/U7v8pzyXyk7Jua5xgWJPPwyQU/2Z9ORvKcVjyO75VvLsem+CJ1ORRexJ+7Bz1FCei2aw==` |
| File SHA-256 | `97e9b97e642a72e5a470917147a2bf79f86cad829a5c6786adb10614d248bb95` |
| File size | 47,668 bytes |
| Upstream repository | `https://github.com/dotnet/aspnetcore` (`src/SignalR/clients/ts/signalr`) |
| Licence | MIT (text below) |

The file is byte-identical to the one in the package: it is not minified, re-encoded or edited here. A git attribute (`-text`) stops line-ending conversion, and a unit test pins the SHA-256 above. To change it, follow `docs/vendored-assets.md`.

The file ends with a reference to a source map (`signalr.min.js.map`). The map is deliberately not shipped, so a browser's developer tools may log a harmless missing-map notice.

## Licence

The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
