# Сторонние компоненты

## Codicons

Значки интерфейса — [Codicons](https://github.com/microsoft/vscode-codicons) 0.0.45 © Microsoft Corporation.
Значки распространяются по лицензии [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/),
исходный код набора — по лицензии MIT. В сборку `CodeEditor.UI` включены без изменений файлы `codicon.ttf`
(шрифт) и `codicon.csv` (таблица имён).

## Inter

Шрифт интерфейса и чата — [Inter](https://github.com/rsms/inter) 4.1 © 2016 The Inter Project Authors. Распространяется
по лицензии [SIL Open Font License 1.1](https://openfontlicense.org); полный текст — в файле
`src/Platform/CodeEditor.UI/Fonts/Inter-OFL.txt`. В сборку `CodeEditor.UI` без изменений включены статические
начертания из официального выпуска: `Inter-Regular.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf`.

## Mermaid

Схемы рисует [Mermaid](https://github.com/mermaid-js/mermaid) 11.17.2 — лицензия MIT. В сборку
`CodeEditor.Modules.Diagrams.Wpf` (папка `Diagrams` рядом с программой) без изменений входит файл `mermaid.min.js` из
пакета npm `mermaid@11.17.2`. В нём собраны библиотеки, от которых зависит Mermaid, — среди них d3 (ISC), cytoscape
(MIT), lodash-es (MIT) и DOMPurify (Apache-2.0 или MPL-2.0); их уведомления сохранены в конце файла.

```text
The MIT License (MIT)

Copyright (c) 2014 - 2022 Knut Sveidqvist

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Документы: PDF, Word, Excel, PowerPoint

Модуль «Документы» (ADR 0034) читает и пишет документы библиотеками из NuGet; их сборки входят в поставку без
изменений и загружаются при первом открытии документа.

| Пакет | Версия | Лицензия | Правообладатель | Проект |
| --- | --- | --- | --- | --- |
| DocumentFormat.OpenXml, DocumentFormat.OpenXml.Framework | 3.5.1 | MIT | © Microsoft Corporation | https://github.com/dotnet/Open-XML-SDK |
| PdfPig | 0.1.16 | Apache-2.0 | UglyToad и участники проекта | https://github.com/UglyToad/PdfPig |
| PDFsharp, PDFsharp-MigraDoc | 6.2.4 | MIT | © 2026 empira | https://docs.pdfsharp.net/ |

## Платформа и библиотеки

Сборки из NuGet входят в поставку без изменений.

| Пакет | Лицензия | Правообладатель | Проект |
| --- | --- | --- | --- |
| .NET Runtime и Windows Desktop Runtime (входят в самодостаточную сборку) | MIT | © .NET Foundation и участники | https://github.com/dotnet/runtime |
| Microsoft.Extensions.* (Hosting, DependencyInjection, Logging, Options, Configuration, AI) | MIT | © .NET Foundation и участники | https://github.com/dotnet/extensions |
| CommunityToolkit.Mvvm | MIT | © .NET Foundation и участники | https://github.com/CommunityToolkit/dotnet |
| AvalonEdit | MIT | © AvalonEdit Contributors | https://github.com/icsharpcode/AvalonEdit |
| Markdig | BSD-2-Clause | © Alexandre Mutel | https://github.com/xoofx/markdig |
| Microsoft.Web.WebView2 | BSD-3-Clause | © Microsoft Corporation | https://aka.ms/webview |
| Microsoft.Agents.AI, Microsoft.Agents.AI.Workflows | MIT | © Microsoft Corporation | https://github.com/microsoft/agent-framework |
| OpenAI | MIT | © OpenAI | https://github.com/openai/openai-dotnet |
| Microsoft.ML.Tokenizers | MIT | © .NET Foundation и участники | https://github.com/dotnet/machinelearning |
| OpenTelemetry.Api | Apache-2.0 | © OpenTelemetry Authors | https://github.com/open-telemetry/opentelemetry-dotnet |
| Google.Protobuf | BSD-3-Clause | © Google Inc. | https://github.com/protocolbuffers/protobuf |
| System.Security.Cryptography.ProtectedData | MIT | © .NET Foundation и участники | https://github.com/dotnet/runtime |
| Velopack (библиотека и программа обновления `Update.exe`) | MIT | © Velopack Ltd. | https://github.com/velopack/velopack |

Шрифт редактора Cascadia Mono в поставку не входит: Breeze берёт его из Windows.

Тексты лицензий: [MIT](https://opensource.org/license/mit), [Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0),
[BSD-2-Clause](https://opensource.org/license/bsd-2-clause), [BSD-3-Clause](https://opensource.org/license/bsd-3-clause).
