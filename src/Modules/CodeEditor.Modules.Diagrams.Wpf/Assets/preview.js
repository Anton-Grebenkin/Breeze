'use strict';

// Diagram preview (ADR 0035). The tab sends:
//   { type: 'show', items: [{ caption, svg, stale }], placeholder, theme: { scheme, background, foreground, secondary, focus }, zoom }
//     where placeholder is shown when there are no diagrams: rendering in progress, an empty file or a diagram error;
//   { type: 'zoom', value }: zoom from buttons and keys; { type: 'fit' }: fit to window.
// The page replies: { type: 'ready' }, { type: 'zoom', value } when Ctrl+wheel or fit changed the zoom,
// { type: 'key', name } for the + - 0 keys on the page (the tab computes zoom steps).
(() => {
  const minZoom = 0.1;
  const maxZoom = 8;
  const wheelSpeed = 0.0015;
  const viewport = document.getElementById('viewport');
  const content = document.getElementById('content');
  const keys = { '+': 'zoomIn', '=': 'zoomIn', '-': 'zoomOut', '0': 'zoomReset' };
  let zoom = 1;
  let sized = [];
  let shown = false;
  let pendingFit = false;
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
    for (const name of ['background', 'foreground', 'secondary', 'focus']) {
      if (theme[name]) {
        style.setProperty('--' + name, theme[name]);
      }
    }
  }

  function naturalSize(svg) {
    const box = svg.viewBox && svg.viewBox.baseVal;
    if (box && box.width > 0 && box.height > 0) {
      return { width: box.width, height: box.height };
    }
    const rect = svg.getBoundingClientRect();
    return { width: rect.width || 1, height: rect.height || 1 };
  }

  function apply(value) {
    zoom = clamp(value);
    for (const item of sized) {
      item.svg.style.maxWidth = 'none';
      item.svg.style.width = item.width * zoom + 'px';
      item.svg.style.height = item.height * zoom + 'px';
    }
  }

  // The point under the pointer stays in place.
  function zoomAround(value, clientX, clientY) {
    const before = zoom;
    const next = clamp(value);
    if (Math.abs(next - before) < 1e-6) {
      return;
    }
    const rect = viewport.getBoundingClientRect();
    const x = clientX - rect.left;
    const y = clientY - rect.top;
    const contentX = viewport.scrollLeft + x;
    const contentY = viewport.scrollTop + y;
    apply(next);
    viewport.scrollLeft = (contentX * next) / before - x;
    viewport.scrollTop = (contentY * next) / before - y;
  }

  function zoomCenter(value) {
    const rect = viewport.getBoundingClientRect();
    zoomAround(value, rect.left + viewport.clientWidth / 2, rect.top + viewport.clientHeight / 2);
  }

  // A single diagram fits whole; several fit the width of the widest one.
  function fitZoom() {
    if (sized.length === 0 || viewport.clientWidth === 0) {
      return zoom;
    }
    const style = getComputedStyle(content);
    const width = viewport.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    const height = viewport.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom);
    let value = width / Math.max(...sized.map(item => item.width));
    if (sized.length === 1) {
      value = Math.min(value, height / sized[0].height);
    }
    return clamp(value);
  }

  function fit() {
    apply(fitZoom());
    post({ type: 'zoom', value: zoom });
  }

  function figure(item) {
    const element = document.createElement('figure');
    if (item.caption) {
      const caption = document.createElement('figcaption');
      caption.textContent = item.caption;
      element.appendChild(caption);
    }
    const holder = document.createElement('div');
    holder.className = item.stale ? 'diagram stale' : 'diagram';
    holder.innerHTML = item.svg;
    element.appendChild(holder);
    return element;
  }

  function placeholder(text) {
    const element = document.createElement('p');
    element.className = 'placeholder';
    element.textContent = text || '';
    return element;
  }

  // The zoom from the state applies only on this page's first show of diagrams: after that zoom messages change it, and
  // the wheel may have changed it while the state was in flight.
  function show(message) {
    applyTheme(message.theme);
    const items = message.items || [];
    content.classList.toggle('single', items.length <= 1);
    content.replaceChildren(...(items.length === 0 ? [placeholder(message.placeholder)] : items.map(figure)));
    sized = Array.from(content.querySelectorAll('.diagram > svg'), svg => ({ svg: svg, ...naturalSize(svg) }));
    if (shown || sized.length === 0) {
      apply(zoom);
      return;
    }

    shown = true;
    const chosen = Math.abs(clamp(message.zoom) - 1) > 1e-6;
    apply(chosen ? message.zoom : 1);
    if (!chosen) {
      fitFirstTime();
    }
  }

  // First show with no chosen zoom: a diagram larger than the window fits the window, otherwise it is shown at natural
  // size. If the window has no size yet (tab opened in the background), fit after the first resize.
  function fitFirstTime() {
    pendingFit = viewport.clientWidth === 0;
    if (!pendingFit && fitZoom() < 1) {
      fit();
    }
  }

  function handle(message) {
    if (!message || typeof message !== 'object') {
      return;
    }
    if (message.type === 'show') {
      show(message);
    } else if (message.type === 'zoom') {
      // Zoom from the tab's buttons and keys; the page doesn't echo it back.
      zoomCenter(message.value);
    } else if (message.type === 'fit') {
      fit();
    }
  }

  viewport.addEventListener('wheel', event => {
    if (!event.ctrlKey) {
      return;
    }
    event.preventDefault();
    zoomAround(zoom * Math.exp(-event.deltaY * wheelSpeed), event.clientX, event.clientY);
    post({ type: 'zoom', value: zoom });
  }, { passive: false });

  // Drag the diagram with the left button; clicks on the scrollbars work as usual.
  viewport.addEventListener('pointerdown', event => {
    const rect = viewport.getBoundingClientRect();
    if (event.button !== 0 || event.clientX - rect.left >= viewport.clientWidth || event.clientY - rect.top >= viewport.clientHeight) {
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

  // Arrows and PageUp/PageDown scroll natively; + - 0 zoom, as in image viewers.
  viewport.addEventListener('keydown', event => {
    const name = keys[event.key];
    if (name && !event.ctrlKey && !event.altKey && !event.metaKey) {
      event.preventDefault();
      post({ type: 'key', name: name });
    }
  });

  window.addEventListener('resize', () => {
    if (pendingFit) {
      fitFirstTime();
    }
  });

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', event => handle(event.data));
  }

  post({ type: 'ready' });
})();
