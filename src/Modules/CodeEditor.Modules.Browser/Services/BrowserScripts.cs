using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Page scripts for the <c>browser</c> tool (ADR 0027) that act on an element of the snapshot
/// (<see cref="Reading.BrowserSnapshot"/>) by its <c>data-agent-ref</c> (<c>e1</c>, <c>e2</c>…). Model values enter a
/// script only as JSON strings, so no code can be injected.
/// </summary>
public static class BrowserScripts
{
    // Finds a snapshot element wherever it was tagged: in the page, an open shadow root or a frame of the same site.
    private const string Locate = """
        const locate = ref => {
          const selector = `[data-agent-ref="${CSS.escape(ref)}"]`;
          const search = root => {
            const hit = root.querySelector(selector);
            if (hit) return hit;
            for (const el of root.querySelectorAll('*')) {
              let inner = null;
              if (el.shadowRoot) inner = search(el.shadowRoot);
              else if (el.tagName === 'IFRAME') { try { inner = el.contentDocument ? search(el.contentDocument) : null; } catch { } }
              if (inner) return inner;
            }
            return null;
          };
          return search(document);
        };
        """;

    /// <summary>Clicks a snapshot element: <c>{ ok, error }</c>.</summary>
    public static string Click(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
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
          {{Locate}}
          const el = locate(ref);
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

    /// <summary>A snapshot element as the page names it, for an approval card: <c>{ ok, role, name }</c>.</summary>
    public static string Describe(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
          if (!el) return { ok: false, error: 'not found' };
          const text = el.getAttribute('aria-label') || (el.labels && el.labels[0] && el.labels[0].innerText) || el.innerText || (el.type === 'password' ? '' : el.value) || el.placeholder || el.title || el.alt || el.name || '';
          return { ok: true, role: el.getAttribute('role') || el.tagName.toLowerCase(), name: text.replace(/\s+/g, ' ').trim().slice(0, 80) };
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>
    /// Scrolls a snapshot element into view and returns its box in page coordinates, for a screenshot of it alone:
    /// <c>{ ok, x, y, width, height }</c>. An element in a frame is placed by the frames it sits in.
    /// </summary>
    public static string Box(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
          if (!el) return { ok: false, error: 'not found' };
          el.scrollIntoView({ block: 'center', inline: 'center' });
          const box = el.getBoundingClientRect();
          let x = box.x, y = box.y;
          for (let view = el.ownerDocument.defaultView; view && view !== window && view.frameElement; view = view.parent) {
            const frame = view.frameElement.getBoundingClientRect();
            x += frame.x; y += frame.y;
          }
          return { ok: true, x: x + scrollX, y: y + scrollY, width: box.width, height: box.height };
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>
    /// Scrolls a snapshot element into view and returns the point a mouse would click, in the viewport of the top page:
    /// <c>{ ok, x, y, kind, draggable }</c>. <c>kind</c> is <c>file</c> for a file field and <c>select</c> for a native
    /// list, which real clicks would answer with a system window. Arms a probe that tells whether the next real click
    /// reached the element (<see cref="WasClicked"/>).
    /// </summary>
    public static string Point(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
          if (!el) return { ok: false, error: 'not found' };
          el.scrollIntoView({ block: 'center', inline: 'center' });
          const box = el.getBoundingClientRect();
          let x = box.x + box.width / 2, y = box.y + box.height / 2;
          for (let view = el.ownerDocument.defaultView; view && view !== window && view.frameElement; view = view.parent) {
            const frame = view.frameElement.getBoundingClientRect();
            x += frame.x; y += frame.y;
          }
          window.__agentClick = null;
          el.addEventListener('click', () => { window.__agentClick = ref; }, { capture: true, once: true });
          const kind = el.tagName === 'INPUT' && el.type === 'file' ? 'file' : el.tagName === 'SELECT' || el.tagName === 'OPTION' ? 'select' : 'element';
          return { ok: true, x, y, kind, draggable: !!el.closest('[draggable=true]') };
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>Whether the real click after <see cref="Point"/> reached the element: <c>true</c> or <c>false</c>.</summary>
    public static string WasClicked(string reference) => $"window.__agentClick === {JsonSerializer.Serialize(reference)}";

    /// <summary>
    /// Drags one snapshot element onto another with the page's own drag-and-drop events, for elements marked
    /// <c>draggable</c>: <c>{ ok, error }</c>.
    /// </summary>
    public static string DragDrop(string reference, string target) => $$"""
        ((from, to) => {
          {{Locate}}
          const source = locate(from), goal = locate(to);
          if (!source || !goal) return { ok: false, error: 'not found' };
          const data = new DataTransfer();
          const fire = (el, type) => el.dispatchEvent(new DragEvent(type, { bubbles: true, cancelable: true, dataTransfer: data }));
          fire(source, 'dragstart'); fire(goal, 'dragenter'); fire(goal, 'dragover'); fire(goal, 'drop'); fire(source, 'dragend');
          return { ok: true };
        })({{JsonSerializer.Serialize(reference)}}, {{JsonSerializer.Serialize(target)}})
        """;

    /// <summary>The snapshot element if it is a file field, else <c>null</c>: the file is set on it through DevTools.</summary>
    public static string FileInput(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
          return el && el.tagName === 'INPUT' && el.type === 'file' ? el : null;
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>
    /// A request to the page's own site with the page's sign-in, as its scripts make them:
    /// <c>{ ok, status, type, url, body }</c> or <c>{ ok: false, error }</c>. An address of another site is refused
    /// (<c>error: 'other site'</c>). A request that changes something carries the site's CSRF token if the page has one,
    /// as GitLab needs. The body is cut at <paramref name="maxBody"/> characters (<c>cut: true</c>).
    /// </summary>
    public static string Request(string method, string address, string? body, int maxBody) => $$"""
        (async (method, address, body, max) => {
          let url;
          try { url = new URL(address, location.href); } catch { return { ok: false, error: 'bad address' }; }
          if (url.origin !== location.origin) return { ok: false, error: 'other site' };
          const headers = { 'Accept': 'application/json, text/plain, */*' };
          if (body !== null) headers['Content-Type'] = 'application/json';
          const token = document.querySelector('meta[name="csrf-token"]');
          if (token && token.content && !['GET', 'HEAD'].includes(method)) headers['X-CSRF-Token'] = token.content;
          try {
            const reply = await fetch(url.href, { method, headers, body: body === null ? undefined : body, credentials: 'include' });
            const text = await reply.text();
            return { ok: true, status: reply.status, type: reply.headers.get('content-type') || '', url: reply.url, body: text.slice(0, max), cut: text.length > max };
          } catch (error) {
            return { ok: false, error: String(error && error.message || error) };
          }
        })({{JsonSerializer.Serialize(method)}}, {{JsonSerializer.Serialize(address)}}, {{JsonSerializer.Serialize(body)}}, {{maxBody}})
        """;

    /// <summary>Focuses a snapshot element before a key press: <c>{ ok, error }</c>.</summary>
    public static string Focus(string reference) => $$"""
        (ref => {
          {{Locate}}
          const el = locate(ref);
          if (!el) return { ok: false, error: 'not found' };
          el.scrollIntoView({ block: 'center' });
          if (el.focus) el.focus();
          return { ok: true };
        })({{JsonSerializer.Serialize(reference)}})
        """;

    /// <summary>
    /// Scrolls to a snapshot element, or the page by most of a screen (<c>down</c>, <c>up</c>) or to its <c>top</c> or
    /// <c>bottom</c>: <c>{ ok, error }</c>.
    /// </summary>
    public static string Scroll(string? reference, string? direction) => $$"""
        ((ref, direction) => {
          {{Locate}}
          if (ref) {
            const el = locate(ref);
            if (!el) return { ok: false, error: 'not found' };
            el.scrollIntoView({ block: 'center' });
            return { ok: true };
          }
          const step = Math.round(innerHeight * 0.8);
          if (direction === 'up') scrollBy(0, -step);
          else if (direction === 'top') scrollTo(0, 0);
          else if (direction === 'bottom') scrollTo(0, document.documentElement.scrollHeight);
          else scrollBy(0, step);
          return { ok: true };
        })({{JsonSerializer.Serialize(reference)}}, {{JsonSerializer.Serialize(direction ?? "down")}})
        """;

    /// <summary>The page text contains the text: <c>true</c> or <c>false</c>.</summary>
    public static string HasText(string text) =>
        $"(text => !!document.body && document.body.innerText.includes(text))({JsonSerializer.Serialize(text)})";
}
