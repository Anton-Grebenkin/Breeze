using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// "Pick an element" on a page: the element under the mouse is outlined, a click picks it and Escape cancels. The
/// answer comes back as a web message that carries a one-time token made for this pick, so a page cannot send a pick of
/// its own: a message without the pending token is ignored. A password is never part of the picked text.
/// </summary>
public static class BrowserPicker
{
    /// <summary>The message field that carries the token.</summary>
    public const string TokenField = "breezePick";

    public const int MaxText = 200;
    public const int MaxHtml = 800;

    /// <summary>Starts picking; the page posts <c>{ breezePick, tag, text, selector, html }</c> or <c>{ breezePick, cancelled }</c>.</summary>
    public static string Start(string token) => $$"""
        (token => {
          if (window.__breezePick) window.__breezePick();
          const box = document.createElement('div');
          box.style.cssText = 'position:fixed;z-index:2147483647;pointer-events:none;border:2px solid #37bbc1;background:rgba(55,187,193,.15);border-radius:2px;left:0;top:0;width:0;height:0';
          document.documentElement.appendChild(box);
          const under = e => { const el = (e.composedPath && e.composedPath()[0]) || e.target; return el instanceof Element ? el : null; };
          const move = e => {
            const el = under(e);
            if (!el) return;
            const r = el.getBoundingClientRect();
            Object.assign(box.style, { left: r.left + 'px', top: r.top + 'px', width: r.width + 'px', height: r.height + 'px' });
          };
          const selector = el => {
            const parts = [];
            for (let node = el; node && node.nodeType === 1 && parts.length < 6; node = node.parentElement) {
              if (node.id) { parts.unshift('#' + CSS.escape(node.id)); break; }
              const tag = node.tagName.toLowerCase();
              const same = node.parentElement ? [...node.parentElement.children].filter(child => child.tagName === node.tagName) : [];
              parts.unshift(same.length > 1 ? `${tag}:nth-of-type(${same.indexOf(node) + 1})` : tag);
            }
            return parts.join(' > ');
          };
          const stop = () => {
            removeEventListener('mousemove', move, true);
            removeEventListener('click', click, true);
            removeEventListener('keydown', key, true);
            box.remove();
            window.__breezePick = null;
          };
          const send = data => window.chrome.webview.postMessage(Object.assign({ {{TokenField}}: token }, data));
          const click = e => {
            e.preventDefault();
            e.stopImmediatePropagation();
            const el = under(e);
            stop();
            if (!el) { send({ cancelled: true }); return; }
            const text = el.innerText || (el.type === 'password' ? '' : el.value) || el.getAttribute('aria-label') || '';
            send({ tag: el.tagName.toLowerCase(), text: text.replace(/\s+/g, ' ').trim().slice(0, {{MaxText}}), selector: selector(el), html: el.outerHTML.slice(0, {{MaxHtml}}) });
          };
          const key = e => { if (e.key === 'Escape') { e.preventDefault(); stop(); send({ cancelled: true }); } };
          addEventListener('mousemove', move, true);
          addEventListener('click', click, true);
          addEventListener('keydown', key, true);
          window.__breezePick = stop;
        })({{JsonSerializer.Serialize(token)}})
        """;

    /// <summary>Stops a pick that is still going on (the user changed their mind in the editor).</summary>
    public static string Stop { get; } = "window.__breezePick && window.__breezePick()";

    /// <summary>The picked element from the page's message; <c>null</c> if the message is not the answer to <paramref name="token"/>.</summary>
    /// <param name="cancelled">The user cancelled with Escape.</param>
    public static BrowserPickedElement? Parse(string json, string token, out bool cancelled)
    {
        cancelled = false;
        try
        {
            using var message = JsonDocument.Parse(json);
            var root = message.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, TokenField) != token)
            {
                return null;
            }

            cancelled = root.TryGetProperty("cancelled", out var flag) && flag.ValueKind == JsonValueKind.True;
            return cancelled ? null : new BrowserPickedElement(Text(root, "tag"), Text(root, "text"), Text(root, "selector"), Text(root, "html"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
