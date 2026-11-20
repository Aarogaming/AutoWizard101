'use strict';
// Opt-in integration test: only the isolated task43 disconnected host/editor.
const {test} = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const bridge = require('./bridge');
const enabled = process.env.MAELSTROM_EDITOR_SMOKE === '1';
test('editor persists edits and evaluates a real state-to-stop flow', {skip:!enabled}, async () => {
    const root = require('node:path').resolve('.local/node-red-43/data');
    const api = (command, args={}) => bridge.call(root, bridge.request(command,args,crypto.randomUUID().replaceAll('-',''),true));
    const base = 'http://127.0.0.1:18843';
    const original = await (await fetch(base+'/flows')).json();
    assert.ok(original.some(n=>n.id==='read' && n.name==='Read Maelstrom state'));
    assert.equal((await api('snapshot')).result.connected,false);
    const deploy = async flow => {
        const r=await fetch(base+'/flows',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(flow)});
        assert.equal(r.status,204);
    };
    const reset = async () => {const s=await api('state');assert.equal((await api('reset',{revision:s.result.state.revision})).ok,true);};
    const run = async () => {assert.equal((await fetch(base+'/inject/run',{method:'POST'})).status,200);await new Promise(r=>setTimeout(r,500));};
    try {
        await reset();
        assert.equal((await api('state')).result.state.status,'Stopped');
        const blocked=structuredClone(original);blocked.find(n=>n.id==='condition').rules[0].v='NeverMatches';
        await deploy(blocked); await run();
        assert.equal((await api('state')).result.state.status,'Stopped','false condition must not call Stop');
        await deploy(original);await run();
        assert.equal((await api('state')).result.state.status,'Paused','existing Stop action changes owner state');
        assert.equal((await api('snapshot')).result.connected,false);
    } finally {await deploy(original);}
});
