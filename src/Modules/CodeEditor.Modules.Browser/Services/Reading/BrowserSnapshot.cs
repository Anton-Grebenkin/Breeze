using System.Globalization;

namespace CodeEditor.Modules.Browser.Services.Reading;

/// <summary>
/// The page snapshot script of the <c>browser</c> tool (ADR 0027, 0054), like the Playwright MCP accessibility snapshot:
/// it walks what is visible and tags each interactive element with <c>data-agent-ref</c> (<c>e1</c>, <c>e2</c>…), which
/// the model uses to click and type. Text next to links is kept, a table comes as rows, the content of open shadow
/// roots and of frames of the same site is included (a frame of another site is named with its address), and a canvas
/// or a large SVG is named as a graphic to look at with a screenshot. The whole page is returned up to
/// <see cref="MaxCharacters"/>; the tool hands it to the model in parts (<see cref="PageParts"/>).
/// </summary>
public static class BrowserSnapshot
{
    /// <summary>Snapshot length limit; a page longer than this is cut with a notice.</summary>
    public const int MaxCharacters = 240_000;

    /// <summary>Rows of one table in the snapshot; the rest are counted.</summary>
    public const int MaxTableRows = 60;

    /// <summary>Snapshot: <c>{ title, url, text, truncated }</c>.</summary>
    public static string Script { get; } = """
        (() => {
          const max = __MAX__, maxRows = __ROWS__;
          let refs = 0, length = 0;
          const lines = [];
          const full = () => length > max;
          const add = (depth, text) => { if (full()) return; const line = '  '.repeat(depth) + text; lines.push(line); length += line.length + 1; };
          const clean = s => (s || '').replace(/\s+/g, ' ').trim();
          const short = (s, n) => { s = clean(s); return s.length > n ? s.slice(0, n) + '…' : s; };
          const styleOf = el => (el.ownerDocument.defaultView || window).getComputedStyle(el);
          const hidden = el => { if (el.hidden || el.getAttribute('aria-hidden') === 'true') return true; const st = styleOf(el); return st.display === 'none' || st.visibility === 'hidden'; };
          const skip = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'META', 'LINK', 'HEAD']);
          const interactive = 'a[href],button,input:not([type=hidden]),select,textarea,summary,[role=button],[role=link],[role=checkbox],[role=radio],[role=tab],[role=menuitem],[role=option],[role=switch],[contenteditable=""],[contenteditable=true]';
          const special = interactive + ',iframe,table,canvas';
          const textBlocks = new Set(['P', 'LI', 'TD', 'TH', 'LABEL', 'DT', 'DD', 'BLOCKQUOTE', 'PRE', 'CAPTION', 'FIGCAPTION', 'SPAN', 'DIV', 'SECTION', 'ARTICLE', 'MAIN']);
          const roots = root => { const found = []; root.querySelectorAll('*').forEach(el => { if (el.shadowRoot) found.push(el.shadowRoot); if (el.tagName === 'IFRAME') { try { if (el.contentDocument) found.push(el.contentDocument); } catch { } } }); return found; };
          const clear = root => { root.querySelectorAll('[data-agent-ref]').forEach(el => el.removeAttribute('data-agent-ref')); roots(root).forEach(clear); };
          // Elements with a shadow root somewhere below: their text is not in innerText, so they are walked.
          const deep = new WeakSet();
          const mark = root => root.querySelectorAll('*').forEach(el => {
            if (!el.shadowRoot) return;
            for (let up = el; up && !deep.has(up); up = up.parentElement || (up.getRootNode().host || null)) deep.add(up);
            mark(el.shadowRoot);
          });
          const roleOf = el => {
            const role = el.getAttribute('role');
            if (role) return role;
            const tag = el.tagName;
            if (tag === 'A') return 'link';
            if (tag === 'BUTTON' || tag === 'SUMMARY') return 'button';
            if (tag === 'SELECT') return 'combobox';
            if (tag === 'TEXTAREA') return 'textbox';
            if (tag === 'INPUT') { const type = (el.type || 'text').toLowerCase(); return ['checkbox', 'radio', 'button', 'submit', 'reset', 'range', 'file'].includes(type) ? type : 'textbox'; }
            return 'editable';
          };
          const nameOf = el => short(el.getAttribute('aria-label') || (el.labels && el.labels[0] && el.labels[0].innerText) || el.innerText || (el.type === 'password' ? '' : el.value) || el.placeholder || el.title || el.alt || el.name || '', 80);
          const tag = el => { const ref = 'e' + (++refs); el.setAttribute('data-agent-ref', ref); return ref; };
          const describe = el => {
            const role = roleOf(el);
            let line = `${role} "${nameOf(el)}" [ref=${tag(el)}]`;
            if (role === 'link') line += ' -> ' + el.getAttribute('href');
            if (role === 'textbox' && el.type !== 'password' && el.value) line += ` value="${short(el.value, 60)}"`;
            if ((role === 'checkbox' || role === 'radio') && el.checked) line += ' (checked)';
            if (role === 'combobox' && el.selectedOptions && el.selectedOptions[0]) line += ` selected="${short(el.selectedOptions[0].text, 40)}"`;
            if (el.disabled) line += ' (disabled)';
            return line;
          };
          const table = (el, depth) => {
            const rows = [...el.rows].filter(row => !hidden(row));
            add(depth, `- table (${rows.length} rows)`);
            rows.slice(0, maxRows).forEach(row => {
              const cells = [...row.cells].map(cell => {
                const inner = [...cell.querySelectorAll(interactive)].filter(item => !hidden(item));
                if (inner.length === 0) return short(cell.innerText, 80);
                const own = short([...cell.childNodes].filter(node => node.nodeType === 3).map(node => node.nodeValue).join(' '), 80);
                return (own ? own + ' ' : '') + inner.map(describe).join(' ');
              });
              add(depth + 1, '| ' + cells.join(' | ') + ' |');
            });
            if (rows.length > maxRows) add(depth + 1, `… ${rows.length - maxRows} more rows: use find or read`);
          };
          const frame = (el, depth) => {
            let doc = null;
            try { doc = el.contentDocument; } catch { }
            const label = short(el.title || el.name || '', 80);
            if (!doc || !doc.body) { add(depth, `- frame "${label}" of another site, not read: open its address in a tab -> ${el.src}`); return; }
            add(depth, `- frame "${label}"`);
            walk(doc.body, depth + 1);
          };
          const graphic = (el, depth) => {
            const box = el.getBoundingClientRect();
            if (box.width < 120 || box.height < 80) return;
            add(depth, `- graphic "${short(el.getAttribute('aria-label') || el.getAttribute('title') || '', 80)}" (${el.tagName.toLowerCase()}, ${Math.round(box.width)}x${Math.round(box.height)}) [ref=${tag(el)}]: see it with screenshot and this ref`);
          };
          const walk = (el, depth) => {
            for (const node of el.childNodes) {
              if (full()) return;
              if (node.nodeType === 3) { const text = short(node.nodeValue, 300); if (text) add(depth, `- text "${text}"`); continue; }
              if (node.nodeType !== 1) continue;
              const name = node.tagName.toUpperCase();
              if (skip.has(name) || hidden(node)) continue;
              if (name === 'IFRAME') { frame(node, depth); continue; }
              if (name === 'CANVAS' || name === 'SVG') { graphic(node, depth); continue; }
              if (node.matches(interactive)) { add(depth, '- ' + describe(node)); continue; }
              if (/^H[1-6]$/.test(name)) { add(depth, `- heading "${short(node.innerText, 120)}"`); continue; }
              if (name === 'TABLE') { table(node, depth); continue; }
              if (textBlocks.has(name) && !deep.has(node) && !node.querySelector(special)) { const text = short(node.innerText, 300); if (text) add(depth, `- text "${text}"`); continue; }
              const inner = node.matches('nav,header,footer,form,dialog,[role=dialog],ul,ol') ? depth + 1 : depth;
              if (node.shadowRoot) walk(node.shadowRoot, inner);
              walk(node, inner);
            }
          };
          clear(document);
          mark(document);
          if (document.body) walk(document.body, 0);
          return { title: document.title, url: location.href, text: lines.join('\n'), truncated: full() };
        })()
        """
        .Replace("__MAX__", MaxCharacters.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("__ROWS__", MaxTableRows.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
