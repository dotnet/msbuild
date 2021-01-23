setTimeout(function () {
  // dotnet-watch browser reload script
  let connection;
  try {
    connection = new WebSocket('{{hostString}}');
  } catch (ex) {
    console.debug(ex);
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
      const parsed = JSON.parse(message.data);
      if (parsed.type == 'UpdateStaticFile') {
        const path = parsed.path;
        if (path && path.endsWith('.css')) {
          updateCssByPath(path);
        } else {
          console.debug(`File change detected to css file ${path}. Reloading page...`);
          location.reload();
          return;
        }
      }
    }
  }

  connection.onerror = function (event) { console.debug('dotnet-watch reload socket error.', event) }
  connection.onclose = function () { console.debug('dotnet-watch reload socket closed.') }
  connection.onopen = function () { console.debug('dotnet-watch reload socket connected.') }

  function updateCssByPath(path) {
    const styleElement = document.querySelector(`link[href^="${path}"]`) ||
      document.querySelector(`link[href^="${document.baseURI}${path}"]`);

    if (!styleElement || !styleElement.parentNode) {
      console.debug('Unable to find a stylesheet to update. Updating all css.');
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
    if (styleElement.loading) {
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

  function updateScopedCss() {
    [...document.querySelectorAll('link')]
      .filter(l => l.baseURI === document.baseURI && l.href && l.href.indexOf('.styles.css') !== -1)
      .forEach(e => updateCssElement(e));
  }
}, 500);
