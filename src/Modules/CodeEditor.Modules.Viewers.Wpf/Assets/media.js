'use strict';

// Audio and video player (ADR 0037). The tab sends:
//   { type: 'show', src, video, theme }: a recording with the browser's controls, paused until started;
//   { type: 'pause' }: the tab was hidden.
// The page replies: { type: 'ready' }; { type: 'metadata', duration, width, height } once the header is read;
// { type: 'error' } for an unsupported codec or format.
(() => {
  const themeVariables = { background: '--background', foreground: '--foreground', focus: '--focus' };
  const stage = document.getElementById('stage');
  let media = null;

  function post(message) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(message);
    }
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

  // No download and no picture-in-picture: the recording lives only in the tab.
  function create(video) {
    const element = document.createElement(video ? 'video' : 'audio');
    element.controls = true;
    element.preload = 'metadata';
    element.setAttribute('controlslist', 'nodownload');
    if (video) {
      element.disablePictureInPicture = true;
    }
    return element;
  }

  function stop() {
    if (media) {
      media.pause();
      media.removeAttribute('src');
      media.load();
    }
  }

  // Showing the same address again only updates the theme; a new address (the file changed) keeps the playback position.
  function show(message) {
    applyTheme(message.theme);
    if (media && media.getAttribute('src') === message.src) {
      return;
    }
    const resume = media ? media.currentTime : 0;
    stop();
    const element = create(message.video === true);
    element.addEventListener('loadedmetadata', () => {
      if (resume > 0 && resume < element.duration) {
        element.currentTime = resume;
      }
      post({
        type: 'metadata',
        duration: Number.isFinite(element.duration) ? element.duration : null,
        width: element.videoWidth || 0,
        height: element.videoHeight || 0,
      });
    });
    element.addEventListener('error', () => post({ type: 'error' }));
    element.src = message.src;
    stage.replaceChildren(element);
    media = element;
  }

  function handle(message) {
    if (!message || typeof message !== 'object') {
      return;
    }
    if (message.type === 'show') {
      show(message);
    } else if (message.type === 'pause' && media) {
      media.pause();
    }
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', event => handle(event.data));
  }

  post({ type: 'ready' });
})();
