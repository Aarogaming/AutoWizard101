'use strict';
const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const {execFile}=require('node:child_process');
const {promisify}=require('node:util');
const library=require('./card-library');
const bridge=require('./bridge');
function createService(config, call=bridge.call, execute=promisify(execFile)) {
    let busy=false;
    async function catalog() {
        const r=await call(config.dataRoot,bridge.request('capabilities'));
        if(!r.ok)throw new Error(r.error);return r.result;
    }
    return async function handle(action,body={}) {
        if(busy)throw new Error('An editor operation is still running');
        busy=true;
        try {
            if(!path.isAbsolute(config.dataRoot||'') || !path.isAbsolute(config.libraryRoot||''))throw new Error('Configure absolute editor owner and library folders on the server');
            const cat=await catalog();
            if(action==='catalog') {
                let names=[];try{names=await fs.readdir(config.libraryRoot);}catch(e){if(e.code!=='ENOENT')throw e;}
                return {catalog:cat,cards:names.filter(n=>/^[a-f0-9]{32}\.json$/i.test(n)).map(n=>n.slice(0,-5)),newId:crypto.randomUUID().replaceAll('-','')};
            }
            if(action==='load')return await library.load(config.libraryRoot,body.id,cat);
            if(action==='save')return await library.save(config.libraryRoot,body.card,cat);
            const view=library.validate(body.card,cat);
            if(action==='preview')return view;
            if(action!=='replay')throw new Error('Unknown editor operation');
            if(view.card.routineId!=='review.routine')throw new Error('Offline replay requires the catalog Replay routine');
            if(view.card.conditions.length || view.card.filters.length)throw new Error('This recorded-routine replay cannot verify card eligibility conditions. Use a separate unconditional Replay card; original cards remain unchanged.');
            if(!path.isAbsolute(config.executable||''))throw new Error('Configure the installed Maelstrom executable on the server');
            const graph=view.card.settings.graphFile,session=view.card.settings.sessionFile;
            if(!path.isAbsolute(graph)||!path.isAbsolute(session))throw new Error('Choose absolute saved routine and session paths');
            const reportFolder=path.join(config.libraryRoot,'replay-reports');await fs.mkdir(reportFolder,{recursive:true});
            const reportPath=path.join(reportFolder,crypto.randomUUID()+'.json');
            let exitCode=0;
            try {await execute(config.executable,['--replay-routine',graph,session,reportPath],{windowsHide:true,timeout:60000,maxBuffer:65536,shell:false});}
            catch(e){if(e.code!==2)throw new Error('Replay failed or timed out; no completion inferred');exitCode=2;}
            const stat=await fs.stat(reportPath);if(stat.size>1048576)throw new Error('Replay report too large');
            const report=JSON.parse(await fs.readFile(reportPath,'utf8'));
            if(report.desktop_dispatch_count!==0 || report.live_qualified!==false)throw new Error('Unexpected replay contract');
            return {completed:exitCode===0&&report.completed===true,reason:report.reason,reportPath,graphHash:report.sha256_graph,desktopDispatchCount:0,liveQualified:false,eligibilityTested:false};
        }finally{busy=false;}
    };
}
function register(RED) {
    if(!RED.httpAdmin)return;
    const handle=createService({dataRoot:process.env.MAELSTROM_CARD_EDITOR_DATA_ROOT,libraryRoot:process.env.MAELSTROM_CARD_LIBRARY,executable:process.env.MAELSTROM_CARD_EDITOR_EXE});
    const permission=RED.auth.needsPermission('flows.write');
    RED.httpAdmin.get('/maelstrom-cards',permission,(_req,res)=>res.sendFile(path.join(__dirname,'card-editor.html')));
    RED.httpAdmin.post('/maelstrom-cards/:action',permission,async(req,res)=>{
        try{if(Buffer.byteLength(JSON.stringify(req.body||{}))>65536)throw new Error('Editor request too large');res.json({ok:true,result:await handle(req.params.action,req.body)});}
        catch(e){res.status(400).json({ok:false,error:e.message});}
    });
}
module.exports={createService,register};
