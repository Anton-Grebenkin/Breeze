using static CodeEditor.Modules.TextEditor.Tests.Highlighting.Tokens;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Config and data samples: YAML, Dockerfile, the INI family, TOML, ignore files, logs.</summary>
internal static class ConfigSamples
{
    public static IEnumerable<(string Id, HighlightingSample Sample)> All()
    {
        yield return ("yaml", new HighlightingSample("deploy.yaml", """
            # deployment
            apiVersion: apps/v1
            kind: "Deployment"
            metadata:
              name: web # inline
              labels: &labels
                app: web
            spec:
              replicas: 3
              enabled: true
              ratio: 0.5
              description: |
                Don't panic: this is text
                key: not a key
              steps:
                - name: 'Build'
                  run: dotnet build
                - *labels
            """)
        {
            Expected =
            [
                Comment("# deployment"), Comment("# inline"), Tag("apiVersion"), String("apps/v1"), Tag("kind"), String("\"Deployment\""),
                Tag("metadata"), Tag("name"), String("web "), Type("&labels"), Tag("labels"), Tag("app"), Tag("replicas"), Number("3"),
                Keyword("true"), Number("0.5"), Tag("description"), Keyword("|"), String("    Don't panic: this is text"),
                String("    key: not a key"), Tag("steps"), String("'Build'"), Tag("run"), String("dotnet build"), Type("*labels"),
            ],
            Unexpected = [Tag("key")],
        });

        yield return ("dockerfile", new HighlightingSample("Dockerfile", """
            # syntax=docker/dockerfile:1
            # Build stage
            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
            ARG CONFIG=Release
            ENV PATH="/app:${PATH}"
            RUN apt-get update \
                # install tools
                && apt-get install -y curl
            COPY --from=build /src /app
            EXPOSE 8080/tcp
            CMD ["dotnet", "app.dll"]
            """)
        {
            Expected =
            [
                Preprocessor("# syntax=docker/dockerfile:1"), Comment("# Build stage"), Keyword("FROM"), Keyword("AS"), Keyword("ARG"),
                Keyword("ENV"), String("\"/app:${PATH}\""), Variable("${PATH}"), Keyword("RUN"), Comment("    # install tools"),
                Keyword("COPY"), Keyword("EXPOSE"), Number("8080/tcp"), Keyword("CMD"), String("\"dotnet\""),
            ],
        });

        yield return ("ini", new HighlightingSample("settings.ini", """
            ; settings
            [server]
            host = example.com ; inline
            port = 8080
            debug = true
            path = "C:\data"
            """)
        {
            Expected =
            [
                Comment("; settings"), Type("[server]"), Variable("host"), Comment("; inline"), Variable("port"), Number("8080"),
                Keyword("true"), String("\"C:\\data\""),
            ],
        });

        yield return ("editorconfig", new HighlightingSample(".editorconfig", """
            # top-most file
            root = true
            [*.{cs,vb}]
            indent_size = 4
            dotnet_diagnostic.IDE0005.severity = warning
            """)
        {
            Expected =
            [
                Comment("# top-most file"), Variable("root"), Keyword("true"), Type("[*.{cs,vb}]"), Number("4"),
                Variable("dotnet_diagnostic.IDE0005.severity"),
            ],
        });

        yield return ("dotenv", new HighlightingSample(".env.local", """
            # secrets
            export API_URL=https://api.example.com
            TOKEN="abc ${API_URL}"
            """)
        {
            Expected = [Comment("# secrets"), Keyword("export"), Variable("API_URL"), Variable("TOKEN"), String("\"abc ${API_URL}\""), Variable("${API_URL}")],
        });

        yield return ("properties", new HighlightingSample("app.properties", """
            # app
            ! legacy comment
            app.name=Demo
            app.port: 8080
            app.debug=false
            """)
        {
            Expected = [Comment("# app"), Comment("! legacy comment"), Variable("app.name"), Variable("app.port"), Number("8080"), Keyword("false")],
        });

        yield return ("toml", new HighlightingSample("Cargo.toml", """
            # Cargo
            [package]
            name = "demo"
            version = '1.0.0'
            edition = 2021
            [dependencies]
            serde = { version = "1", features = ["derive"] }
            [[bin]]
            created = 1979-05-27T07:32:00Z
            enabled = false
            """)
        {
            Expected =
            [
                Comment("# Cargo"), Type("[package]"), Variable("name"), String("\"demo\""), String("'1.0.0'"), Number("2021"),
                Variable("serde"), Variable("version"), Variable("features"), Type("[[bin]]"), Number("1979-05-27T07:32:00Z"),
                Keyword("false"),
            ],
        });

        yield return ("ignore", new HighlightingSample(".gitignore", """
            # build output
            bin/
            **/obj/*.cache
            !keep.txt
            log?.txt
            """)
        {
            Expected = [Comment("# build output"), Keyword("**"), Keyword("*"), Control("!"), Keyword("?")],
        });

        yield return ("gitattributes", new HighlightingSample(".gitattributes", """
            # line endings
            * text=auto
            *.png binary
            """)
        {
            Expected = [Comment("# line endings"), Keyword("*"), Attribute("text=auto"), Attribute("binary")],
        });

        yield return ("log", new HighlightingSample("app.log", """
            2024-05-01 12:00:01.123 INFO Server started on port 8080
            2024-05-01 12:00:02 WARN Disk "C:" is almost full
            [12:00:03 ERR] Request failed: System.InvalidOperationException: boom
               at App.Program.Main() in Program.cs:line 42
            fail: Microsoft.AspNetCore[1]
            dbug: cache hit 3fa85f64-5717-4562-b3fc-2c963f66afa6
            """)
        {
            Expected =
            [
                Comment("2024-05-01 12:00:01.123"), Success("INFO"), Number("8080"), Warning("WARN"), String("\"C:\""), Comment("12:00:03"),
                Error("ERR"), Error("InvalidOperationException"), Preprocessor("   at App.Program.Main() in Program.cs:line 42"),
                Error("fail:"), Keyword("dbug:"), Number("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            ],
        });
    }
}
