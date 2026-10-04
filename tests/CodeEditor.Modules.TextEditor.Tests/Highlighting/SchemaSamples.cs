using static CodeEditor.Modules.TextEditor.Tests.Highlighting.Tokens;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>Schema and request samples: HTTP, GraphQL, Protocol Buffers, Mermaid.</summary>
internal static class SchemaSamples
{
    public static IEnumerable<(string Id, HighlightingSample Sample)> All()
    {
        yield return ("http", new HighlightingSample("api.http", """
            @host = https://localhost:5001
            ### Get users
            GET {{host}}/api/users?page=1 HTTP/1.1
            Authorization: Bearer {{token}}
            # comment

            POST {{host}}/api/users HTTP/1.1
            Content-Type: application/json

            {"name": "Ann", "age": 42, "admin": true}
            """)
        {
            Expected =
            [
                Variable("@host"), Comment("### Get users"), Keyword("GET"), Variable("{{host}}"), Keyword("HTTP/1.1"),
                Type("Authorization"), Variable("{{token}}"), Comment("# comment"), Keyword("POST"), Type("Content-Type"),
                String("\"name\""), String("\"Ann\""), Number("42"), Keyword("true"),
            ],
            Unexpected = [Number("5001")],
        });

        yield return ("graphql", new HighlightingSample("schema.graphql", """
            # Get user
            query GetUser($id: ID!) @cached {
              user(id: $id) {
                name
                posts(first: 10) { title }
              }
            }
            type User implements Node {
              "Display name"
              name: String!
            }
            """)
        {
            Expected =
            [
                Comment("# Get user"), Keyword("query"), Function("GetUser"), Variable("$id"), Type("ID"), Function("@cached"),
                Variable("id"), Variable("first"), Number("10"), Keyword("type"), Type("User"), Keyword("implements"), Type("Node"),
                String("\"Display name\""), Variable("name"), Type("String"),
            ],
        });

        yield return ("proto", new HighlightingSample("users.proto", """
            // Users API
            syntax = "proto3";
            package demo.v1;
            import "google/protobuf/timestamp.proto";
            message User {
              string name = 1;
              repeated int32 ids = 2 [packed = true];
              Status status = 3;
            }
            enum Status { STATUS_UNKNOWN = 0; }
            service Users {
              rpc GetUser (GetUserRequest) returns (stream User);
            }
            """)
        {
            Expected =
            [
                Comment("// Users API"), Keyword("syntax"), String("\"proto3\""), Keyword("package"), Keyword("import"), Keyword("message"),
                Type("User"), Type("string"), Number("1"), Keyword("repeated"), Type("int32"), Keyword("true"), Type("Status"),
                Keyword("enum"), Keyword("service"), Keyword("rpc"), Function("GetUser"), Keyword("returns"), Keyword("stream"),
                Type("GetUserRequest"),
            ],
            Unexpected = [Type("STATUS_UNKNOWN")],
        });

        yield return ("mermaid", new HighlightingSample("flow.mmd", """
            %%{init: {"theme": "dark"}}%%
            flowchart TD
                %% comment
                A[Start] -->|yes| B(Process)
                B --> C{Decision}
                subgraph Group
                    D((Circle)) -.-> E
                end
            sequenceDiagram
                Alice->>Bob: Hello "friend"
                loop Every minute
                    Bob-->>Alice: Ping
                end
            """)
        {
            Expected =
            [
                Preprocessor("%%{init: {\"theme\": \"dark\"}}%%"), Keyword("flowchart"), Keyword("TD"), Comment("%% comment"),
                String("[Start]"), Keyword("-->"), String("|yes|"), String("(Process)"), Keyword("subgraph"), String("((Circle))"),
                Keyword("-.->"), Control("end"), Keyword("sequenceDiagram"), Keyword("->>"), String("\"friend\""), Control("loop"),
                Keyword("-->>"),
            ],
        });
    }
}
