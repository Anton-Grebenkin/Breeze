namespace CodeEditor.UI.Themes;

/// <summary>
/// Color token keys. Both color dictionaries (<c>Colors.Dark.xaml</c>, <c>Colors.Light.xaml</c>) define all of them,
/// which an architecture test checks. Markup uses tokens only via <c>DynamicResource</c>.
/// </summary>
public static class ThemeKeys
{
    public const string WindowBackground = "Brush.Window.Background";
    public const string WindowBorder = "Brush.Window.Border";

    public const string CardBorder = "Brush.Card.Border";
    public const string PanelBackground = "Brush.Panel.Background";
    public const string Brand = "Brush.Brand";

    public const string FileIconFolder = "Brush.FileIcon.Folder";
    public const string FileIconCode = "Brush.FileIcon.Code";
    public const string FileIconData = "Brush.FileIcon.Data";
    public const string FileIconDocument = "Brush.FileIcon.Document";
    public const string FileIconMedia = "Brush.FileIcon.Media";
    public const string FileIconTable = "Brush.FileIcon.Table";
    public const string FileIconPdf = "Brush.FileIcon.Pdf";
    public const string FileIconSlides = "Brush.FileIcon.Slides";

    /// <summary>Files with agent edits awaiting review: explorer name and tab title.</summary>
    public const string AgentChangeForeground = "Brush.AgentChange.Foreground";

    public const string TitleBarBackground = "Brush.TitleBar.Background";
    public const string TitleBarForeground = "Brush.TitleBar.Foreground";
    public const string TitleBarInactiveForeground = "Brush.TitleBar.InactiveForeground";

    public const string ActivityBarBackground = "Brush.ActivityBar.Background";
    public const string ActivityBarForeground = "Brush.ActivityBar.Foreground";
    public const string ActivityBarInactiveForeground = "Brush.ActivityBar.InactiveForeground";

    public const string EditorBackground = "Brush.Editor.Background";
    public const string EditorForeground = "Brush.Editor.Foreground";
    public const string EditorSelection = "Brush.Editor.Selection";
    public const string EditorLineHighlight = "Brush.Editor.LineHighlight";
    public const string EditorLineNumber = "Brush.Editor.LineNumber";
    public const string EditorFindMatch = "Brush.Editor.FindMatch";

    /// <summary>Syntax highlighting: VS Code Dark+ and Light+ palettes.</summary>
    public const string SyntaxComment = "Brush.Syntax.Comment";
    public const string SyntaxString = "Brush.Syntax.String";
    public const string SyntaxNumber = "Brush.Syntax.Number";
    public const string SyntaxKeyword = "Brush.Syntax.Keyword";
    public const string SyntaxControlKeyword = "Brush.Syntax.ControlKeyword";
    public const string SyntaxFunction = "Brush.Syntax.Function";
    public const string SyntaxType = "Brush.Syntax.Type";
    public const string SyntaxTag = "Brush.Syntax.Tag";
    public const string SyntaxAttribute = "Brush.Syntax.Attribute";

    /// <summary>Variables and keys: <c>$VAR</c>, <c>{{var}}</c>, JSON, INI and TOML keys (like "variable" in VS Code).</summary>
    public const string SyntaxVariable = "Brush.Syntax.Variable";
    public const string SyntaxPreprocessor = "Brush.Syntax.Preprocessor";

    public const string TabActiveBackground = "Brush.Tab.ActiveBackground";
    public const string TabInactiveBackground = "Brush.Tab.InactiveBackground";
    public const string TabActiveForeground = "Brush.Tab.ActiveForeground";
    public const string TabInactiveForeground = "Brush.Tab.InactiveForeground";
    public const string TabModified = "Brush.Tab.Modified";

    public const string StatusBarBackground = "Brush.StatusBar.Background";
    public const string StatusBarForeground = "Brush.StatusBar.Foreground";

    public const string Border = "Brush.Border";
    public const string Accent = "Brush.Accent";
    public const string AccentForeground = "Brush.Accent.Foreground";
    public const string FocusBorder = "Brush.Focus.Border";

    public const string TextPrimary = "Brush.Text.Primary";
    public const string TextSecondary = "Brush.Text.Secondary";

    public const string ControlHoverBackground = "Brush.Control.HoverBackground";
    public const string ControlPressedBackground = "Brush.Control.PressedBackground";

    public const string InputBackground = "Brush.Input.Background";
    public const string InputBorder = "Brush.Input.Border";

    public const string CaptionCloseHoverBackground = "Brush.Caption.CloseHoverBackground";
    public const string CaptionCloseHoverForeground = "Brush.Caption.CloseHoverForeground";

    public const string KeycapBackground = "Brush.Keycap.Background";
    public const string KeycapBorder = "Brush.Keycap.Border";

    /// <summary>Popups: menus, palette, tooltips.</summary>
    public const string WidgetBackground = "Brush.Widget.Background";
    public const string WidgetBorder = "Brush.Widget.Border";
    public const string WidgetSeparator = "Brush.Widget.Separator";

    public const string ListHoverBackground = "Brush.List.HoverBackground";
    public const string ListSelectionBackground = "Brush.List.SelectionBackground";
    public const string ListSelectionForeground = "Brush.List.SelectionForeground";

    /// <summary>The folder a dragged file would drop into (<see cref="Controls.DropHighlight"/>).</summary>
    public const string ListDropBackground = "Brush.List.DropBackground";

    /// <summary>Characters matching the query in the palette and quick open.</summary>
    public const string MatchHighlightForeground = "Brush.MatchHighlight.Foreground";

    public const string ErrorForeground = "Brush.Error.Foreground";
    public const string WarningForeground = "Brush.Warning.Foreground";

    /// <summary>Success or running state: container up, check passed (like charts.green in VS Code).</summary>
    public const string SuccessForeground = "Brush.Success.Foreground";

    /// <summary>Git file status letters in the panel and history (like gitDecoration.* in VS Code).</summary>
    public const string GitModified = "Brush.Git.Modified";
    public const string GitAdded = "Brush.Git.Added";
    public const string GitDeleted = "Brush.Git.Deleted";
    public const string GitUntracked = "Brush.Git.Untracked";
    public const string GitConflict = "Brush.Git.Conflict";

    public const string BadgeBackground = "Brush.Badge.Background";
    public const string BadgeForeground = "Brush.Badge.Foreground";
    public const string DiffInsertedBackground = "Brush.Diff.InsertedBackground";
    public const string DiffRemovedBackground = "Brush.Diff.RemovedBackground";

    /// <summary>Markdown in agent replies: code blocks and spans, links, quotes (like textPreformat, textLink in VS Code).</summary>
    public const string MarkdownCodeBlockBackground = "Brush.Markdown.CodeBlockBackground";
    public const string MarkdownInlineCodeBackground = "Brush.Markdown.InlineCodeBackground";
    public const string MarkdownInlineCodeForeground = "Brush.Markdown.InlineCodeForeground";
    public const string MarkdownLink = "Brush.Markdown.Link";
    public const string MarkdownQuoteBorder = "Brush.Markdown.QuoteBorder";

    /// <summary>Checkerboard behind transparent images: background and squares (as in VS Code's image preview).</summary>
    public const string CheckerboardBackground = "Brush.Checkerboard.Background";
    public const string CheckerboardSquare = "Brush.Checkerboard.Square";

    public const string ScrollbarThumb = "Brush.Scrollbar.Thumb";
    public const string ScrollbarThumbHover = "Brush.Scrollbar.ThumbHover";
}
