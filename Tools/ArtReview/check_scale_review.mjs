// Review the offline page in a dedicated headless Chrome session, then close that session.
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const out = path.join(root, 'ArtReview/ScaleStudy');
const port = (await fs.readFile(path.join(root,'.utmp/scale-study-cdp/DevToolsActivePort'),'utf8')).split('\n')[0];
const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
let next=0;const pending=new Map();
ws.onmessage=event=>{const m=JSON.parse(event.data);if(pending.has(m.id)){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result);}};
function call(method,params={}){return new Promise((resolve,reject)=>{const id=++next;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
function assert(ok,message){if(!ok)throw Error(message);results.push('PASS '+message);}
const results=[];
try {
 await call('Page.enable');
 await call('Emulation.setDeviceMetricsOverride',{width:1040,height:1980,deviceScaleFactor:1,mobile:false});
 await call('Page.navigate',{url:pathToFileURL(path.join(out,'index.html')).href});
 for(let i=0;i<50;i++){if(await evaluate("document.readyState==='complete' && typeof setMode==='function'"))break;await new Promise(r=>setTimeout(r,100));}
 await evaluate('Promise.all(Array.from(document.images,i=>i.decode()))');
 assert(await evaluate("Array.from(document.images).every(i=>i.naturalWidth===896&&i.naturalHeight===384)"),'all three Unity renders loaded at the expected size');
 assert(await evaluate("document.getElementById('source').getAttribute('aria-pressed')==='true'"),'original-art mode is the initial selection');
 const snap=async name=>{const r=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});await fs.writeFile(path.join(out,name),Buffer.from(r.data,'base64'));};
 await snap('comparison.png');
 await evaluate("document.getElementById('32px').click(); Promise.all(Array.from(document.images,i=>i.decode()))");
 assert(await evaluate("document.getElementById('32px').getAttribute('aria-pressed')==='true' && document.getElementById('C').src===images['C-32px']"),'building density button switches all comparison images');
 await snap('comparison-32px.png');
 await evaluate("document.getElementById('zoom').click()");
 assert(await evaluate("document.body.classList.contains('zoom') && document.getElementById('C').getBoundingClientRect().width===1792"),'detail zoom shows exact doubled raster size');
 await evaluate("document.getElementById('zoom').click(); document.getElementById('source').click()");
 assert(await evaluate("!document.body.classList.contains('zoom') && document.getElementById('A').src===images['A-source']"),'zoom and original-art mode restore correctly');
 await call('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:false});
 assert(await evaluate('document.documentElement.scrollWidth<=390'),'narrow layout does not overflow the page');
 await fs.writeFile(path.join(out,'review-checks.txt'),results.join('\n')+'\nCOMPLETE\n');
 console.log(results.join('\n'));
} finally {
 await call('Browser.close').catch(()=>{});ws.close();
}
