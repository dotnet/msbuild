export function receiveHotReload() {
  return BINDING.js_to_mono_obj(new Promise((resolve) => receiveHotReloadAsync().then(resolve(0))));
}

export async function receiveHotReloadAsync() {
  const response = await fetch('/_framework/blazor-hotreload');
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
