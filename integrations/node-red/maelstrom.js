'use strict';
const bridge = require('./bridge');
module.exports = RED => {
    function Maelstrom(config) {
        RED.nodes.createNode(this, config);
        const node = this;
        let stop, pending = false;
        const cancellation = new AbortController();
        node.on('input', async (msg, send, done) => {
            try {
                if (msg.topic === 'unsubscribe') { stop?.(); stop = null; done(); return; }
                if (msg.topic === 'subscribe') {
                    stop?.();
                    const command = config.channel === 'state' ? 'state' : 'snapshot';
                    stop = bridge.subscribe(signal => bridge.call(config.dataRoot, bridge.request(command), signal), payload => node.send({topic:'maelstrom/state', payload}), config.interval);
                    done(); return;
                }
                if (pending) throw new Error('One request already pending; no queue created');
                const bytes = bridge.request(msg.topic, msg.payload || {}, msg.requestId, config.allowActions === true);
                pending = true;
                try { msg.payload = await bridge.call(config.dataRoot, bytes, cancellation.signal); send(msg); }
                finally { pending = false; }
                done();
            } catch (e) { done(e); }
        });
        node.on('close', () => { stop?.(); cancellation.abort(); });
    }
    RED.nodes.registerType('maelstrom-local', Maelstrom);
};
