'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs/promises');const path=require('node:path');const crypto=require('node:crypto');
const {createService}=require('./card-editor');
const catalog={routines:[{id:'review.routine',description:'Replay',qualification:'Offline',available:true,options:[{name:'graphFile',kind:'file',required:true},{name:'sessionFile',kind:'file',required:true}]}],facts:[{id:'energy',kind:'number'}],routineFacts:{}};
const card=()=>({id:crypto.randomUUID().replaceAll('-',''),routineId:'review.routine',title:'Replay',inDeck:true,priority:1,runs:1,attemptLimit:1,cooldownSeconds:5,settings:{graphFile:path.resolve('graph.json'),sessionFile:path.resolve('session.json')},conditions:[],filters:[]});
test('editor saves, loads and blocks unsupported eligibility without executing',async()=>{
 const root=path.resolve('.local/card-editor-45/test-'+crypto.randomUUID());let executions=0;
 const service=createService({dataRoot:root,libraryRoot:root,executable:path.resolve('Maelstrom.exe')},async()=>({ok:true,result:catalog}),async()=>{executions++;});
 const c=card();await service('save',{card:c});assert.deepEqual((await service('load',{id:c.id})).card,c);await assert.rejects(service('save',{card:c}),{code:'EEXIST'});
 c.conditions=[{fact:'energy',operator:'gte',value:10}];await assert.rejects(service('replay',{card:c}),/cannot verify/);assert.equal(executions,0);
 await assert.rejects(service('load',{id:'../x'}));
});
test('replay validates report and launches only the existing offline command',async()=>{
 const root=path.resolve('.local/card-editor-45/test-'+crypto.randomUUID());const c=card();
 const service=createService({dataRoot:root,libraryRoot:root,executable:path.resolve('Maelstrom.exe')},async()=>({ok:true,result:catalog}),async(exe,args,options)=>{
  assert.equal(args[0],'--replay-routine');assert.equal(options.shell,false);assert.equal(options.windowsHide,true);
  await fs.writeFile(args[3],JSON.stringify({completed:true,reason:'observed-completion',desktop_dispatch_count:0,live_qualified:false,sha256_graph:'test'}));
 });
 const result=await service('replay',{card:c});assert.equal(result.completed,true);assert.equal(result.eligibilityTested,false);
});
test('editor refuses overlapping operations',async()=>{
 let release;const root=path.resolve('.local/card-editor-45/test-'+crypto.randomUUID());
 const service=createService({dataRoot:root,libraryRoot:root},()=>new Promise(r=>release=r));
 const pending=service('catalog');await assert.rejects(service('catalog'),/still running/);release({ok:true,result:catalog});await pending;
});
