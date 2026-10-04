using static CodeEditor.Modules.TextEditor.Tests.Highlighting.Tokens;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Web samples: JavaScript, TypeScript, JSX.</summary>
internal static class WebSamples
{
    public static IEnumerable<(string Id, HighlightingSample Sample)> All()
    {
        yield return ("javascript", new HighlightingSample("app.js", """
            // line comment
            /* block */
            import { readFile } from 'fs';
            const name = "Ann", re = /a\/b[/]+/gi, cache = new Map();
            const greeting = `Hi ${name.toUpperCase()}!`;
            export default class Greeter extends Base {
              get size() { return this.items.length; }
              async run() {
                if (ready) { await fetch(url).catch(() => null); }
                return { type: 0x1F, total: 1_000.5 / 2 };
              }
            }
            """)
        {
            Expected =
            [
                Comment("// line comment"), Comment("/* block */"), Control("import"), Control("from"), String("'fs'"),
                Keyword("const"), String("\"Ann\""), String("/a\\/b[/]+/gi"), Keyword("new"), Type("Map"), String("`Hi ${name.toUpperCase()}!`"),
                Text("${name.toUpperCase()}"), Keyword("${"), Function("toUpperCase"), Control("export"), Keyword("class"),
                Keyword("extends"), Type("Greeter"), Type("Base"), Keyword("get"), Keyword("this"), Keyword("async"),
                Function("run"), Control("if"), Control("await"), Function("fetch"), Function("catch"), Keyword("null"),
                Control("return"), Number("0x1F"), Number("1_000.5"), Number("2"),
            ],
            Unexpected = [Control("catch"), String("/ 2")],
        });

        yield return ("typescript", new HighlightingSample("shape.ts", """
            // TypeScript
            interface Point { readonly x: number; label?: string }
            type Id = string | undefined;
            enum Color { Red, Green }
            export abstract class Shape implements Drawable {
              private readonly id: Id = 'a';
              area(): number { return this.sizes.declare ?? 42; }
            }
            const options = { namespace: 1 };
            """)
        {
            Expected =
            [
                Comment("// TypeScript"), Keyword("interface"), Keyword("readonly"), Type("number"), Keyword("type"), Type("string"),
                Keyword("undefined"), Keyword("enum"), Keyword("abstract"), Keyword("implements"), Keyword("private"),
                String("'a'"), Number("42"), Type("Point"), Type("Shape"), Type("Drawable"), Control("return"),
            ],
            Unexpected = [Keyword("declare"), Keyword("namespace")],
        });

        yield return ("typescriptreact", new HighlightingSample("App.tsx", """
            // component
            export function App({ items }: Props) {
              const list = useState<string[]>([]);
              return (
                <div className="app" onClick={() => setOpen(!open)}>
                  <p>Don't panic</p>
                  {items.map(item => <Item key={item.id} {...item} />)}
                  <>
                    <br />
                  </>
                </div>
              );
            }
            """)
        {
            Expected =
            [
                Comment("// component"), Control("export"), Keyword("function"), Function("App"), Keyword("const"), Function("useState"),
                Control("return"), Tag("<div"), Attribute("className"), String("\"app\""), Attribute("onClick"), Keyword("{"),
                Tag(">"), Tag("<p>"), Tag("</p>"), Tag("<Item"), Attribute("key"), Tag("/>"), Tag("<>"), Tag("<br"), Tag("</>"),
                Tag("</div>"),
            ],
            Unexpected = [String("panic"), Tag("<string")],
        });

        yield return ("javascriptreact", new HighlightingSample("App.jsx", """
            /* JSX */
            const App = () => <main title='x'>{'text'}</main>;
            """)
        {
            Expected = [Comment("/* JSX */"), Keyword("const"), Tag("<main"), Attribute("title"), String("'x'"), String("'text'"), Tag("</main>")],
        });
    }
}
