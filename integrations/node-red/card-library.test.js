'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const path=require('node:path');
const os=require('node:os');
const library=require('./card-library');
const catalog={routines:[{id:'review.routine',description:'Replay only',qualification:'Offline',available:true,options:[{name:'graphFile',required:true,kind:'file',label:'Graph'},{name:'sessionFile',required:true,kind:'file',label:'Session'}]}],facts:[{id:'energy',kind:'number',producerAvailable:false}],routineFacts:{}};
const card=()=>({id:'a473a22c930b4e859403bc98d4bdb874',routineId:'review.routine',title:'Replay',inDeck:true,priority:50,runs:1,attemptLimit:1,cooldownSeconds:5,settings:{graphFile:'graph.json',sessionFile:'session.json'},conditions:[],filters:[]});
test('front/back derive from one native spec and catalog',()=>{
    const c=card();c.conditions=[{fact:'energy',operator:'gte',value:10}];
    const v=library.validate(c,catalog);assert.equal(v.front.requirements[0],'energy gte 10');assert.equal(v.back.settings.graphFile,'graph.json');assert.equal(v.liveQualified,false);
    assert.throws(()=>library.validate({...c,conditions:[{fact:'invented',operator:'eq',value:true}]},catalog));
    assert.throws(()=>library.validate({...c,conditions:[{fact:'energy',operator:'gte',value:'10'}]},catalog));
    assert.throws(()=>library.validate({...c,settings:{shell:'go'}},catalog));
    assert.throws(()=>library.validate({...c,id:'../escape'},catalog));
    assert.throws(()=>library.validate({...c,cooldownSeconds:0},catalog));
});
test('save/load preserves native card and refuses overwrites or catalog drift',async()=>{
    const root=await fs.mkdtemp(path.join(os.tmpdir(),'maelstrom-card-'));
    try {
        await library.save(root,card(),catalog);
        assert.deepEqual((await library.load(root,card().id,catalog)).card,card());
        await assert.rejects(library.save(root,card(),catalog),{code:'EEXIST'});
        await assert.rejects(library.load(root,card().id,{...catalog,routines:[]}));
    }finally{await fs.rm(root,{recursive:true,force:true});}
});
test('native owner accepts saved card and scheduler replays it',{skip:process.env.MAELSTROM_CARD_SMOKE!=='1'},async()=>{
    const bridge=require('./bridge');const crypto=require('node:crypto');
    const root=path.resolve('.local/card-author-44/data');
    const call=async(command,args={})=>{const r=await bridge.call(root,bridge.request(command,args,crypto.randomUUID().replaceAll('-',''),true));assert.equal(r.ok,true,r.error);return r.result;};
    const native=await call('capabilities');const c=card();c.id=crypto.randomUUID().replaceAll('-','');
    c.settings={graphFile:path.resolve('.local/teaching-action-39/linked-card/routine.json'),sessionFile:path.resolve('.local/teaching-release-40/fresh-linked/session.json')};
    const folder=path.resolve('.local/card-author-44/library');
    await library.save(folder,c,native);const loaded=await library.load(folder,c.id,native);
    let state=await call('state');
    await call('deal',{card:{...loaded.card,conditions:[{fact:'energy',operator:'gte',value:10}]},revision:state.state.revision});
    const blocked=await call('preview');assert.ok(blocked.eligibility.some(e=>e.cardId===c.id&&!e.ready));
    state=await call('state');await call('deal',{card:loaded.card,revision:state.state.revision});
    const preview=await call('preview');assert.ok(preview.eligibility.some(e=>e.cardId===c.id&&e.ready));
    await call('step');let result;
    for(let i=0;i<30;i++){result=await call('state');if(result.state.progress[c.id].completed===1)break;await new Promise(r=>setTimeout(r,100));}
    assert.equal(result.state.progress[c.id].completed,1);
    assert.equal((await call('snapshot')).connected,false);
    await fs.writeFile(path.resolve('.local/card-author-44/verified-state.json'),JSON.stringify(result,null,2));
});
test('author node saves, loads and deals the loaded native card',{skip:process.env.MAELSTROM_CARD_SMOKE!=='1'},async()=>{
    let Constructor;
    require('./maelstrom-card')({nodes:{registerType:(_,c)=>{Constructor=c;},createNode:n=>{
        const events=new(require('node:events').EventEmitter)();n.on=events.on.bind(events);n.emit=events.emit.bind(events);
    }}});
    const c=card();c.id=require('node:crypto').randomUUID().replaceAll('-','');
    c.settings={graphFile:path.resolve('.local/teaching-action-39/linked-card/routine.json'),sessionFile:path.resolve('.local/teaching-release-40/fresh-linked/session.json')};
    const node=new Constructor({dataRoot:path.resolve('.local/card-author-44/data'),libraryRoot:path.resolve('.local/card-author-44/node-library'),cardId:c.id,cardJson:JSON.stringify(c)});
    const input=(topic,payload)=>new Promise((resolve,reject)=>{let output;node.emit('input',{topic,payload},m=>{output=m.payload;},e=>e?reject(e):resolve(output));});
    try {
        await input('card/save');const loaded=await input('card/load');assert.deepEqual(loaded.card,c);
        loaded.card.title='Loaded card edited in flow';
        const dealt=await input('card/deal',loaded);assert.equal(dealt.nativeAccepted,true);assert.equal(dealt.front.title,loaded.card.title);
        await assert.rejects(input('card/save'),{code:'EEXIST'});
        await assert.rejects(input('unsupported'));
    }finally{node.emit('close');}
});
