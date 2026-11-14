'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const MAX = 65536;
const clone = x => JSON.parse(JSON.stringify(x));
function validate(card, catalog) {
    if (!card || !/^[a-fA-F0-9]{32}$/.test(card.id || '')) throw new Error('Use a simple card ID');
    const known = ['id','routineId','title','inDeck','priority','runs','attemptLimit','cooldownSeconds','settings','conditions','filters'];
    if (Object.keys(card).some(k=>!known.includes(k))) throw new Error('Unknown card field');
    const routine = catalog?.routines?.find(r=>r.id===card.routineId);
    if (!routine) throw new Error('Select a routine from the native catalog');
    if (typeof card.title!=='string' || !card.title.trim() || card.title.length>100) throw new Error('Provide a short card title');
    if (typeof card.inDeck!=='boolean') throw new Error('inDeck must be explicit');
    for (const [key,min,max] of [['priority',0,999],['runs',1,100],['attemptLimit',1,100],['cooldownSeconds',1,3600]])
        if (!Number.isInteger(card[key]) || card[key]<min || card[key]>max) throw new Error('Invalid '+key);
    if (card.attemptLimit<card.runs) throw new Error('Attempt limit must cover run limit');
    const settings=card.settings || {};
    if (typeof settings!=='object' || Array.isArray(settings)) throw new Error('Settings must be an object');
    for (const [key,value] of Object.entries(settings)) {
        const option=routine.options.find(o=>o.name===key);
        if (!option || typeof value!=='string' || value.length>4096) throw new Error('Invalid setting '+key);
        if (option.kind.startsWith('choice:') && !option.kind.slice(7).split(',').includes(value)) throw new Error('Unsupported choice '+key);
    }
    for (const o of routine.options) if (o.required && !settings[o.name]?.trim()) throw new Error('Missing '+o.label);
    const extension=catalog.routineFacts?.[routine.id] || [];
    const facts=[...(catalog.facts || []),...extension];
    if (!Array.isArray(card.conditions) || card.conditions.length>16) throw new Error('Invalid conditions');
    for (const c of card.conditions) {
        const fact=facts.find(f=>f.id===c.fact);
        if (!fact || Object.keys(c).some(k=>!['fact','operator','value'].includes(k))) throw new Error('Unknown condition fact/field');
        if (!['eq','gte','lte'].includes(c.operator) || (c.operator!=='eq' && fact.kind!=='number')) throw new Error('Invalid condition operator');
        if (typeof c.value!==fact.kind || (fact.kind==='number' && !Number.isFinite(c.value))) throw new Error('Wrong condition value type');
    }
    if (!Array.isArray(card.filters) || card.filters.length>8) throw new Error('Invalid filters');
    for (const f of card.filters) {
        if (facts.find(x=>x.id===f.fact)?.kind!=='string' || !Array.isArray(f.include) || !Array.isArray(f.exclude) ||
            [...f.include,...f.exclude].length>64 || [...f.include,...f.exclude].some(x=>typeof x!=='string' || !x || x.length>256)) throw new Error('Invalid string filter');
    }
    if (Buffer.byteLength(JSON.stringify(card))>MAX) throw new Error('Card too large');
    return {card:clone(card),front:{title:card.title,description:routine.description,requirements:card.conditions.map(c=>`${c.fact} ${c.operator} ${JSON.stringify(c.value)}`),filters:clone(card.filters),qualification:routine.qualification,available:routine.available,limits:{runs:card.runs,attemptLimit:card.attemptLimit,cooldownSeconds:card.cooldownSeconds}},back:{routineId:routine.id,options:clone(routine.options),settings:clone(settings)},liveQualified:false};
}
function file(root,id) {
    if (!path.isAbsolute(root) || !/^[a-fA-F0-9]{32}$/.test(id)) throw new Error('Absolute library folder and simple ID required');
    return path.join(root,id+'.json');
}
async function save(root,card,catalog) {
    const checked=validate(card,catalog);await fs.mkdir(root,{recursive:true});
    // Exclusive creation keeps prior card versions intact. Choose a new ID to save a revision.
    await fs.writeFile(file(root,card.id),JSON.stringify(checked.card,null,2)+'\n',{flag:'wx'});return checked;
}
async function load(root,id,catalog) {
    const location=file(root,id);const handle=await fs.open(location,'r');
    try {if ((await handle.stat()).size>MAX) throw new Error('Card too large');const card=JSON.parse(await handle.readFile('utf8'));if(card.id!==id)throw new Error('Saved ID mismatch');return validate(card,catalog);}
    finally {await handle.close();}
}
module.exports={validate,save,load};
