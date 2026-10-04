using System.Collections.Immutable;

namespace CodeEditor.Modules.TextEditor.Languages;

/// <summary>
/// The language table (ADR 0036). Ids and names follow VS Code. Highlighting is built into AvalonEdit ("C#", "XML",
/// "Json"…) or custom (a file in <c>Highlighting/Definitions</c> of <c>TextEditor.Wpf</c>); a related format without
/// its own definition borrows the closest one: Less uses CSS, Vue uses HTML.
/// </summary>
internal static class KnownLanguages
{
    public static ImmutableArray<Language> All { get; } =
    [
        // .NET
        new("csharp", "C#", "C#") { Extensions = [".cs", ".csx"], Aliases = ["c#"] },
        new("fsharp", "F#", "FSharp") { Extensions = [".fs", ".fsi", ".fsx"], Aliases = ["f#"] },
        new("vb", "Visual Basic", "VB") { Extensions = [".vb"], Aliases = ["vbnet"] },
        new("xaml", "XAML", "XML") { Extensions = [".xaml", ".axaml"] },
        new("razor", "Razor", "ASP/XHTML") { Extensions = [".cshtml", ".razor"] },
        new("aspnet", "ASP.NET", "ASP/XHTML") { Extensions = [".asp", ".aspx", ".asax", ".asmx", ".ascx", ".master"] },
        new("xml", "XML", "XML")
        {
            Extensions =
            [
                ".xml", ".xsl", ".xslt", ".xsd", ".manifest", ".config", ".xshd", ".proj", ".csproj", ".vbproj", ".fsproj",
                ".props", ".targets", ".slnx", ".resx", ".nuspec", ".wsdl", ".svg", ".ruleset", ".runsettings",
            ],
            Aliases = ["msbuild"],
        },

        // Web
        new("javascript", "JavaScript", "JavaScript") { Extensions = [".js", ".mjs", ".cjs"] },
        new("javascriptreact", "JavaScript JSX", "Jsx") { Extensions = [".jsx"] },
        new("typescript", "TypeScript", "TypeScript") { Extensions = [".ts", ".mts", ".cts"] },
        new("typescriptreact", "TypeScript JSX", "Jsx") { Extensions = [".tsx"] },
        new("html", "HTML", "HTML") { Extensions = [".html", ".htm", ".xhtml"] },
        new("vue", "Vue", "HTML") { Extensions = [".vue"] },
        new("svelte", "Svelte", "HTML") { Extensions = [".svelte"] },
        new("css", "CSS", "CSS") { Extensions = [".css"] },
        new("scss", "SCSS", "CSS") { Extensions = [".scss"] },
        new("less", "Less", "CSS") { Extensions = [".less"] },
        new("json", "JSON", "Json") { Extensions = [".json", ".webmanifest"], FileNames = [".babelrc", ".eslintrc", ".prettierrc"] },
        new("jsonc", "JSON with Comments", "Json") { Extensions = [".jsonc"] },
        new("json5", "JSON5", "Json") { Extensions = [".json5"] },
        new("jsonl", "JSON Lines", "Json") { Extensions = [".jsonl", ".ndjson"] },
        new("graphql", "GraphQL", "GraphQL") { Extensions = [".graphql", ".gql", ".graphqls"] },
        new("http", "HTTP", "Http") { Extensions = [".http", ".rest"] },

        // Documents
        new("markdown", "Markdown", "MarkDown") { Extensions = [".md", ".markdown"] },
        new("mdx", "MDX", "MarkDown") { Extensions = [".mdx"] },
        new("mermaid", "Mermaid", "Mermaid") { Extensions = [".mmd", ".mermaid"] },
        new("latex", "LaTeX", "TeX") { Extensions = [".tex"] },
        new("log", "Log", "Log") { Extensions = [".log"] },

        // Programming languages
        new("c", "C", "C++") { Extensions = [".c"] },
        new("cpp", "C++", "C++") { Extensions = [".cpp", ".cc", ".cxx", ".h", ".hh", ".hpp", ".hxx", ".ino"], Aliases = ["c++"] },
        new("java", "Java", "Java") { Extensions = [".java"] },
        new("groovy", "Groovy", "Java") { Extensions = [".groovy", ".gradle"], FileNames = ["Jenkinsfile"] },
        new("kotlin", "Kotlin", "Kotlin") { Extensions = [".kt", ".kts"] },
        new("go", "Go", "Go") { Extensions = [".go"], Aliases = ["golang"] },
        new("rust", "Rust", "Rust") { Extensions = [".rs"] },
        new("swift", "Swift", "Swift") { Extensions = [".swift"] },
        new("dart", "Dart", "Dart") { Extensions = [".dart"] },
        new("python", "Python", "Python") { Extensions = [".py", ".pyw", ".pyi"] },
        new("ruby", "Ruby", "Ruby")
        {
            Extensions = [".rb", ".rake", ".gemspec", ".ru"],
            FileNames = ["Gemfile", "Rakefile", "Podfile", "Vagrantfile", "Guardfile", "Brewfile", "Fastfile"],
        },
        new("lua", "Lua", "Lua") { Extensions = [".lua"] },
        new("php", "PHP", "PHP") { Extensions = [".php"] },
        new("sql", "SQL", "TSQL") { Extensions = [".sql"], Aliases = ["tsql"] },
        new("proto", "Protocol Buffers", "Protobuf") { Extensions = [".proto"], Aliases = ["protobuf"] },
        new("boo", "Boo", "Boo") { Extensions = [".boo"] },
        new("coco", "Coco", "Coco") { Extensions = [".atg"] },

        // Scripts and build
        new("shellscript", "Shell Script", "Shell")
        {
            Extensions = [".sh", ".bash", ".zsh", ".ksh"],
            FileNames = [".bashrc", ".bash_profile", ".bash_aliases", ".bash_logout", ".profile", ".zshrc", ".zprofile", ".zshenv", ".envrc", "PKGBUILD"],
            Aliases = ["shell"],
        },
        new("powershell", "PowerShell", "PowerShell") { Extensions = [".ps1", ".psm1", ".psd1"], Aliases = ["pwsh"] },
        new("bat", "Batch", "Batch") { Extensions = [".bat", ".cmd"], Aliases = ["batch"] },
        new("dockerfile", "Dockerfile", "Dockerfile")
        {
            FileNames = ["Dockerfile", "Containerfile"],
            FilePatterns = ["Dockerfile.*", "Containerfile.*", "*.dockerfile", "*.containerfile"],
            Aliases = ["docker"],
        },
        new("makefile", "Makefile", "Makefile") { Extensions = [".mk", ".mak"], FileNames = ["Makefile", "GNUmakefile"], Aliases = ["make"] },
        new("cmake", "CMake", "CMake") { Extensions = [".cmake"], FileNames = ["CMakeLists.txt"] },
        new("diff", "Diff", "Patch") { Extensions = [".diff", ".patch"] },

        // Settings and data
        new("yaml", "YAML", "Yaml") { Extensions = [".yml", ".yaml"], FileNames = [".clang-format", ".clang-tidy"] },
        new("toml", "TOML", "Toml") { Extensions = [".toml"], FileNames = ["Cargo.lock", "Pipfile", "poetry.lock", "uv.lock"] },
        new("ini", "INI", "Ini") { Extensions = [".ini", ".cfg", ".conf", ".gitconfig", ".gitmodules"], FileNames = [".npmrc", ".pylintrc", ".flake8"] },
        new("editorconfig", "EditorConfig", "Ini") { Extensions = [".editorconfig"] },
        new("properties", "Properties", "Ini") { Extensions = [".properties"] },
        new("dotenv", "Dotenv", "Ini") { Extensions = [".env"], FilePatterns = [".env.*"] },
        new("ignore", "Ignore", "Ignore")
        {
            FileNames = [".gitignore", ".dockerignore", ".npmignore", ".eslintignore", ".prettierignore", ".stylelintignore", ".hgignore", ".vscodeignore"],
        },
        new("gitattributes", "Git Attributes", "Ignore") { FileNames = [".gitattributes"] },
    ];
}
