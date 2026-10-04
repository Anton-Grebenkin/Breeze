'use strict';

// SVG drawing (ADR 0037). The tab sends:
//   { type: 'show', src, theme, zoom }: the drawing address and theme colors; zoom null fits the window (a large
//     drawing shrinks, a small one isn't stretched), a number is the chosen zoom;
//   { type: 'zoom', value }: zoom from buttons and keys; { type: 'fit' }: fit to window.
// The page replies: { type: 'ready' }; { type: 'size', width, height } in CSS pixels;
// { type: 'zoom', value, fit } when Ctrl+wheel or fit changed the zoom; { type: 'error' } when the drawing can't be
// rendered (the file isn't SVG).
(() => {
  const minZoom = 0.01;
  const maxZoom = 20;
  const wheelSpeed = 0.0015;
  // A drawing without width, height and viewBox gets the CSS default size for replaced elements.
  const defaultSize = { width: 300, height: 150 };
  const themeVariables = {
    background: '--background',
    foreground: '--foreground',
    secondary: '--secondary',
    focus: '--focus',
    checkerBackground: '--checker-background',
    checkerSquare: '--checker-square',
  };
  const viewport = document.getElementById('viewport');
  const picture = document.getElementById('picture');
  let zoom = 1;
  let fitted = true;
  let natural = null;
  let drag = null;

  function post(message) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(message);
    }
  }

  function clamp(value) {
    return Number.isFinite(value) ? Math.min(maxZoom, Math.max(minZoom, value)) : 1;
  }

  function applyTheme(theme) {
    if (!theme) {
      return;
    }
    const style = document.documentElement.style;
    style.colorScheme = theme.scheme || 'light dark';
    for (const [name, variable] of Object.entries(themeVariables)) {
      if (theme[name]) {
        style.setProperty(variable, theme[name]);
      }
    }
  }

  function canPan() {
    return viewport.scrollWidth > viewport.clientWidth || viewport.scrollHeight > viewport.clientHeight;
  }

  function apply(value) {
    zoom = clamp(value);
    if (!natural) {
      return;
    }
    picture.style.width = natural.width * zoom + 'px';
    picture.style.height = natural.height * zoom + 'px';
    viewport.classList.toggle('pannable', canPan());
  }

  function fitZoom() {
    if (!natural || viewport.clientWidth === 0 || viewport.clientHeight === 0) {
      return zoom;
    }
    return clamp(Math.min(1, viewport.clientWidth / natural.width, viewport.clientHeight / natural.height));
  }

  function fit() {
    fitted = true;
    apply(fitZoom());
    post({ type: 'zoom', value: zoom, fit: true });
  }

  // The drawing point under the pointer stays in place.
  function zoomAround(value, clientX, clientY) {
    fitted = false;
    if (!natural) {
      zoom = clamp(value);
      return;
    }
    const before = picture.getBoundingClientRect();
    const shareX = before.width > 0 ? Math.min(1, Math.max(0, (clientX - before.left) / before.width)) : 0.5;
    const shareY = before.height > 0 ? Math.min(1, Math.max(0, (clientY - before.top) / before.height)) : 0.5;
    apply(value);
    const after = picture.getBoundingClientRect();
    viewport.scrollLeft += after.left + shareX * after.width - clientX;
    viewport.scrollTop += after.top + shareY * after.height - clientY;
  }

  function zoomCenter(value) {
    const rect = viewport.getBoundingClientRect();
    zoomAround(value, rect.left + viewport.clientWidth / 2, rect.top + viewport.clientHeight / 2);
  }

  // Showing the same address again only updates the theme; a new address (the file changed) redraws the drawing.
  function show(message) {
    applyTheme(message.theme);
    fitted = typeof message.zoom !== 'number';
    if (!fitted) {
      zoom = clamp(message.zoom);
    }
    if (picture.getAttribute('src') !== message.src) {
      picture.src = message.src;
    } else if (natural && fitted) {
      fit();
    } else if (natural) {
      apply(zoom);
    }
  }

  function handle(message) {
    if (!message || typeof message !== 'object') {
      return;
    }
    if (message.type === 'show') {
      show(message);
    } else if (message.type === 'zoom') {
      zoomCenter(message.value);
    } else if (message.type === 'fit') {
      fit();
    }
  }

  picture.addEventListener('load', () => {
    natural = {
      width: picture.naturalWidth || defaultSize.width,
      height: picture.naturalHeight || defaultSize.height,
    };
    picture.classList.remove('waiting');
    post({ type: 'size', width: natural.width, height: natural.height });
    if (fitted) {
      fit();
    } else {
      apply(zoom);
    }
  });

  picture.addEventListener('error', () => {
    natural = null;
    picture.classList.add('waiting');
    post({ type: 'error' });
  });

  viewport.addEventListener('wheel', event => {
    if (!event.ctrlKey) {
      return;
    }
    event.preventDefault();
    zoomAround(zoom * Math.exp(-event.deltaY * wheelSpeed), event.clientX, event.clientY);
    post({ type: 'zoom', value: zoom, fit: false });
  }, { passive: false });

  // Drag the drawing with the left button when it's larger than the window; clicks on the scrollbars work as usual.
  viewport.addEventListener('pointerdown', event => {
    const rect = viewport.getBoundingClientRect();
    if (event.button !== 0 || !canPan() || event.clientX - rect.left >= viewport.clientWidth || event.clientY - rect.top >= viewport.clientHeight) {
      return;
    }
    drag = { id: event.pointerId, x: event.clientX, y: event.clientY, left: viewport.scrollLeft, top: viewport.scrollTop };
    viewport.setPointerCapture(event.pointerId);
    viewport.classList.add('dragging');
  });

  viewport.addEventListener('pointermove', event => {
    if (drag && event.pointerId === drag.id) {
      viewport.scrollLeft = drag.left - (event.clientX - drag.x);
      viewport.scrollTop = drag.top - (event.clientY - drag.y);
    }
  });

  function endDrag(event) {
    if (drag && event.pointerId === drag.id) {
      drag = null;
      viewport.classList.remove('dragging');
    }
  }

  viewport.addEventListener('pointerup', endDrag);
  viewport.addEventListener('pointercancel', endDrag);

  window.addEventListener('resize', () => {
    if (fitted) {
      fit();
    } else {
      viewport.classList.toggle('pannable', canPan());
    }
  });

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', event => handle(event.data));
  }

  post({ type: 'ready' });
})();
