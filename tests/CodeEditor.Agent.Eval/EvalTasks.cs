namespace CodeEditor.Agent.Eval;

/// <summary>
/// Tasks on the Calc repository, from a question to multi-file edits: find a symbol, fix a described bug, repair a
/// failing test, extend the parser, add a function with tests, rename across the solution, fix a bug from its symptom
/// (locale), extract a method; harder ones: operator precedence, full expressions, two bugs at once, invalid input;
/// plus a manually reviewed project overview.
/// </summary>
internal static class EvalTasks
{
    private const string Calculator = "src/Calc/Calculator.cs";
    private const string Parser = "src/Calc/Parser.cs";
    private const string CalculatorTests = "tests/Calc.Tests/CalculatorTests.cs";
    private const string ParserTests = "tests/Calc.Tests/ParserTests.cs";

    /// <param name="divideLine">Line of the <c>Calculator.Divide</c> declaration in the clean repository.</param>
    public static IReadOnlyList<EvalTask> All(int divideLine) =>
    [
        new("find", "В каком файле и на какой строке объявлен метод Calculator.Divide? Ответь ссылкой путь:строка.")
        {
            AnswerMustContain = $"Calculator.cs:{divideLine}",
        },
        new("fix-divide", "Calculator.Divide(1, 0) возвращает 0, а должен бросать DivideByZeroException. Исправь.")
        {
            Seeds =
            [
                (Calculator, "b == 0 ? throw new DivideByZeroException(\"Деление на ноль.\") : a / b", "b == 0 ? 0 : a / b"),
                (CalculatorTests, "    [Fact]\n    public void Divide_ByZero_Throws() => Assert.Throws<DivideByZeroException>(() => Calculator.Divide(1, 0));\n\n", string.Empty),
            ],
            HiddenTests = "DivideHiddenTests.cs",
        },
        new("failing-test", "Тест ParserTests.Parses_Multiplication падает. Найди причину и исправь код; сам тест не меняй.")
        {
            Seeds = [(Parser, "'*' => Calculator.Multiply(left, right)", "'*' => Calculator.Add(left, right)")],
            Unchanged = [ParserTests],
        },
        new("negative-numbers", "Parser.Evaluate не понимает отрицательные числа: «-2 + 3» и «4 * -2» падают с FormatException. Исправь и добавь тесты.")
        {
            HiddenTests = "NegativeHiddenTests.cs",
        },
        new("add-power", "Добавь в Calculator метод Power(double x, int n) — возведение в целую степень, в том числе нулевую и отрицательную, — и тесты к нему.")
        {
            HiddenTests = "PowerHiddenTests.cs",
        },
        new("rename-sub", "Переименуй метод Calculator.Sub в Subtract во всём решении.")
        {
            HiddenTests = "SubtractHiddenTests.cs",
            Forbidden = @"\bSub\(",
        },
        new("add-mod", "Добавь остаток от деления: метод Calculator.Mod(double a, double b) и оператор % в Parser.Evaluate («7 % 3» = 1). Деление на ноль — DivideByZeroException, как у Divide. Добавь тесты.")
        {
            HiddenTests = "ModHiddenTests.cs",
        },
        new("culture-bug", "На компьютере с русской локалью Parser.Evaluate(\"2.5 + 1\") падает с FormatException, а с английской работает. Исправь.")
        {
            Seeds = [(Parser, "double.Parse(text.Trim(), CultureInfo.InvariantCulture)", "double.Parse(text.Trim())")],
            HiddenTests = "CultureHiddenTests.cs",
        },
        new("switch-fix", "На компьютере с русской локалью Parser.Evaluate(\"2.5 + 1\") падает с FormatException, а с английской работает. В чём причина и что поправить?")
        {
            Seeds = [(Parser, "double.Parse(text.Trim(), CultureInfo.InvariantCulture)", "double.Parse(text.Trim())")],
            HiddenTests = "CultureHiddenTests.cs",
            FollowUp = "внеси эту правку",
        },
        new("extract-number", "Вынеси разбор числа из Parser в публичный метод Parser.ParseNumber(string text) — пробелы по краям допустимы, формат инвариантный — и используй его в Evaluate. Поведение не меняй.")
        {
            HiddenTests = "ParseNumberHiddenTests.cs",
        },
        new("precedence", "Научи Parser.Evaluate выражения из нескольких операций с обычным приоритетом: «2 + 3 * 4» = 14, «10 - 4 - 3» = 3, «8 / 2 / 2» = 2. Скобки и отрицательные числа не нужны.")
        {
            HiddenTests = "PrecedenceHiddenTests.cs",
        },
        new("expressions", "Научи Parser.Evaluate полноценные арифметические выражения: приоритет операций, скобки и унарный минус — «(2 + 3) * 4» = 20, «-(1 + 2) * 3» = -9, «2 * -3» = -6. На некорректное выражение — ArgumentException.")
        {
            HiddenTests = "ExpressionsHiddenTests.cs",
        },
        new("two-bugs", "Тесты падают. Найди причины и исправь код; сами тесты не меняй.")
        {
            Seeds =
            [
                (Calculator, "public static double Sub(double a, double b) => a - b;", "public static double Sub(double a, double b) => b - a;"),
                (Calculator, "value * percent / 100", "value * percent / 10"),
            ],
            Unchanged = [CalculatorTests, ParserTests],
        },
        new("invalid-input", "Parser.Evaluate на некорректном вводе («2 +», «abc», пустая строка) должен бросать ArgumentException с понятным сообщением, а не FormatException. Корректные выражения — как раньше.")
        {
            HiddenTests = "InvalidInputHiddenTests.cs",
        },
        new("overview", "Изучи проект и расскажи, как он устроен.")
        {
            ManualReview = true,
        },

        // Web (ADR 0025): the answer isn't in the repository; a person checks the answer and its source.
        new("web-search", "Какая сейчас последняя стабильная версия пакета xunit.v3 на NuGet? Найди в интернете и ответь номером версии со ссылкой на источник.")
        {
            ManualReview = true,
        },
    ];
}
