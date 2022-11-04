export function receiveHotReload() {
  return BINDING.js_to_mono_obj(new Promise((resolve) => receiveHotReloadAsync().then(resolve(0))));
}

async function receiveHotReloadAsync() {
  const cache = window.sessionStorage.getItem('blazor-webassembly-cache');
  let headers;
  if (cache) {
    headers = { 'if-none-match' : cache.etag };
  }
  const response = await fetch('/_framework/blazor-hotreload', { headers });
  if (response.status === 200) {
    const deltas = await response.json();
    if (deltas) {
      try {
        deltas.forEach(d => window.Blazor._internal.applyHotReload(d.moduleId, d.metadataDelta, d.ilDelta));
      } catch (error) {
        console.warn(error);
        return;
      } 
    }
  }
}
