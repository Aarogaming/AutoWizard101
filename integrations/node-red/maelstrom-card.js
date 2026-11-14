'use strict';
const library=require('./card-library');
const bridge=require('./bridge');
const crypto=require('node:crypto');
module.exports=RED=>{
    require('./card-editor').register(RED);
    function Card(config) {
        RED.nodes.createNode(this,config);
        const node=this;const cancel=new AbortController();let pending=false;
        node.on('input',async(msg,send,done)=>{
            if(pending){done(new Error('Card request already pending'));return;}
            pending=true;
            try {
                if(!['card/save','card/load','card/preview','card/deal'].includes(msg.topic))throw new Error('Use card/preview, card/save, card/load or card/deal');
                const response=await bridge.call(config.dataRoot,bridge.request('capabilities'),cancel.signal);
                if(!response.ok)throw new Error(response.error);
                const catalog=response.result;
                let view;
                if(msg.topic==='card/load')view=await library.load(config.libraryRoot,config.cardId,catalog);
                else {
                    // A loaded/edited native card may be passed along a flow; validate it again.
                    const card=msg.payload?.card || JSON.parse(config.cardJson);
                    view=msg.topic==='card/save'?await library.save(config.libraryRoot,card,catalog):library.validate(card,catalog);
                }
                if(cancel.signal.aborted)return;
                if(msg.topic==='card/deal') {
                    const state=await bridge.call(config.dataRoot,bridge.request('state'),cancel.signal);
                    if(!state.ok)throw new Error(state.error);
                    // Configuration only. Execution remains a separately triggered owner command.
                    const reply=await bridge.call(config.dataRoot,bridge.request('deal',{card:view.card,revision:state.result.state.revision},crypto.randomUUID().replaceAll('-',''),true),cancel.signal);
                    if(!reply.ok)throw new Error(reply.error);
                    view.nativeAccepted=true;
                }
                msg.payload=view;send(msg);done();
            }catch(e){done(e);}finally{pending=false;}
        });
        node.on('close',()=>cancel.abort());
    }
    RED.nodes.registerType('maelstrom-card',Card);
};
