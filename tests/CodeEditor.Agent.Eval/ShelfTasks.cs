namespace CodeEditor.Agent.Eval;

/// <summary>
/// Tasks on folder settings (ADR 0039) on the Calc repository: a rule from <c>.breeze/rules</c> must reach the model
/// with the file it edits, and a tool from the shelf must be found in the context and called with <c>run_tool</c>.
/// </summary>
internal static class ShelfTasks
{
    public static IReadOnlyList<EvalTask> All() =>
    [
        new("file-rule", "Добавь в Calculator метод Mod(double a, double b) — остаток от деления; при b = 0 — DivideByZeroException. Тесты не нужны.")
        {
            Files =
            [
                (".breeze/rules/calculator.md", """
                    ---
                    applies: "src/**/Calculator.cs"
                    description: Error messages of the calculator
                    ---
                    Every exception thrown in Calculator has a message that starts with the error code prefix "CALC-E: ",
                    for example "CALC-E: division by zero". The UI log parser relies on this prefix.
                    """),
            ],
            HiddenTests = "ModRuleHiddenTests.cs",
        },
        new("tool-shelf", "Какой номер сборки сообщает инструмент проекта build-info? Ответь номером.")
        {
            Files =
            [
                (".breeze/tools/build-info/tool.md", """
                    ---
                    description: Prints the build number of the calculator
                    ---
                    """),
                (".breeze/tools/build-info/run.ps1", "Write-Output \"Calc build 7731\"\n"),
            ],
            AnswerMustContain = "7731",
        },
    ];
}
