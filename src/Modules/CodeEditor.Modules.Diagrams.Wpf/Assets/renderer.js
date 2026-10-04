'use strict';

// Mermaid diagram renderer (ADR 0035): a hidden WebView2 page. The editor calls window.diagrams through DevTools
// (Runtime.evaluate awaiting the promise) and gets an object: { ok: true, ... }, a diagram error
// { ok: false, message, line }, or { ok: false, unavailable: true, message } if Mermaid failed to load. A failure of
// the script itself is an exception.
(() => {
  // Mermaid 11.17.2, a copy next to the page (THIRD-PARTY-NOTICES.md): diagrams render without the network.
  const sources = ['mermaid.min.js'];
  // The diagram id scopes its styles (#id ...) and arrow markers. The preview shows SVGs from earlier page loads side by
  // side (the page is reopened after a hang), so each load gets its own prefix.
  const prefix = 'd' + Date.now().toString(36) + Math.random().toString(36).slice(2, 6) + '-';
  let loading = null;
  let counter = 0;

  function loadScript(source) {
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = source;
      script.onload = () => resolve();
      script.onerror = () => {
        script.remove();
        reject(new Error('cannot load ' + source));
      };
      document.head.appendChild(script);
    });
  }

  async function loadFirst() {
    let lastError = null;
    for (const source of sources) {
      try {
        await loadScript(source);
        // An IIFE build may expose the whole module: the API is in its default export.
        if (window.mermaid && typeof window.mermaid.render !== 'function' && window.mermaid.default) {
          window.mermaid = window.mermaid.default;
        }
        if (window.mermaid && typeof window.mermaid.render === 'function') {
          return;
        }
        lastError = new Error('mermaid is not defined in ' + source);
      } catch (error) {
        lastError = error;
      }
    }
    throw lastError;
  }

  // A failure isn't cached: the next diagram tries to load Mermaid again.
  function loadMermaid() {
    if (!loading) {
      loading = loadFirst().catch(error => {
        loading = null;
        throw error;
      });
    }
    return loading;
  }

  function configure(theme) {
    window.mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: theme,
      suppressErrorRendering: true,
    });
  }

  function messageOf(error) {
    if (error && typeof error.message === 'string' && error.message) {
      return error.message;
    }
    if (error && typeof error.str === 'string') {
      return error.str;
    }
    return String(error);
  }

  // Error line: "on line 3" in the message, otherwise the location from the jison parser.
  function lineOf(error, message) {
    const match = /\bline (\d+)/i.exec(message);
    if (match) {
      return Number(match[1]);
    }
    const hash = error && error.hash;
    if (hash && hash.loc && typeof hash.loc.first_line === 'number') {
      return hash.loc.first_line;
    }
    if (hash && typeof hash.line === 'number') {
      return hash.line + 1;
    }
    return null;
  }

  function failure(error) {
    const message = messageOf(error);
    return { ok: false, message: message, line: lineOf(error, message) };
  }

  // Temporary render elements that Mermaid didn't remove after an error.
  function cleanup(id) {
    for (const prefix of ['', 'd', 'i']) {
      const element = document.getElementById(prefix + id);
      if (element) {
        element.remove();
      }
    }
  }

  // Mermaid markup is HTML serialization (it may contain &nbsp; and <br>); an SVG file and an image need valid XML.
  function parseSvg(markup) {
    const holder = document.createElement('div');
    holder.innerHTML = markup;
    const element = holder.querySelector('svg');
    if (!element) {
      throw new Error('Mermaid returned no SVG');
    }
    return element;
  }

  async function draw(text, theme) {
    configure(theme);
    const id = prefix + (++counter);
    try {
      const result = await window.mermaid.render(id, text);
      return { ok: true, element: parseSvg(result.svg) };
    } catch (error) {
      return failure(error);
    } finally {
      cleanup(id);
    }
  }

  function background() {
    const api = window.mermaid.mermaidAPI;
    const config = api && typeof api.getConfig === 'function' ? api.getConfig() : null;
    const variables = config && config.themeVariables;
    return (variables && variables.background) || 'white';
  }

  function naturalSize(element) {
    const box = element.viewBox && element.viewBox.baseVal;
    const width = (box && box.width) || parseFloat(element.getAttribute('width')) || 1;
    const height = (box && box.height) || parseFloat(element.getAttribute('height')) || 1;
    return { width: width, height: height };
  }

  async function rasterize(element, scale, maxSide, fill) {
    const size = naturalSize(element);
    const factor = Math.min(scale, maxSide / Math.max(size.width, size.height));
    const copy = element.cloneNode(true);
    copy.setAttribute('width', String(size.width));
    copy.setAttribute('height', String(size.height));
    copy.style.maxWidth = 'none';
    const image = new Image();
    image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(copy));
    await image.decode();
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(size.width * factor));
    canvas.height = Math.max(1, Math.round(size.height * factor));
    const context = canvas.getContext('2d');
    context.fillStyle = fill;
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/png').split(',')[1];
  }

  async function withMermaid(work) {
    try {
      await loadMermaid();
    } catch (error) {
      return { ok: false, unavailable: true, message: messageOf(error) };
    }
    return work();
  }

  window.diagrams = {
    // Mermaid is loaded: the page is ready to render.
    ready() {
      return withMermaid(async () => ({ ok: true }));
    },

    check(text) {
      return withMermaid(async () => {
        configure('default');
        let type = '';
        try {
          const parsed = await window.mermaid.parse(text);
          type = (parsed && parsed.diagramType) || '';
        } catch (error) {
          return failure(error);
        }
        const drawn = await draw(text, 'default');
        return drawn.ok ? { ok: true, type: type } : drawn;
      });
    },

    svg(text, theme) {
      return withMermaid(async () => {
        const drawn = await draw(text, theme);
        return drawn.ok ? { ok: true, svg: new XMLSerializer().serializeToString(drawn.element) } : drawn;
      });
    },

    png(text, theme, scale, maxSide) {
      return withMermaid(async () => {
        const drawn = await draw(text, theme);
        return drawn.ok ? { ok: true, png: await rasterize(drawn.element, scale, maxSide, background()) } : drawn;
      });
    },
  };

  // Mermaid starts loading as soon as the page opens, so the first diagram doesn't wait for it.
  loadMermaid().catch(() => {});
})();
