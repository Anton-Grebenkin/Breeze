using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services.Web;

/// <summary>
/// Reads a search engine's results page without knowing its markup, which changes often: a result is a link in a
/// heading that leads to another site; the excerpt is the rest of the text of the block around it. Links the engine
/// wraps in a redirect are unwrapped. A page with no results that speaks of a check or a consent needs the user; the
/// words it is known by are in the resources, in the editor's language and in English.
/// </summary>
public static class BrowserSearchScript
{
    public const int MaxHits = 8;

    /// <summary>Results: <c>{ hits: [{ title, url, snippet }], needsUser }</c>.</summary>
    public static string Results { get; } = """
        (() => {
          const max = __MAX__;
          const clean = s => (s || '').replace(/\s+/g, ' ').trim();
          const own = location.hostname.replace(/^www\./, '').split('.').slice(-2).join('.');
          const unwrap = href => {
            try {
              const link = new URL(href, location.href);
              for (const key of ['uddg', 'q', 'url', 'u']) {
                const inner = link.searchParams.get(key);
                if (inner && /^https?:\/\//.test(inner)) return inner;
              }
              return link.href;
            } catch { return null; }
          };
          const seen = new Set(), hits = [];
          for (const link of document.querySelectorAll('h1 a[href], h2 a[href], h3 a[href], a[href]:has(h2), a[href]:has(h3), a.result__a[href]')) {
            if (hits.length >= max) break;
            const url = unwrap(link.getAttribute('href'));
            if (!url || !/^https?:\/\//.test(url) || seen.has(url)) continue;
            const site = new URL(url).hostname.replace(/^www\./, '');
            if (site === own || site.endsWith('.' + own)) continue;
            const title = clean(link.innerText);
            if (title.length < 3) continue;
            const box = link.closest('li, article, .result, .g, [data-testid="result"]') || (link.parentElement && link.parentElement.parentElement);
            const around = clean(box ? box.innerText : '');
            seen.add(url);
            hits.push({ title: title.slice(0, 200), url, snippet: around.replace(title, '').trim().slice(0, 300) });
          }
          const text = document.body ? document.body.innerText.slice(0, 4000) : '';
          return { hits, needsUser: hits.length === 0 && new RegExp(__CHECK__, 'i').test(text) };
        })()
        """
        .Replace("__MAX__", MaxHits.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("__CHECK__", JsonSerializer.Serialize(Strings.SearchCheckWords), StringComparison.Ordinal);
}
