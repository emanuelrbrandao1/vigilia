const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'foco-pending-test-'));
fs.copyFileSync(path.join(__dirname,'FocoHost.next.exe'),path.join(root,'FocoHost.exe'));
async function run(data){
 fs.writeFileSync(path.join(root,'dados.json'),JSON.stringify(data));
 const child=spawn(path.join(root,'FocoHost.exe'),[],{windowsHide:true});
 const exited=new Promise((resolve,reject)=>{child.on('exit',resolve);child.on('error',reject);});
 const response=new Promise((resolve,reject)=>{let buffer=Buffer.alloc(0);const timer=setTimeout(()=>reject(new Error('Response timeout')),5000);child.on('error',reject);child.stdout.on('data',bytes=>{buffer=Buffer.concat([buffer,bytes]);if(buffer.length>=4&&buffer.length>=4+buffer.readUInt32LE(0)){clearTimeout(timer);resolve(JSON.parse(buffer.subarray(4,4+buffer.readUInt32LE(0))));}});});
 const bytes=Buffer.from(JSON.stringify({type:'sample',browser:'Chrome',active:false,domain:'',ready:true}));const size=Buffer.alloc(4);size.writeUInt32LE(bytes.length);child.stdin.write(Buffer.concat([size,bytes]));
 try{const result=await response;assert.equal(result.ok,true,result.error);return result;}finally{child.stdin.end();await exited;}
}
(async()=>{
 const now=Date.now()/1000;
 const rule={id:'test',name:'Pending test',domains:['example.com'],always:true,minutes:30,start:0,end:0,days:127};
 const pending={ruleId:'test',kind:'archive',requestedAt:now-86401,executeAt:now-1};
 const expired={version:4,rules:[rule],pendingChanges:[pending]};
 const result=await run(expired);assert.equal(result.rules.length,0);assert.equal(result.archivedRules.length,1);
 const saved=JSON.parse(fs.readFileSync(path.join(root,'dados.json'),'utf8'));assert.equal(saved.version,7);assert.equal(saved.pendingChanges.length,0);assert.equal(saved.rules[0].archived,true);
 const restored=await run(saved);assert.equal(restored.rules.length,0);
 const future={version:4,rules:[rule],pendingChanges:[{...pending,executeAt:now+86400}]};
 const waiting=await run(future);assert.equal(waiting.rules.length,1);assert.equal(waiting.rules[0].blocked,true);assert.ok(waiting.rules[0].changeStatus.includes('Arquivar'));
 const kept=JSON.parse(fs.readFileSync(path.join(root,'dados.json'),'utf8'));assert.equal(kept.pendingChanges.length,1);
 console.log('PASS: overdue pending change applied before native snapshot, persisted v7, restart stable, future pending stays blocked');
})().catch(error=>{console.error(error);process.exitCode=1;});
