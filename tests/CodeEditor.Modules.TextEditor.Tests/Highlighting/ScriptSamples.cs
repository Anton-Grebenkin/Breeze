using static CodeEditor.Modules.TextEditor.Tests.Highlighting.Tokens;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Script and build samples: Shell, Batch, Makefile, CMake.</summary>
internal static class ScriptSamples
{
    // Makefile recipe lines start with a tab.
    private const string Makefile =
        "# Build settings\n" +
        "CC := gcc\n" +
        "CFLAGS ?= -O2 -Wall\n" +
        "SRC = $(wildcard src/*.c)\n" +
        "OBJ = $(patsubst %.c,%.o,$(SRC))\n" +
        ".PHONY: all clean\n" +
        "all: app\n" +
        "app: $(OBJ)\n" +
        "\t$(CC) $(CFLAGS) -o $@ $^\n" +
        "\t@echo \"Built $@\"\n" +
        "ifeq ($(OS),Windows_NT)\n" +
        "\tRM = del\n" +
        "endif\n";

    public static IEnumerable<(string Id, HighlightingSample Sample)> All()
    {
        yield return ("shellscript", new HighlightingSample("deploy.sh", """
            #!/usr/bin/env bash
            # deploy script
            set -euo pipefail
            NAME="world"
            greet() {
              local who=${1:-$NAME}
              echo "Hello, ${who}! Today is $(date +%A)"
            }
            if [ -f "$HOME/.bashrc" ]; then
              for f in *.sh; do greet "$f"; done
            fi
            cat <<EOF
            Don't stop: $NAME
            EOF
            exit 0
            """)
        {
            Expected =
            [
                Preprocessor("#!/usr/bin/env bash"), Comment("# deploy script"), Function("set"), Variable("NAME"), String("\"world\""),
                Function("greet"), Keyword("local"), Variable("who"), Variable("${1:-$NAME}"), Function("echo"),
                String("\"Hello, ${who}! Today is $(date +%A)\""), Variable("${who}"), Text("$(date +%A)"), Control("if"),
                Control("then"), Control("for"), Control("in"), Control("do"), Control("done"), Control("fi"), String("<<EOF"),
                String("Don't stop: $NAME"), Variable("$NAME"), Control("exit"), Number("0"),
            ],
        });

        yield return ("bat", new HighlightingSample("build.cmd", """
            @echo off
            REM build script
            :: another comment
            setlocal enabledelayedexpansion
            set "CONFIG=Release"
            if not exist "%~dp0bin" mkdir "%~dp0bin"
            for %%f in (*.txt) do echo %%f
            call :build !CONFIG!
            goto :eof
            :build
            echo Building %1
            exit /b 0
            """)
        {
            Expected =
            [
                Keyword("echo"), Keyword("off"), Comment("REM build script"), Comment(":: another comment"), Keyword("setlocal"),
                Keyword("enabledelayedexpansion"), Keyword("set"), String("\"CONFIG=Release\""), Control("if"), Keyword("not"),
                Keyword("exist"), Variable("%~dp0"), Control("for"), Variable("%%f"), Control("in"), Control("do"), Control("call"),
                Function(":build"), Variable("!CONFIG!"), Control("goto"), Function(":eof"), Variable("%1"), Control("exit"), Number("0"),
            ],
        });

        yield return ("makefile", new HighlightingSample("Makefile", Makefile)
        {
            Expected =
            [
                Comment("# Build settings"), Variable("CC"), Variable("CFLAGS"), Variable("SRC"), Function("wildcard"),
                Function("patsubst"), Variable("$("), Keyword(".PHONY"), Function("all"), Function("app"), Variable("$@"),
                Variable("$^"), String("\"Built $@\""), Control("ifeq"), Variable("$(OS)"), Control("endif"),
            ],
            Unexpected = [Variable("RM"), Function("RM")],
        });

        yield return ("cmake", new HighlightingSample("CMakeLists.txt", """
            # Project
            cmake_minimum_required(VERSION 3.20)
            project(Demo LANGUAGES CXX)
            set(SOURCES main.cpp "util.cpp")
            if(WIN32 AND NOT MSVC)
              message(STATUS "Path: ${CMAKE_SOURCE_DIR}")
            endif()
            target_link_libraries(app PRIVATE $<$<CONFIG:Debug>:dbg>)
            """)
        {
            Expected =
            [
                Comment("# Project"), Function("cmake_minimum_required"), Keyword("VERSION"), Number("3.20"), Function("project"),
                Keyword("LANGUAGES"), Function("set"), String("\"util.cpp\""), Control("if"), Keyword("AND"), Keyword("NOT"),
                Function("message"), String("\"Path: ${CMAKE_SOURCE_DIR}\""), Variable("${CMAKE_SOURCE_DIR}"), Control("endif"),
                Keyword("PRIVATE"), Preprocessor("$<$<CONFIG:Debug>:dbg>"),
            ],
            Unexpected = [Keyword("SOURCES")],
        });
    }
}
