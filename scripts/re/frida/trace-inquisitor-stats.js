// Instance-only read probe: no sockets, packet buffers, login hooks, keys, or credential capture.
(() => {
  const game = Process.getModuleByName('Game.patched.exe');
  const at = va => game.base.add(ptr(va).sub(ptr('0x140000000')));
  const wanted = new Set(__WANTED__), active = {}, tracked = new Set(), lives = new Map();
  let stats = null, entries = 0;
  function pairs(s) {
    const begin = s.add(0xf0).readPointer(), end = s.add(0xf8).readPointer();
    const bytes = end.sub(begin).toInt32();
    if (bytes < 0 || bytes > 160000 || bytes % 8) return null;
    const values = {};
    for (let p = begin; p.compare(end) < 0; p = p.add(8)) {
      const key = p.readU32();
      if (wanted.has(key)) values[key] = p.add(4).readS32();
    }
    return values;
  }
  Interceptor.attach(at('0x141e6a190'), {
    onEnter(args) {
      this.local = false;
      try {
        const id = args[0].add(8).readPointer().add(0x88).readU32();
        if (tracked.has(id)) lives.set(id, args[0]);
        this.local = id === 0x1000;
        if (this.local) active[this.threadId] = (active[this.threadId] || 0) + 1;
      } catch (_) {}
    },
    onLeave() { if (this.local) active[this.threadId]--; }
  });
  Interceptor.attach(at('0x1424cfa40'), {
    onEnter(args) {
      if (active[this.threadId]) try { if (pairs(args[0]) !== null) stats = args[0]; } catch (_) {}
    }
  });
  // S0x21 area entry generation: do not admit commands on the old area's instanceState=3.
  Interceptor.attach(at('0x142156d80'), {
    onLeave() { entries++; stats = null; lives.clear(); }
  });
  rpc.exports.read = () => {
    try { return {entries, values: stats === null ? null : pairs(stats)}; }
    catch (_) { stats = null; return {entries, values: null}; }
  };
  rpc.exports.track = id => { tracked.add(id); };
  rpc.exports.life = id => {
    try { return lives.has(id) ? lives.get(id).add(0x1a8).readU32() : null; }
    catch (_) { lives.delete(id); return null; }
  };
})();
