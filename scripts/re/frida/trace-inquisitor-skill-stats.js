// Instance-only read probe for the current local player's Fireball. No engine calls or memory writes.
// Native 3.29.3b ActiveSkillInstance +0xf8/+0x138 refer to its current received stat containers.
(() => {
  const game = Process.getModuleByName('Game.patched.exe');
  const at = va => game.base.add(ptr(va).sub(ptr('0x140000000')));
  const wanted = new Set(__WANTED__);
  let actor = null;
  function pairs(stats) {
    const begin = stats.add(0xf0).readPointer(), end = stats.add(0xf8).readPointer();
    const bytes = end.sub(begin).toInt32();
    if (bytes < 0 || bytes > 160000 || bytes % 8) throw Error('skill stats bounds');
    const values = {};
    for (let p = begin; p.compare(end) < 0; p = p.add(8))
      if (wanted.has(p.readU32())) values[p.readU32()] = p.add(4).readS32();
    return values;
  }
  const local = p => !p.isNull() && p.add(8).readPointer().add(0x88).readU32() === 0x1000;
  Interceptor.attach(at('0x1421932b0'), {onEnter(args) {
    try { if (local(args[0])) actor = args[0]; } catch (_) {}
  }});
  Interceptor.attach(at('0x142156d80'), {onLeave() { actor = null; }});
  rpc.exports.inquisitorskill = () => {
    try {
      if (actor === null || !local(actor)) return null;
      const manager = actor.add(0x598);
      const begin = manager.add(0x158).readPointer(), end = manager.add(0x160).readPointer();
      const bytes = end.sub(begin).toInt32();
      if (bytes < 0 || bytes > 16384 || bytes % 16) throw Error('skill manager bounds');
      for (let p = begin; p.compare(end) < 0; p = p.add(16)) {
        const skill = p.readPointer();
        if (skill.isNull() || skill.add(0x40).readU16() !== 0x103) continue;
        const row = skill.add(0x48).readPointer();
        const name = row.readPointer().readPointer().readUtf16String();
        if (name !== 'Fireball') continue;
        return {id: 0x103, name, level: row.add(0x10).readS32(),
          values: pairs(skill.add(0xf8).readPointer()), mirror: pairs(skill.add(0x138).readPointer())};
      }
      return null;
    } catch (error) { return {error: String(error)}; }
  };
})();
