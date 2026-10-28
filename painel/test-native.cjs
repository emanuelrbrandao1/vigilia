const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const temp=fs.mkdtempSync(path.join(os.tmpdir(),'foco-native-test-'));
fs.copyFileSync(path.join(__dirname,process.env.FOCO_TEST_STAGED?'FocoHost.next.exe':'FocoHost.exe'),path.join(temp,'FocoHost.exe'));
const data={version:1,day:'',usage:{},seen:{},ready:{},chargedUntil:0,rules:[{id:'example',name:'Teste',domains:['example.com'],always:false,windowQuota:true,minutes:1,start:0,end:0,days:127}]};
fs.writeFileSync(path.join(temp,'dados.json'),JSON.stringify(data));
function host(browser){
 const child=spawn(path.join(temp,'FocoHost.exe'),[],{windowsHide:true});let buffer=Buffer.alloc(0),pending=[];
 child.stdout.on('data',chunk=>{buffer=Buffer.concat([buffer,chunk]);while(buffer.length>=4&&buffer.length>=4+buffer.readUInt32LE(0)){let size=buffer.readUInt32LE(0);const msg=JSON.parse(buffer.subarray(4,4+size));buffer=buffer.subarray(4+size);pending.shift()?.resolve(msg);}});
 child.on('error',error=>pending.splice(0).forEach(p=>p.reject(error)));
 child.on('exit',code=>pending.splice(0).forEach(p=>p.reject(new Error('Native host exited '+code))));
 return {child,send(active){return new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('Native response timed out')),5000);pending.push({resolve:m=>{clearTimeout(timer);resolve(m);},reject});const bytes=Buffer.from(JSON.stringify({type:'sample',browser,active,domain:active?'example.com':'',ready:true}));const length=Buffer.alloc(4);length.writeUInt32LE(bytes.length);child.stdin.write(Buffer.concat([length,bytes]));});}};
}
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 const chrome=host('Chrome'),edge=host('Edge');
 try{
  const initial=await chrome.send(true);assert.equal(initial.ok,true,initial.error);await sleep(150);
  const first=await chrome.send(false);assert.ok(first.rules[0].usedSeconds>0);
  await sleep(150);assert.equal((await chrome.send(false)).rules[0].usedSeconds,first.rules[0].usedSeconds);
  await edge.send(true);await sleep(150);const both=await edge.send(false);assert.ok(both.rules[0].usedSeconds>first.rules[0].usedSeconds);
  assert.equal((await chrome.send(false)).rules[0].usedSeconds,both.rules[0].usedSeconds);
  assert.ok(both.seen.Chrome && both.seen.Edge);
  assert.equal(both.rules[0].windowQuota,true);assert.ok(both.rules[0].windowUsedSeconds>0);
  assert.equal((await chrome.send(false)).rules[0].windowUsedSeconds,both.rules[0].windowUsedSeconds);
  const saved=JSON.parse(fs.readFileSync(path.join(temp,'dados.json'),'utf8'));assert.equal(saved.version,7);assert.ok(Object.keys(saved.windowUsage).length>0);
  console.log('PASS: native framing, pause, shared Chrome/Edge window quota, v1-to-v7 migration, persistence and connection status');
 }finally{chrome.child.stdin.end();edge.child.stdin.end();}
})().catch(e=>{console.error(e);process.exitCode=1;});
