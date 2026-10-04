'use strict';

// The Terminal panel page: one xterm.js terminal per shell, one shown at a time. The panel sends output and state
// (TerminalPageMessages); the page sends keyboard input, sizes, copied text and paste requests back.
(() => {
  const host = window.chrome.webview;
  const container = document.getElementById('terminals');
  const sessions = new Map();
  let settings = { theme: { scheme: 'dark' }, fontSize: 14 };
  let activeId = 0;

  // ANSI colors of VS Code's Dark+ and Light+ terminals; the background and text come from the editor theme.
  const palettes = {
    dark: {
      black: '#000000', red: '#cd3131', green: '#0dbc79', yellow: '#e5e510', blue: '#2472c8', magenta: '#bc3fbc',
      cyan: '#11a8cd', white: '#e5e5e5', brightBlack: '#666666', brightRed: '#f14c4c', brightGreen: '#23d18b',
      brightYellow: '#f5f543', brightBlue: '#3b8eea', brightMagenta: '#d670d6', brightCyan: '#29b8db', brightWhite: '#e5e5e5',
    },
    light: {
      black: '#000000', red: '#cd3131', green: '#00bc00', yellow: '#949800', blue: '#0451a5', magenta: '#bc05bc',
      cyan: '#0598bc', white: '#555555', brightBlack: '#666666', brightRed: '#cd3131', brightGreen: '#14ce14',
      brightYellow: '#b5ba00', brightBlue: '#0451a5', brightMagenta: '#bc05bc', brightCyan: '#0598bc', brightWhite: '#a5a5a5',
    },
  };

  const post = message => host.postMessage(message);

  function xtermTheme() {
    const theme = settings.theme;
    return Object.assign({}, palettes[theme.scheme] || palettes.dark, {
      background: theme.background,
      foreground: theme.foreground,
      cursor: theme.cursor,
      cursorAccent: theme.background,
      selectionBackground: theme.selectionBackground,
    });
  }

  function open(id, replay) {
    if (sessions.has(id)) {
      return;
    }

    const element = document.createElement('div');
    element.className = 'terminal-host';
    container.appendChild(element);

    const term = new Terminal({
      fontFamily: '"Cascadia Mono", Consolas, monospace',
      fontSize: settings.fontSize,
      theme: xtermTheme(),
      cursorBlink: true,
      scrollback: 10000,
      windowsPty: { backend: 'conpty' },
    });
    const fit = new FitAddon.FitAddon();
    term.loadAddon(fit);
    term.open(element);
    term.onData(data => post({ type: 'input', id, data }));
    term.onResize(size => post({ type: 'resize', id, columns: size.cols, rows: size.rows }));
    term.attachCustomKeyEventHandler(event => onKey(term, event));
    element.addEventListener('contextmenu', event => {
      event.preventDefault();
      onRightClick(id, term);
    });

    if (replay) {
      term.write(replay);
    }

    sessions.set(id, { term, fit, element });
  }

  // Ctrl+C copies a selection and otherwise interrupts the shell; Ctrl+V pastes through the browser.
  function onKey(term, event) {
    const copyKey = event.type === 'keydown' && event.ctrlKey && !event.altKey && event.code === 'KeyC';
    if (copyKey && (event.shiftKey || term.hasSelection())) {
      copy(term);
      return false;
    }

    return true;
  }

  // As in Windows Terminal: right click copies the selection, or pastes when there is none.
  function onRightClick(id, term) {
    if (term.hasSelection()) {
      copy(term);
    } else {
      post({ type: 'paste', id });
    }
  }

  function copy(term) {
    if (term.hasSelection()) {
      post({ type: 'copy', text: term.getSelection() });
      term.clearSelection();
    }
  }

  function fit(session) {
    try {
      session.fit.fit();
    } catch {
      // The panel is collapsed: there is nothing to measure until it opens.
    }
  }

  function show(id) {
    activeId = id;
    for (const [key, session] of sessions) {
      session.element.classList.toggle('active', key === id);
    }

    const session = sessions.get(id);
    if (session) {
      fit(session);
      session.term.focus();
    }
  }

  function close(id) {
    const session = sessions.get(id);
    if (session) {
      session.term.dispose();
      session.element.remove();
      sessions.delete(id);
    }
  }

  function applySettings() {
    document.documentElement.style.setProperty('--background', settings.theme.background || 'transparent');
    document.documentElement.style.colorScheme = settings.theme.scheme;
    for (const session of sessions.values()) {
      session.term.options.theme = xtermTheme();
      session.term.options.fontSize = settings.fontSize;
      fit(session);
    }
  }

  function handle(message) {
    const session = sessions.get(message.id);
    switch (message.type) {
      case 'init':
        settings = { theme: message.theme, fontSize: message.fontSize };
        applySettings();
        break;
      case 'theme':
        settings.theme = message.theme;
        applySettings();
        break;
      case 'open':
        open(message.id, message.data);
        break;
      case 'output':
        session?.term.write(message.data);
        break;
      case 'show':
        show(message.id);
        break;
      case 'close':
        close(message.id);
        break;
      case 'clear':
        session?.term.clear();
        break;
      case 'paste':
        session?.term.paste(message.text);
        break;
      case 'focus':
        sessions.get(activeId)?.term.focus();
        break;
    }
  }

  new ResizeObserver(() => {
    const session = sessions.get(activeId);
    if (session) {
      fit(session);
    }
  }).observe(container);

  host.addEventListener('message', event => handle(event.data));
  post({ type: 'ready' });
})();
