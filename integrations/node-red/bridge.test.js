'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const {EventEmitter} = require('node:events');
const b = require('./bridge');
test('pipe normalization and command admission', () => {
    assert.equal(b.pipeName('C:\\Data\\'), b.pipeName('c:\\data'));
    assert.throws(() => b.pipeName('relative'));
    assert.throws(() => b.request('start'));
    assert.throws(() => b.request('start', {}, 'bad', true));
    assert.throws(() => b.request('connect', {}, 'a'.repeat(32), true));
    assert.equal(JSON.parse(b.request('start', {}, 'a'.repeat(32), true)).requestId, 'a'.repeat(32));
    assert.throws(() => b.request('preview', {x:NaN}));
    assert.throws(() => b.request('preview', {x:'a'.repeat(262144)}));
});
function fake(chunks) {
    return () => {
        const s = new EventEmitter(); s.destroy = () => {}; s.write = () => { for (const c of chunks) s.emit('data',c); };
        process.nextTick(() => s.emit('connect')); return s;
    };
}
test('fragmented native framing and invalid lengths', async () => {
    const body = Buffer.from(JSON.stringify({ok:true,schemaVersion:1,result:{facts:null}}));
    const h = Buffer.alloc(4); h.writeInt32LE(body.length);
    const result = await b.call('C:\\Data', b.request('snapshot'), null, fake([h.subarray(0,2),h.subarray(2),body]));
    assert.equal(result.result.facts,null);
    h.writeInt32LE(999999);
    await assert.rejects(b.call('C:\\Data', b.request('snapshot'), null, fake([h])));
});
test('subscription recovers with snapshot, has sequence and stops', async () => {
    let reads = 0; const events=[];
    const stop = b.subscribe(async () => { reads++; return reads===1 ? {ok:false,error:'offline'} : {ok:true,result:{facts:null}}; }, e=>events.push(e), 1000);
    // Wait for the third event (about 2 s of chained timers) with a generous deadline, not a fixed sleep:
    // a loaded CI runner can delay chained timers by more than the old 150 ms margin.
    const deadline = Date.now() + 10000;
    while (events.length < 3 && Date.now() < deadline) await new Promise(r=>setTimeout(r,25));
    stop();
    assert.deepEqual(events.map(e=>e.kind), ['unavailable','snapshot','heartbeat']);
    assert.deepEqual(events.map(e=>e.sequence),[1,2,3]);
    assert.equal(events[1].result.facts,null);
});
test('cancel in-flight subscription emits nothing', async () => {
    let resolve; const events=[];
    const stop=b.subscribe(()=>new Promise(r=>{resolve=r;}),e=>events.push(e));
    stop(); resolve({ok:true,result:{}}); await new Promise(r=>setImmediate(r));
    assert.equal(events.length,0);
});
