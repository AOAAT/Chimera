import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..'),out=path.join(root,'ArtReview/RotationStudy');
const port=(await fs.readFile(path.join(root,'.utmp/rotation-study-cdp/DevToolsActivePort'),'utf8')).split('\n')[0];
const target=await(await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();
const ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
let next=0;const pending=new Map(),results=[],errors=[];
ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.method==='Runtime.exceptionThrown')errors.push(m.params);if(pending.has(m.id)){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result);}};
function call(method,params={}){return new Promise((resolve,reject)=>{const id=++next;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
function check(ok,label){if(!ok)throw Error(label);results.push('PASS '+label);}
async function canvas(id,name){const uri=await evaluate(`document.getElementById('${id}').toDataURL('image/png')`);await fs.writeFile(path.join(out,name),Buffer.from(uri.split(',')[1],'base64'));}
try{
 await call('Page.enable');await call('Runtime.enable');
 await call('Emulation.setDeviceMetricsOverride',{width:1320,height:1160,deviceScaleFactor:1,mobile:false});
 await call('Page.navigate',{url:pathToFileURL(path.join(out,'index.html')).href});
 let ready=false;for(let i=0;i<100;i++){ready=await evaluate('Boolean(window.study&&study.ready)');if(ready)break;await new Promise(r=>setTimeout(r,100));}
 check(ready,'WebGL study loads offline with all six source sprites');
 const geometry=await evaluate(`(()=>{let max=0;for(const ref of study.model.checks){const l=study.model.layers[ref.layer];const c=study.corner(l,ref.angle,'both'),m=study.transform(l,l.muzzleX,l.muzzleY,ref.angle,'both');max=Math.max(max,Math.hypot(c[0]-ref.corner.x,c[1]-ref.corner.y),Math.hypot(m[0]-ref.muzzle.x,m[1]-ref.muzzle.y),Math.hypot(l.slotX-ref.pivot.x,l.slotY-ref.pivot.y));}return {count:study.model.checks.length,max};})()`);
 check(geometry.count===42&&geometry.max<.00001,'42 browser corner/pivot/muzzle transforms agree with Unity within 0.00001 world unit');
 await evaluate("study.state.grid=false;study.state.guides=false;study.setAngle(22.5)");
 const comparisons=[];
 for(const [id,size] of [['direct',600],['low',300]]){
  const data='data:image/png;base64,'+(await fs.readFile(path.join(out,`unity-22.5-${size}.png`))).toString('base64');
  const result=await evaluate(`(async()=>{const im=new Image();im.src=${JSON.stringify(data)};await im.decode();const c=document.createElement('canvas');c.width=c.height=${size};const ctx=c.getContext('2d');ctx.drawImage(im,0,0);const a=ctx.getImageData(0,0,c.width,c.height).data;ctx.clearRect(0,0,c.width,c.height);ctx.drawImage(document.getElementById('${id}'),0,0);const b=ctx.getImageData(0,0,c.width,c.height).data;let foreground=0,mismatch=0,allMismatch=0;for(let i=0;i<a.length;i+=4){const diff=Math.max(Math.abs(a[i]-b[i]),Math.abs(a[i+1]-b[i+1]),Math.abs(a[i+2]-b[i+2]));const fg=Math.max(Math.abs(a[i]-51),Math.abs(a[i+1]-61),Math.abs(a[i+2]-64),Math.abs(b[i]-51),Math.abs(b[i+1]-61),Math.abs(b[i+2]-64))>4;if(diff>8)allMismatch++;if(fg){foreground++;if(diff>8)mismatch++;}}return {size:${size},foreground,mismatch,allMismatch,foregroundAgreement:1-mismatch/foreground};})()`);
  comparisons.push(result);await canvas(id,`browser-22.5-${size}.png`);
 }
 await fs.writeFile(path.join(out,'unity-browser-comparison.json'),JSON.stringify({geometry,comparisons},null,2)+'\n');
 check(comparisons.every(x=>x.foregroundAgreement>.97),'both rendering modes match Unity reference foreground pixels above 97% (RGB tolerance 8)');
 await evaluate("study.setAngle(0)");const at0=await evaluate("document.getElementById('direct').toDataURL()");
 await evaluate("study.setAngle(360)");check(at0===await evaluate("document.getElementById('direct').toDataURL()"),'full rotation returns to the same image');
 await evaluate("document.getElementById('target').value='3';document.getElementById('target').dispatchEvent(new Event('change'));study.setAngle(45)");
 check(await evaluate("study.state.target==='3' && rotation(study.model.layers[4],45,study.state.target)===study.model.layers[4].angle"),'single-component mode leaves the other weapon at its assembly angle');
 await evaluate("document.getElementById('play').click()");await new Promise(r=>setTimeout(r,450));
 check(await evaluate('study.state.angle>45'),'animation advances the inspection angle');
 await evaluate("document.getElementById('play').click()");const angle=await evaluate('study.state.angle');await new Promise(r=>setTimeout(r,150));
 check(angle===await evaluate('study.state.angle'),'pause holds the exact inspection angle');
 await evaluate("document.querySelector('[data-angle=\"22.5\"]').click();document.getElementById('target').value='both';document.getElementById('target').dispatchEvent(new Event('change'));document.getElementById('guides').checked=true;document.getElementById('guides').dispatchEvent(new Event('change'));document.getElementById('grid').checked=true;document.getElementById('grid').dispatchEvent(new Event('change'))");
 check(await evaluate('study.state.angle===22.5&&study.state.grid&&study.state.guides'),'angle preset and inspection overlays work');
 await evaluate("document.getElementById('zoom').click()");check(await evaluate("document.getElementById('direct').getBoundingClientRect().width===1200&&document.getElementById('low').width===300"),'detail zoom preserves the low-resolution render target');
 await evaluate("document.getElementById('zoom').click()");
 const shotHeight=await evaluate('Math.ceil(document.querySelector("main").getBoundingClientRect().bottom+24)');
 const shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:1320,height:shotHeight,scale:1}});await fs.writeFile(path.join(out,'rotation-comparison.png'),Buffer.from(shot.data,'base64'));
 await call('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:false});
 check(await evaluate('document.documentElement.scrollWidth<=390'),'narrow page scrolls each image without overflowing the document');
 check(errors.length===0,'no browser runtime exceptions');
 await fs.writeFile(path.join(out,'review-checks.txt'),results.join('\n')+'\nCOMPLETE\n');console.log(results.join('\n'));
}catch(error){await fs.writeFile(path.join(out,'review-checks.txt'),results.join('\n')+'\nFAIL '+error.stack+'\n');throw error;}
finally{await call('Browser.close').catch(()=>{});ws.close();}
