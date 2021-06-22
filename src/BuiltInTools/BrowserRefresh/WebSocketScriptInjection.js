setTimeout(async function () {
  // dotnet-watch browser reload script
  const webSocketUrls = '{{hostString}}'.split(',');
  let connection;
  for (const url of webSocketUrls) {
    try {
      connection = await getWebSocket(url);
      break;
    } catch (ex) {
      console.debug(ex);
    }
  }
  if (!connection) {
    console.debug('Unable to establish a conection to the browser refresh server.');
    return;
  }

  let waiting = false;

  connection.onmessage = function (message) {
    if (message.data === 'Reload') {
      console.debug('Server is ready. Reloading...');
      location.reload();
    } else if (message.data === 'Wait') {
      if (waiting) {
        return;
      }
      waiting = true;
      console.debug('File changes detected. Waiting for application to rebuild.');
      const glyphs = ['☱', '☲', '☴'];
      const title = document.title;
      let i = 0;
      setInterval(function () { document.title = glyphs[i++ % glyphs.length] + ' ' + title; }, 240);
    } else {
      const payload = JSON.parse(message.data);
      const action = {
        'UpdateStaticFile': () => updateStaticFile(payload.path),
        'BlazorHotReloadDeltav1': () => applyBlazorDeltas(payload.deltas),
        'HotReloadDiagnosticsv1': () => displayDiagnostics(payload.diagnostics),
        'AspNetCoreHotReloadApplied': () => aspnetCoreHotReloadApplied(),
      };

      if (payload.type && action.hasOwnProperty(payload.type)) {
        action[payload.type]();
      } else {
        console.error('Unknown payload:', message.data);
      }
    }
  }

  connection.onerror = function (event) { console.debug('dotnet-watch reload socket error.', event) }
  connection.onclose = function () { console.debug('dotnet-watch reload socket closed.') }
  connection.onopen = function () { console.debug('dotnet-watch reload socket connected.') }

  function updateStaticFile(path) {
    if (path && path.endsWith('.css')) {
      updateCssByPath(path);
    } else {
      console.debug(`File change detected to css file ${path}. Reloading page...`);
      location.reload();
      return;
    }
  }

  async function updateCssByPath(path) {
    const styleElement = document.querySelector(`link[href^="${path}"]`) ||
      document.querySelector(`link[href^="${document.baseURI}${path}"]`);

    // Receive a Clear-site-data header.
    await fetch('/_framework/clear-browser-cache');

    if (!styleElement || !styleElement.parentNode) {
      console.debug('Unable to find a stylesheet to update. Updating all local css files.');
      updateAllLocalCss();
    }

    updateCssElement(styleElement);
  }

  function updateAllLocalCss() {
    [...document.querySelectorAll('link')]
      .filter(l => l.baseURI === document.baseURI)
      .forEach(e => updateCssElement(e));
  }

  function updateCssElement(styleElement) {
    if (!styleElement || styleElement.loading) {
      // A file change notification may be triggered for the same file before the browser
      // finishes processing a previous update. In this case, it's easiest to ignore later updates
      return;
    }

    const newElement = styleElement.cloneNode();
    const href = styleElement.href;
    newElement.href = href.split('?', 1)[0] + `?nonce=${Date.now()}`;

    styleElement.loading = true;
    newElement.loading = true;
    newElement.addEventListener('load', function () {
      newElement.loading = false;
      styleElement.remove();
    });

    styleElement.parentNode.insertBefore(newElement, styleElement.nextSibling);
  }

  function applyBlazorDeltas(deltas) {
    let applyFailed = false;
    deltas.forEach(d => {
      try {
        window.Blazor._internal.applyHotReload(d.moduleId, d.metadataDelta, d.ilDelta)
      } catch (error) {
        console.warn(error);
        applyFailed = true;
      }
    });

    fetch('_framework/blazor-hotreload', { method: 'post', headers: { 'content-type': 'application/json' }, body: JSON.stringify(deltas) })
      .then(response => {
        if (response.status == 200) {
          const etag = response.headers['etag'];
          window.sessionStorage.setItem('blazor-webasssembly-cache', { etag, deltas });
        }
      });

    if (applyFailed) {
      sendDeltaNotApplied();
    } else {
      sendDeltaApplied();
      notifyHotReloadApplied();
    }
  }

  function displayDiagnostics(diagnostics) {
    document.querySelectorAll('#dotnet-compile-error').forEach(el => el.remove());
    const el = document.body.appendChild(document.createElement('div'));
    el.id = 'dotnet-compile-error';
    el.setAttribute('style', 'z-index:1000000; position:fixed; top: 0; left: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0.5); color:black; overflow: scroll;');
    diagnostics.forEach(error => {
      const item = el.appendChild(document.createElement('div'));
      item.setAttribute('style', 'border: 2px solid red; padding: 8px; background-color: #faa;')
      const message = item.appendChild(document.createElement('div'));
      message.setAttribute('style', 'font-weight: bold');
      message.textContent = error.Message;
      item.appendChild(document.createElement('div')).textContent = error;
    });
  }

  function notifyHotReloadApplied() {
    document.querySelectorAll('#dotnet-compile-error').forEach(el => el.remove());
    if (document.querySelector('#dotnet-hotreload-toast')) {
      return;
    }
    const el = document.createElement('div');
    el.id = 'dotnet-hotreload-toast';
    el.setAttribute('style', 'z-index:1000000; width:100%; height: 30px; font-size: large; text-align:center; position:fixed; top: 0; left: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0.5); color:black; transition: 0.5s all ease-in-out;');
    el.textContent = 'Updated the page';
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 520);
  }

  function aspnetCoreHotReloadApplied() {
    if (window.Blazor) {
      // If this page has any Blazor, don't refresh the browser.
      notiifyHotReloadApplied();
    } else {
      location.reload();
    }
  }

  function sendDeltaApplied() {
    connection.send(new Uint8Array([1]).buffer);
  }

  function sendDeltaNotApplied() {
    connection.send(new Uint8Array([0]).buffer);
  }

  function getWebSocket(url) {
    return new Promise((resolve, reject) => {
      const webSocket = new WebSocket(url);
      let opened = false;

      function onOpen() {
        opened = true;
        clearEventListeners();
        resolve(webSocket);
      }

      function onClose(event) {
        if (opened) {
          // Open completed successfully. Nothing to do here.
          return;
        }

        let error = 'WebSocket failed to connect.';
        if (event instanceof ErrorEvent) {
          error = event.error;
        }

        clearEventListeners();
        reject(error);
      }

      function clearEventListeners() {
        webSocket.removeEventListener('open', onOpen);
        // The error event isn't as reliable, but close is always called even during failures.
        // If close is called without a corresponding open, we can reject the promise.
        webSocket.removeEventListener('close', onClose);
      }

      webSocket.addEventListener('open', onOpen);
      webSocket.addEventListener('close', onClose);
    });
  }
}, 500);
