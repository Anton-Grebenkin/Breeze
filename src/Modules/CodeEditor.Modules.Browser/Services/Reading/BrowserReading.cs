using System.Globalization;
using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services.Reading;

/// <summary>
/// Page scripts for reading without a snapshot: "find on page" (where a text occurs, with its surroundings and the
/// heading above) and the page's main text as Markdown, like a reader view. Both look into open shadow roots and frames
/// of the same site. Model values enter a script only as JSON strings, so no code can be injected.
/// </summary>
public static class BrowserReading
{
    /// <summary>Places reported by <see cref="Find"/>; further ones are only counted.</summary>
    public const int MaxPlaces = 20;

    /// <summary>Markdown length limit; a longer page is cut with a notice.</summary>
    public const int MaxCharacters = 400_000;

    private const string Roots = """
        const roots = root => { const found = []; root.querySelectorAll('*').forEach(el => { if (el.shadowRoot) found.push(el.shadowRoot); if (el.tagName === 'IFRAME') { try { if (el.contentDocument && el.contentDocument.body) found.push(el.contentDocument.body); } catch { } } }); return found; };
        """;

    /// <summary>
    /// Finds the text, ignoring case: <c>{ total, places: [{ heading, context, count, ref }] }</c>. A place is the text
    /// block the match is in; <c>ref</c> is the nearest element of the last snapshot, to scroll to or click.
    /// </summary>
    public static string Find(string text) => $$"""
        (query => {
          {{Roots}}
          const needle = query.toLowerCase(), places = new Map();
          const clean = s => (s || '').replace(/\s+/g, ' ').trim();
          const blocks = 'p,li,td,th,pre,blockquote,dd,dt,label,h1,h2,h3,h4,h5,h6,summary,figcaption,caption,a,button';
          let total = 0, heading = '';
          const scan = root => {
            const walker = (root.ownerDocument || root).createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
            for (let node = walker.nextNode(); node; node = walker.nextNode()) {
              if (node.nodeType === 1) { if (/^H[1-6]$/.test(node.tagName)) heading = clean(node.innerText).slice(0, 120); continue; }
              const parent = node.parentElement;
              if (!parent || ['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE'].includes(parent.tagName)) continue;
              const value = node.nodeValue.toLowerCase();
              let count = 0;
              for (let at = value.indexOf(needle); at >= 0; at = value.indexOf(needle, at + needle.length)) count++;
              if (count === 0 || (parent.checkVisibility && !parent.checkVisibility())) continue;
              total += count;
              const block = parent.closest(blocks) || parent;
              const known = places.get(block);
              if (known) { known.count += count; continue; }
              if (places.size >= {{MaxPlaces}}) continue;
              const whole = clean(block.innerText || block.textContent), at = whole.toLowerCase().indexOf(needle);
              const from = Math.max(0, at - 100), to = Math.min(whole.length, at + needle.length + 100);
              const marked = parent.closest('[data-agent-ref]');
              places.set(block, { heading, count, context: (from > 0 ? '…' : '') + whole.slice(from, to) + (to < whole.length ? '…' : ''), ref: marked ? marked.getAttribute('data-agent-ref') : null });
            }
            roots(root).forEach(scan);
          };
          if (needle && document.body) scan(document.body);
          return { total, places: [...places.values()] };
        })({{JsonSerializer.Serialize(text)}})
        """;

    /// <summary>
    /// The main text as Markdown: <c>{ title, url, text, truncated }</c>. The main part is the page's single
    /// <c>article</c>, else <c>main</c>, else the body without navigation, header, footer and side blocks.
    /// </summary>
    public static string Markdown { get; } = """
        (() => {
          const max = __MAX__;
          const articles = document.querySelectorAll('article');
          const root = articles.length === 1 ? articles[0] : document.querySelector('main, [role=main]') || document.body;
          const chrome = root === document.body ? new Set(['NAV', 'HEADER', 'FOOTER', 'ASIDE']) : new Set();
          const skip = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'SVG', 'CANVAS', 'BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'HEAD']);
          const block = new Set(['P', 'DIV', 'SECTION', 'ARTICLE', 'MAIN', 'FIGURE', 'FIGCAPTION', 'DETAILS', 'SUMMARY', 'DL', 'DT', 'DD', 'ADDRESS', 'FORM', 'HEADER', 'FOOTER', 'NAV', 'ASIDE']);
          const clean = s => (s || '').replace(/\s+/g, ' ');
          const hidden = el => { if (el.hidden || el.getAttribute('aria-hidden') === 'true') return true; const st = (el.ownerDocument.defaultView || window).getComputedStyle(el); return st.display === 'none' || st.visibility === 'hidden'; };
          const cell = el => clean(inline(el, 0)).trim().replace(/\|/g, '\\|');
          const list = (el, depth, ordered) => [...el.children].filter(item => item.tagName === 'LI' && !hidden(item)).map((item, index) =>
            '  '.repeat(depth) + (ordered ? (index + 1) + '. ' : '- ') + convert(item, depth + 1).trim().replace(/\n{2,}/g, '\n')).join('\n');
          const table = el => {
            const rows = [...el.rows].filter(row => !hidden(row)).map(row => '| ' + [...row.cells].map(cell).join(' | ') + ' |');
            if (rows.length > 0) rows.splice(1, 0, '| ' + [...el.rows[0].cells].map(() => '---').join(' | ') + ' |');
            return rows.join('\n');
          };
          const inline = (el, depth) => [...el.childNodes].map(node => convert(node, depth)).join('');
          const convert = (node, depth) => {
            if (node.nodeType === 3) return clean(node.nodeValue);
            if (node.nodeType !== 1 && node.nodeType !== 11) return '';
            const name = node.nodeType === 11 ? '' : node.tagName.toUpperCase();
            if (name && (skip.has(name) || chrome.has(name) || hidden(node))) return '';
            if (name === 'IFRAME') { try { return node.contentDocument && node.contentDocument.body ? '\n\n' + convert(node.contentDocument.body, depth) + '\n\n' : ''; } catch { return ''; } }
            if (/^H[1-6]$/.test(name)) return '\n\n' + '#'.repeat(+name[1]) + ' ' + clean(inline(node, depth)).trim() + '\n\n';
            if (name === 'BR') return '\n';
            if (name === 'HR') return '\n\n---\n\n';
            if (name === 'PRE') return '\n\n```\n' + node.innerText.replace(/\n+$/, '') + '\n```\n\n';
            if (name === 'UL' || name === 'OL') return '\n\n' + list(node, depth, name === 'OL') + '\n\n';
            if (name === 'TABLE') return '\n\n' + table(node) + '\n\n';
            if (name === 'IMG') return node.alt ? '[image: ' + clean(node.alt).trim() + ']' : '';
            const inner = (node.shadowRoot ? convert(node.shadowRoot, depth) : '') + inline(node, depth);
            if (name === 'A') { const text = clean(inner).trim(); return text && node.href && /^https?:/.test(node.href) ? '[' + text + '](' + node.href + ')' : text; }
            if (name === 'STRONG' || name === 'B') return inner.trim() ? '**' + inner.trim() + '**' : '';
            if (name === 'EM' || name === 'I') return inner.trim() ? '*' + inner.trim() + '*' : '';
            if (name === 'CODE') return inner.trim() ? '`' + inner.trim() + '`' : '';
            if (name === 'BLOCKQUOTE') return '\n\n' + inner.trim().split('\n').map(line => '> ' + line).join('\n') + '\n\n';
            return block.has(name) || name === 'LI' ? '\n\n' + inner.trim() + '\n\n' : inner;
          };
          const text = convert(root, 0).replace(/[ \t]+\n/g, '\n').replace(/\n{3,}/g, '\n\n').trim();
          return { title: document.title, url: location.href, text: text.slice(0, max), truncated: text.length > max };
        })()
        """.Replace("__MAX__", MaxCharacters.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
