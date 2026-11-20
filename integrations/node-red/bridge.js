'use strict';
const net = require('node:net');
const path = require('node:path');
const crypto = require('node:crypto');
const MAX = 262144;
const READS = new Set(['state', 'snapshot', 'catalog', 'capabilities', 'validate-routine', 'preview']);
const WRITES = new Set(['deal', 'replace', 'start', 'step', 'pause', 'stop', 'reset']);
function pipeName(root) {
    if (!path.win32.isAbsolute(root) || root.startsWith('\\\\')) throw new Error('Use an absolute local Windows data root');
    const full = path.win32.normalize(root);
    const normalized = full.length > path.win32.parse(full).root.length ? full.replace(/\\$/, '') : full;
    return '\\\\.\\pipe\\maelstrom-deck-' + crypto.createHash('sha256').update(normalized.toUpperCase()).digest('hex').slice(0, 24).toUpperCase();
}
function request(command, args = {}, requestId, allowActions = false) {
    if (!READS.has(command) && !(allowActions && WRITES.has(command))) throw new Error('Command not enabled');
    if (!args || typeof args !== 'object' || Array.isArray(args)) throw new Error('Arguments must be an object');
    if (WRITES.has(command) && !/^[a-f0-9]{32}$/i.test(requestId || '')) throw new Error('Actions require an explicit GUID-N requestId; reuse it for ambiguous retries');
    const bytes = Buffer.from(JSON.stringify({command, arguments: args, requestId}, (_, v) => {
        if (typeof v === 'number' && !Number.isFinite(v)) throw new Error('Nonfinite value');
        return v;
    }));
    if (bytes.length > MAX) throw new Error('Request exceeds size budget');
    return bytes;
}
function call(root, bytes, signal, connect = net.createConnection) {
    return new Promise((resolve, reject) => {
        let socket, buffer = Buffer.alloc(0), settled = false;
        const finish = (err, value) => {
            if (settled) return;
            settled = true; clearTimeout(timer); signal?.removeEventListener('abort', abort); socket?.destroy();
            err ? reject(err) : resolve(value);
        };
        const abort = () => finish(new Error('Cancelled; outcome unconfirmed'));
        const timer = setTimeout(() => finish(new Error('Owner timeout; outcome unconfirmed')), 8000);
        signal?.addEventListener('abort', abort, {once:true});
        if (signal?.aborted) return abort();
        try {
            socket = connect(pipeName(root));
            socket.on('connect', () => { const header = Buffer.alloc(4); header.writeInt32LE(bytes.length); socket.write(Buffer.concat([header, bytes])); });
            socket.on('error', e => finish(e));
            socket.on('end', () => finish(new Error('Owner disconnected; outcome unconfirmed')));
            socket.on('data', chunk => {
                if (buffer.length + chunk.length > MAX + 4) return finish(new Error('Response exceeds size budget'));
                buffer = Buffer.concat([buffer, chunk]);
                if (buffer.length < 4) return;
                const count = buffer.readInt32LE();
                if (count < 2 || count > MAX) return finish(new Error('Invalid frame length'));
                if (buffer.length < count + 4) return;
                try {
                    const reply = JSON.parse(buffer.subarray(4, count + 4));
                    if (typeof reply.ok !== 'boolean' || reply.schemaVersion !== 1) throw new Error('Unsupported owner reply');
                    finish(null, reply);
                } catch (e) { finish(e); }
            });
        } catch (e) { finish(e); }
    });
}
// Latest published snapshots, not a lossless event log or a game observation producer.
function subscribe(read, emit, interval = 1000) {
    const delay = Math.max(1000, Math.min(30000, Number(interval) || 1000));
    const controller = new AbortController();
    const session = crypto.randomUUID();
    let timer, sequence = 0, previous, disconnected = true;
    async function tick() {
        try {
            const reply = await read(controller.signal);
            if (controller.signal.aborted) return;
            if (!reply.ok) throw new Error(reply.error || 'Owner unavailable');
            const encoded = JSON.stringify(reply.result);
            const kind = disconnected ? 'snapshot' : encoded === previous ? 'heartbeat' : 'change';
            disconnected = false; previous = encoded;
            emit({schemaVersion:1, session, sequence:++sequence, kind, sampledUtc:new Date().toISOString(), result:reply.result});
        } catch (e) {
            if (controller.signal.aborted) return;
            disconnected = true; previous = undefined;
            emit({schemaVersion:1, session, sequence:++sequence, kind:'unavailable', result:null, error:e.message});
        }
        if (!controller.signal.aborted) timer = setTimeout(tick, delay);
    }
    tick();
    return () => { controller.abort(); clearTimeout(timer); };
}
module.exports = {pipeName, request, call, subscribe};
