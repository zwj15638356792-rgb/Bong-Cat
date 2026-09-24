// Inspect the original compiled rig without modifying it. Also checks that its
// keyboard, pointer and click channels produce real changes in the mesh data.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const ctx = { require, __dirname: root, process, console, Buffer, setTimeout, clearTimeout, WebAssembly };
vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(root, 'public/js/live2dcubismcore.min.js'), 'utf8'), ctx);

setTimeout(() => {
  const core = ctx.Live2DCubismCore;
  const bytes = fs.readFileSync(path.join(root, 'src-tauri/assets/models/standard/demomodel.moc3'));
  const moc = core.Moc.fromArrayBuffer(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
  const model = core.Model.fromMoc(moc);
  const parameters = model.parameters;
  function reset() { parameters.values.set(parameters.defaultValues); model.update(); }
  function snapshot() {
    const d = model.drawables;
    return Array.from(d.ids, (id, i) => ({
      id, texture: d.textureIndices[i], order: d.renderOrders[i], opacity: d.opacities[i],
      positions: Array.from(d.vertexPositions[i]), uvs: Array.from(d.vertexUvs[i]), indices: Array.from(d.indices[i]),
    }));
  }
  reset();
  const baseline = snapshot();
  const checks = [];
  for (const id of ['CatParamLeftHandDown', 'ParamMouseX', 'ParamMouseY', 'ParamMouseLeftDown', 'ParamMouseRightDown', 'ParamAngleX', 'ParamAngleY']) {
    reset();
    const index = parameters.ids.indexOf(id);
    assert(index >= 0, `Missing ${id}`);
    parameters.values[index] = parameters.maximumValues[index];
    model.update();
    assert.notEqual(JSON.stringify(snapshot()), JSON.stringify(baseline), `No animation response for ${id}`);
    checks.push(`${id}: PASS`);
  }
  fs.mkdirSync(path.join(root, 'tmp'), { recursive: true });
  fs.writeFileSync(path.join(root, 'tmp/model-geometry.json'), JSON.stringify({ canvas: model.canvasinfo, meshes: baseline }));
  fs.writeFileSync(path.join(root, 'tmp/rig-checks.txt'), `${checks.join('\n')}\n`);
  console.log(checks.join('\n'));
  model.release();
  moc._release();
}, 500);
