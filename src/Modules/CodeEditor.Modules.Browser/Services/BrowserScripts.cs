using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Page scripts for the <c>browser</c> tool (ADR 0027), like the Playwright MCP accessibility snapshot. The snapshot
/// walks visible elements and tags each interactive one with <c>data-agent-ref</c> (<c>e1</c>, <c>e2</c>…), which the
/// model uses to click and type. Model values enter a script only as JSON strings, so no code can be injected.
/// </summary>
public static class BrowserScripts
{
    /// <summary>Snapshot length limit; the rest of the page is cut with a notice.</summary>
    public const int MaxSnapshotCharacters = 12_000;

    /// <summary>Snapshot: <c>{ title, url, text, truncated }</c>.</summary>
    public static string Snapshot { get; } = """
        (() => {
          const max = __MAX__;
          let refs = 0, length = 0;
          const lines = [];
          const add = (depth, text) => { if (length > max) return; const line = '  '.repeat(depth) + text; lines.push(line); length += line.length + 1; };
          const clean = s => (s || '').replace(/\s+/g, ' ').trim();
          const short = (s, n) => { s = clean(s); return s.length > n ? s.slice(0, n) + '…' : s; };
          const hidden = el => { if (el.hidden || el.getAttribute('aria-hidden') === 'true') return true; const st = getComputedStyle(el); return st.display === 'none' || st.visibility === 'hidden'; };
          const skip = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE', 'SVG', 'IFRAME', 'META', 'LINK']);
          const interactive = 'a[href],button,input:not([type=hidden]),select,textarea,summary,[role=button],[role=link],[role=checkbox],[role=radio],[role=tab],[role=menuitem],[role=option],[role=switch],[contenteditable=""],[contenteditable=true]';
          const textBlocks = new Set(['P', 'LI', 'TD', 'TH', 'LABEL', 'DT', 'DD', 'BLOCKQUOTE', 'PRE', 'CAPTION', 'FIGCAPTION', 'SPAN', 'DIV', 'SECTION', 'ARTICLE', 'MAIN']);
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
          const nameOf = el => short(el.getAttribute('aria-label') || (el.labels && el.labels[0] && el.labels[0].innerText) || el.innerText || el.value || el.placeholder || el.title || el.alt || el.name || '', 80);
          const describe = el => {
            const ref = 'e' + (++refs);
            el.setAttribute('data-agent-ref', ref);
            const role = roleOf(el);
            let line = `- ${role} "${nameOf(el)}" [ref=${ref}]`;
            if (role === 'link') line += ' -> ' + el.getAttribute('href');
            if (role === 'textbox' && el.type !== 'password' && el.value) line += ` value="${short(el.value, 60)}"`;
            if ((role === 'checkbox' || role === 'radio') && el.checked) line += ' (checked)';
            if (role === 'combobox' && el.selectedOptions && el.selectedOptions[0]) line += ` selected="${short(el.selectedOptions[0].text, 40)}"`;
            if (el.disabled) line += ' (disabled)';
            return line;
          };
          const walk = (el, depth) => {
            for (const child of el.children) {
              if (length > max) return;
              if (skip.has(child.tagName) || hidden(child)) continue;
              if (child.matches(interactive)) { add(depth, describe(child)); continue; }
              if (/^H[1-6]$/.test(child.tagName)) { add(depth, `- heading "${short(child.innerText, 120)}"`); continue; }
              if (textBlocks.has(child.tagName) && !child.querySelector(interactive)) { const text = short(child.innerText, 300); if (text) add(depth, `- text "${text}"`); continue; }
              walk(child, child.matches('nav,header,footer,form,dialog,[role=dialog],table,ul,ol') ? depth + 1 : depth);
            }
          };
          document.querySelectorAll('[data-agent-ref]').forEach(el => el.removeAttribute('data-agent-ref'));
          if (document.body) walk(document.body, 0);
          return { title: document.title, url: location.href, text: lines.join('\n'), truncated: length > max };
        })()
        """.Replace("__MAX__", MaxSnapshotCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Clicks a snapshot element: <c>{ ok, error }</c>.</summary>
    public static string Click(string reference) => $$"""
        (ref => {
          const el = document.querySelector(`[data-agent-ref="${CSS.escape(ref)}"]`);
          if (!el) return { ok: false, error: 'not found' };
          el.scrollIntoView({ block: 'center', inline: 'center' });
          if (el.focus) el.focus();
          el.click();
          return { ok: true };
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>
    /// Types into a snapshot field, replacing the value so frameworks (React, Vue) see it; a select picks an option by
    /// text; <paramref name="submit"/> presses Enter and submits the form. Returns <c>{ ok, error }</c>.
    /// </summary>
    public static string Type(string reference, string text, bool submit) => $$"""
        ((ref, text, submit) => {
          const el = document.querySelector(`[data-agent-ref="${CSS.escape(ref)}"]`);
          if (!el) return { ok: false, error: 'not found' };
          el.scrollIntoView({ block: 'center' });
          el.focus();
          if (el.isContentEditable) {
            el.textContent = text;
            el.dispatchEvent(new InputEvent('input', { bubbles: true }));
          } else if (el.tagName === 'SELECT') {
            const option = [...el.options].find(o => o.text.trim() === text || o.value === text);
            if (!option) return { ok: false, error: 'no option' };
            el.value = option.value;
            el.dispatchEvent(new Event('change', { bubbles: true }));
          } else {
            const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
            Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, text);
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
          }
          if (submit) {
            const key = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true };
            el.dispatchEvent(new KeyboardEvent('keydown', key));
            el.dispatchEvent(new KeyboardEvent('keyup', key));
            if (el.form) { if (el.form.requestSubmit) el.form.requestSubmit(); else el.form.submit(); }
          }
          return { ok: true };
        })({{JsonSerializer.Serialize(reference)}}, {{JsonSerializer.Serialize(text)}}, {{(submit ? "true" : "false")}})
        """;
}
