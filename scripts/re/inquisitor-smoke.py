"""Prepared ordinary-Inquisitor smoke. Default self-check never attaches or launches anything.
Parent owns the runtime window and starts the pinned build through scripts/dev/live.ps1.
Run: .venv/Scripts/python.exe scripts/re/inquisitor-smoke.py --run --expected-head <full SHA>
Credentials come from --private-file and are never copied to results/stdout.
"""
import sys
from pathlib import Path

# The sibling RE tool dis.py must not shadow the standard-library module used by Frida/inspect.
if __name__ == '__main__':
    script_directory = Path(__file__).resolve().parent
    sys.path[:] = [entry for entry in sys.path if Path(entry).resolve() != script_directory]

import argparse
from collections import deque
import hashlib
import json
import math
import sqlite3
import struct
import subprocess
import time
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[2]
STAT_NAMES = ['strength','intelligence','maximum_life','critical_strike_chance_+%',
 'spell_critical_strike_chance','main_hand_critical_strike_chance',
 'life_regeneration_rate_per_minute_%','base_life_regeneration_rate_per_minute',
 'energy_shield_recovery_per_minute','current_fanaticism_charges']

def wait(fn, label, timeout=20):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        result=fn()
        if result: return result
        time.sleep(.07)
    raise AssertionError('Timeout: '+label)

def packets(node):
    # Current InstanceEntities.ReadAllocatePassive is FIXED 7 bytes: u32 node BE + u8 expansion.
    # The little-endian vector belongs to S0x33, not C0x34.
    return list(struct.pack('>HIB',0x34,node,0)),list(struct.pack('>HIBB',0x36,node,0,0))

def extend_api(source):
    anchor='const lengths={'
    assert source.count(anchor)==1, 'Inspect changed ClientControl interaction allowlist'
    return source.replace(anchor,anchor+'0x34:7,0x36:8,',1)

def data():
    table=lambda name:json.loads((ROOT/'data/tables'/f'{name}.json').read_text(encoding='utf-8-sig'))
    stats=table('Stats'); keys={r['Id']:i+1 for i,r in enumerate(stats)}
    rows=table('PassiveSkills'); graph=json.loads((ROOT/'data/passive-skill-graphs.json').read_text())
    normal={int(n['id']):list(map(int,n['adjacent'])) for n in graph['normal']['nodes']}
    asc=table('Ascendancy'); row=next(i for i,r in enumerate(asc) if r['Name']=='Inquisitor')
    nodes={r['PassiveSkillGraphId']:r for r in rows if r['PassiveSkillGraphId'] in normal and r.get('AscendancyKey')==row}
    start=next(n for n,r in nodes.items() if r['IsAscendancyStartingNode'])
    links={n:set(x for x in normal[n] if x in nodes) for n in nodes}
    for n in list(links):
        for other in list(links[n]):links[other].add(n)
    def path(end):
        q=deque([(start,[])]); seen={start}
        while q:
            n,p=q.popleft()
            if n==end:return p
            for x in sorted(links[n]):
                if x not in seen:seen.add(x);q.append((x,p+[x]))
        raise AssertionError('No connected Inquisitor path')
    def raw(path,name):
        total=0
        for n in path:
            for i,k in enumerate(nodes[n]['Stats']):
                if stats[k]['Id']==name:total+=nodes[n][f'Stat{i+1}Value']
        return total
    return keys,path,raw,asc[row]['ClassNo']

def self_check():
    assert bytes(packets(61393)[0]).hex()=='00340000efd100'
    assert len(packets(61393)[1])==8
    keys,path,_,_=data()
    assert all(k in keys for k in STAT_NAMES)
    assert len(path(32816))<=8 and len(path(3154))<=8
    source=(ROOT/'scripts/dev/client-control.js').read_text(encoding='utf-8')
    assert extend_api(source).replace('const lengths={0x34:7,0x36:8,','const lengths={',1)==source
    return {'offlineSelfCheck':'passed','clientAccess':False,'paths':{str(n):path(n) for n in (53884,32816,40059,3154)}}

def run(options):
    sys.path.insert(0,str(ROOT/'scripts/dev'))
    import client_control
    if options.private_file is None: raise ValueError('--private-file is required for --run')
    actual=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    assert options.expected_head and actual==options.expected_head,'Pin the exact checkout HEAD before runtime access'
    dll=ROOT/'src/GameServer.Gateway/bin/Debug/net10.0/GameServer.Gateway.dll'
    out=ROOT/'logs'/('inquisitor-native-'+time.strftime('%Y%m%d-%H%M%S'));out.mkdir()
    metadata={'head':actual,'gatewaySha256':hashlib.sha256(dll.read_bytes()).hexdigest(),
      'scope':'ordinary native lifecycle, received Providence/Pious/Justice stats; Augury graph allocation only',
      'limitations':['No actual damage/elapsed recovery/Fanaticism acceptance in this basic smoke'], 'status':'running'}
    results=[]; native={'trees':[],'errors':[]}
    def persist():
        (out/'metadata.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
        (out/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    def record(name,**evidence):
        results.append({'check':name,**evidence});persist();print(json.dumps({'check':name,'passed':len(results)}),flush=True)
    keys,path,raw,class_no=data()
    source=extend_api(client_control.AGENT.read_text(encoding='utf-8'))
    source=source.replace('args[0].add(0x34).writeByteArray(value.digest);', 'args[0].add(0x34).writeByteArray(value.digest); args[0].add(0x94).writeU8(0); args[0].add(0x95).writeU8(0);')
    client_control.AGENT=SimpleNamespace(read_text=lambda **_:source)
    private=json.loads(options.private_file.read_text(encoding='utf-8'))
    character=private['character']
    with client_control.ClientControl() as control:
        probe=None
        try:
            initial=control.ready()
            assert initial.get('screen')!='game','Shared client must be released at a menu before this smoke'
            control.execute({'command':'login','account':private['account'],'password':private['password']})
            del private
            probe_source=(ROOT/'scripts/re/frida/trace-ascendancies.js').read_text(encoding='utf-8')
            probe_source+='\n'+options.probe.read_text(encoding='utf-8').replace('__WANTED__',json.dumps([keys[n] for n in STAT_NAMES]))
            probe=control.session.create_script(probe_source,name='inquisitor-read-only-native-probe')
            def message(m,_):
                if m.get('type')=='error':native['errors'].append(m.get('description'))
                elif m.get('type')=='send' and isinstance(m.get('payload'),dict):
                    payload=m['payload']
                    if 'ascendancyPassives' in payload:native['trees'].append(payload['ascendancyPassives'])
                    with (out/'native.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps({'at':time.monotonic(),'event':payload})+'\n')
            probe.on('message',message);probe.load()
            def snapshot():
                values=probe.exports_sync.read().get('values')
                return None if values is None else {n:values.get(str(keys[n]),0) for n in STAT_NAMES}
            def stat(**expected):
                return wait(lambda:s if (s:=snapshot()) is not None and all(s[k]==v for k,v in expected.items()) else None,
                    'received player stat values')
            def tree(**expected):
                return wait(lambda:t if native['trees'] and (t:=native['trees'][-1]) and
                    all(t.get(k)==v for k,v in expected.items()) else None,'received passive tree')
            def active():
                return control.wait(lambda s:s.get('screen')=='game' and s.get('instanceState')==3 and s.get('character')==character,
                    'isolated character active',timeout=45)
            def command(text,transfer=False):
                active(); before=probe.exports_sync.read()['entries']
                response=control.execute({'command':'command','text':text,'wait':.4})
                if transfer and not any('already your main' in r for r in response.get('replies') or []):
                    wait(lambda:probe.exports_sync.read()['entries']>before,'new area generation',45);active();wait(snapshot,'new received player stats')
                return response
            def allocate(end):
                for node in path(end):
                    if node in tree()['allocated']:continue
                    control.rpc('interaction',bytes=packets(node)[0]);tree()
                    wait(lambda:node in native['trees'][-1]['allocated'],'allocated connected node')
                return tree()
            def reset():
                command('/reset_ascendancy');tree(ascendancyNodeCount=0)
            def inventory():
                # Read only this isolated character to obtain ids for native inventory interactions.
                with sqlite3.connect(options.db.resolve().as_uri()+'?mode=ro',uri=True) as db:
                    r=db.execute('SELECT save FROM characters WHERE name=?',(character,)).fetchone()
                assert r is not None,'Isolated character missing from the selected runtime database'
                return json.loads(r[0]).get('Inventories',{})
            def equip_wand():
                assert not inventory().get('2'),'Fresh isolated main hand must be empty'
                before={p['Id'] for p in inventory().get('0',[])}
                command('/item Driftwood Wand')
                item=wait(lambda:next((p for p in inventory().get('0',[]) if p['Id'] not in before),None),'new isolated wand')
                control.execute({'command':'inventory:pickup','inventoryId':1,'id':item['Id']})
                control.execute({'command':'inventory:place','inventoryId':3,'x':0,'y':0})
                wait(lambda:inventory().get('2'),'native wand equipment placement')
            control.execute({'command':'character:play','name':character});active();wait(snapshot,'initial player stats')
            command('/set_level 70');tree(level=70)
            command('/ascendancy normal Inquisitor',transfer=True)
            for tier in ('normal','cruel','merciless','eternal'):command('/ascendancy_points '+tier)
            command('/add_refund_points 200');tree(ascendancyPoints=8)
            reset();stat(strength=23,intelligence=23)
            record('ordinary-choice-and-eight-separate-points',native=tree(baseClass=5,classNo=class_no,ascendancyPoints=8))
            allocate(53884)
            record('Providence-received-attributes-and-critical-increase',native=stat(strength=73,intelligence=73,**{'critical_strike_chance_+%':73}))
            before=tree()['refund'];control.rpc('interaction',bytes=packets(53884)[1])
            record('native-five-point-refund',native=tree(refund=before-5),stats=stat(strength=23,intelligence=23,**{'critical_strike_chance_+%':0}))
            reset();allocate(32816)
            regen=raw(path(32816),'life_regeneration_rate_per_minute_%')+300
            received=stat(**{'life_regeneration_rate_per_minute_%':regen})
            expected=math.floor(raw(path(32816),'base_life_regeneration_rate_per_minute')+received['maximum_life']*regen/100)
            record('Sanctuary-Pious-received-life-regeneration-and-additional-ES-recovery',native=stat(**{'energy_shield_recovery_per_minute':expected}),expectedPerMinute=expected)
            control.rpc('interaction',bytes=packets(32816)[1]);wait(lambda:32816 not in tree()['allocated'],'paid Pious removal')
            record('Pious-paid-refund-clears-additional-ES-recovery',native=stat(**{'energy_shield_recovery_per_minute':0}))
            reset();allocate(40059);record('Augury-native-connected-allocation-only',native=tree(),effectAccepted=False)
            reset();equip_wand();allocate(3154)
            record('Justice-native-main-weapon-base-crit',native=stat(spell_critical_strike_chance=830))
            command('/set_stat global_cannot_crit 1');record('Justice-cannot-crit-wins',native=stat(spell_critical_strike_chance=0))
            command('/set_stat global_cannot_crit off');stat(spell_critical_strike_chance=830)
            control.execute({'command':'character:select'});control.execute({'command':'character:play','name':character});active()
            record('Justice-relog-preserves-native-class-nodes-and-critical-base',native=tree(baseClass=5,classNo=class_no),stats=stat(spell_critical_strike_chance=830))
            assert not native['errors'], 'Read-only probe reported an error'
            reset();metadata['status']='passed';persist()
        except Exception as error:
            metadata['status']='failed';metadata['errorType']=type(error).__name__;metadata['error']=str(error);persist();raise
        finally:
            try:
                if control.status().get('screen')=='game':control.execute({'command':'character:select'})
                if control.status().get('screen')=='characters':control.execute({'command':'logout'})
            finally:
                if probe is not None:probe.unload()
    return {'status':metadata['status'],'checks':len(results),'evidence':str(out)}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--run',action='store_true');parser.add_argument('--expected-head')
    parser.add_argument('--private-file',type=Path,help='Private JSON: account, password, character; never printed'); parser.add_argument('--db',type=Path,default=ROOT/'data/inquisitor-native.db')
    parser.add_argument('--probe',type=Path,default=ROOT/'scripts/re/frida/trace-inquisitor-stats.js')
    args=parser.parse_args()
    print(json.dumps(run(args) if args.run else self_check()),flush=True)
