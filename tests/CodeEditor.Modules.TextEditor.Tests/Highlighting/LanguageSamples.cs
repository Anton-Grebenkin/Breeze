using static CodeEditor.Modules.TextEditor.Tests.Highlighting.Tokens;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Programming language samples: F#, Go, Rust, Kotlin, Ruby, Lua, Swift, Dart.</summary>
internal static class LanguageSamples
{
    public static IEnumerable<(string Id, HighlightingSample Sample)> All()
    {
        yield return ("fsharp", new HighlightingSample("Program.fs", """
            // Program
            module Demo
            open System
            [<EntryPoint>]
            let main argv =
                let name = "World"
                let c = 'x'
                let greet (n: string) = $"Hello {n.ToUpper()}"
                (* block (* nested *) comment *)
                match argv.Length with
                | 0 -> printfn "%s" (greet name)
                | _ -> ()
                0
            type Shape() =
                member this.Area = 1.5
            """)
        {
            Expected =
            [
                Comment("// Program"), Keyword("module"), Keyword("open"), Type("[<EntryPoint>]"), Keyword("let"), String("\"World\""),
                String("'x'"), Type("string"), String("$\"Hello {n.ToUpper()}\""), Text("{n.ToUpper()}"),
                Comment("(* block (* nested *) comment *)"), Control("match"), Control("with"), Number("0"), Keyword("type"),
                Keyword("member"), Function("Area"), Number("1.5"),
            ],
        });

        yield return ("go", new HighlightingSample("main.go", """
            // Package main
            package main
            import "fmt"
            type Point struct { X, Y int }
            func main() {
                p := Point{1, 2}
                s := `raw
            string`
                if p.X > 0 { fmt.Println("x", 'r', 0x1F) }
                defer close(ch)
                return
            }
            """)
        {
            Expected =
            [
                Comment("// Package main"), Keyword("package"), Keyword("import"), String("\"fmt\""), Keyword("type"), Type("Point"),
                Keyword("struct"), Type("int"), Keyword("func"), Function("main"), String("`raw"), String("string`"), Control("if"),
                Function("Println"), String("'r'"), Number("0x1F"), Control("defer"), Function("close"), Control("return"),
            ],
        });

        yield return ("rust", new HighlightingSample("main.rs", """
            // main
            #[derive(Debug, Clone)]
            struct Point { x: i32, y: f64 }
            fn main() {
                let name = "World";
                let raw = r#"C:\path"#;
                let c = 'a';
                let v: Vec<u8> = vec![1, 2, 3];
                println!("{} {}", name, v.len());
                match Some(1_000u32) { Some(n) => {}, None => {} }
            }
            fn longest<'a>(x: &'a str) -> &'a str { x }
            """)
        {
            Expected =
            [
                Comment("// main"), Preprocessor("#[derive(Debug, Clone)]"), Keyword("struct"), Type("Point"), Type("i32"), Type("f64"),
                Keyword("fn"), Function("main"), Keyword("let"), String("\"World\""), String("r#\"C:\\path\"#"), String("'a'"), Type("Vec"),
                Type("u8"), Function("vec!"), Function("println!"), Function("len"), Control("match"), Type("Some"), Number("1_000u32"),
                Type("None"), Type("'a"), Type("str"), Function("longest"),
            ],
        });

        yield return ("kotlin", new HighlightingSample("User.kt", """
            // Kotlin
            package demo
            data class User(val name: String, val age: Int = 0)
            fun greet(user: User): String {
                val list = listOf(1, 2, 3)
                if (user.age > 18) return "Hi ${user.name}!"
                list.map { it * 2 }
                config.open
                return "Name: $name, char: ${'c'}"
            }
            @JvmStatic suspend fun load() = 1.5f
            """)
        {
            Expected =
            [
                Comment("// Kotlin"), Keyword("package"), Keyword("data"), Keyword("class"), Type("User"), Keyword("val"), Type("String"),
                Type("Int"), Number("0"), Keyword("fun"), Function("greet"), Function("listOf"), Control("if"), Control("return"),
                String("\"Hi ${user.name}!\""), Text("${user.name}"), Function("map"), Variable("$name"), String("'c'"),
                Type("@JvmStatic"), Keyword("suspend"), Function("load"), Number("1.5f"),
            ],
            Unexpected = [Keyword("open")],
        });

        yield return ("ruby", new HighlightingSample("greeter.rb", """
            # Ruby
            require 'json'
            =begin
            block comment
            =end
            class Greeter < Base
              attr_reader :name
              def initialize(name, greeting: "Hi")
                @name = name
                @count ||= 0
              end
              def greet!
                return "#{@greeting}, #{name}!" if valid?
                puts %w[a b], /ab+c/i
              end
            end
            sql = <<~SQL
              SELECT * FROM users WHERE name = 'x'
            SQL
            """)
        {
            Expected =
            [
                Comment("# Ruby"), Function("require"), String("'json'"), Comment("=begin"), Comment("block comment"), Comment("=end"),
                Keyword("class"), Type("Greeter"), Type("Base"), Function("attr_reader"), Variable(":name"), Keyword("def"),
                Function("initialize"), Variable("greeting:"), String("\"Hi\""), Variable("@name"), Variable("@count"), Number("0"),
                Keyword("end"), Function("greet!"), Control("return"), String("\"#{@greeting}, #{name}!\""), Text("#{@greeting}"),
                Control("if"), Function("puts"), String("%w[a b]"), String("/ab+c/i"), String("  SELECT * FROM users WHERE name = 'x'"),
            ],
        });

        yield return ("lua", new HighlightingSample("module.lua", """
            -- Lua module
            local M = {}
            --[[ block
            comment ]]
            function M.greet(name)
              local s = "Hello " .. name
              if s ~= nil then print(s) end
              return [[long
            string]], 0x1F
            end
            local x <const> = 10
            """)
        {
            Expected =
            [
                Comment("-- Lua module"), Keyword("local"), Comment("--[[ block"), Comment("comment ]]"), Keyword("function"),
                Function("greet"), String("\"Hello \""), Control("if"), Keyword("nil"), Control("then"), Function("print"), Control("end"),
                Control("return"), String("[[long"), String("string]]"), Number("0x1F"), Keyword("<const>"), Number("10"),
            ],
        });

        yield return ("swift", new HighlightingSample("ContentView.swift", """
            // Swift
            import SwiftUI
            @MainActor struct ContentView: View {
                @State private var count = 0
                var body: some View {
                    Text("Count: \(count + 1)")
                }
                func load() async throws -> [String] {
                    guard let url = URL(string: #"https://x"#) else { return [] }
                    #if DEBUG
                    print(url)
                    #endif
                    return try await fetch(url)
                }
            }
            """)
        {
            Expected =
            [
                Comment("// Swift"), Keyword("import"), Type("SwiftUI"), Type("@MainActor"), Keyword("struct"), Type("ContentView"),
                Type("View"), Type("@State"), Keyword("private"), Keyword("var"), Number("0"), Keyword("some"), Type("Text"),
                String("\"Count: \\(count + 1)\""), Text("\\(count + 1)"), Number("1"), Keyword("func"), Function("load"), Keyword("async"),
                Keyword("throws"), Control("guard"), Keyword("let"), String("#\"https://x\"#"), Control("else"), Control("return"),
                Preprocessor("#if"), Function("print"), Preprocessor("#endif"), Control("try"), Control("await"), Function("fetch"),
            ],
        });

        yield return ("dart", new HighlightingSample("main.dart", """
            // Dart
            import 'package:flutter/material.dart';
            @override
            Widget build(BuildContext context) {
              final name = "World";
              var list = <int>[1, 2];
              print('Hello $name, ${list.length}');
              if (list.isEmpty) return const Text(r'raw\n');
              stream.on;
              return Container();
            }
            """)
        {
            Expected =
            [
                Comment("// Dart"), Keyword("import"), String("'package:flutter/material.dart'"), Type("@override"), Type("Widget"),
                Function("build"), Type("BuildContext"), Keyword("final"), String("\"World\""), Keyword("var"), Type("int"), Number("1"),
                Function("print"), String("'Hello $name, ${list.length}'"), Variable("$name"), Text("${list.length}"), Control("if"),
                Control("return"), Keyword("const"), Type("Text"), String("r'raw\\n'"), Type("Container"),
            ],
            Unexpected = [Control("on")],
        });
    }
}
