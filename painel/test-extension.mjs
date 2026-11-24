import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defaults, hostname, matchingRule, networkRules, failClosed, displayOrder, studySession, remaining } from './extensao/rules.mjs';
const root = path.dirname(fileURLToPath(import.meta.url));
const rule = {id:'sample',name:'Example',domains:['example.com'],blocked:false,minutes:1,usedSeconds:0};
test('fresh installations have no personal default blocks', () => {
  assert.deepEqual(defaults, []);
  assert.deepEqual(networkRules(failClosed(defaults)), []);
});
test('exact domains, subdomains, paths and misleading suffixes', () => {
  assert.equal(matchingRule('https://a.example.com/path',[rule]),rule);
  assert.equal(matchingRule('https://example.com.evil.test',[rule]),undefined);
  assert.equal(matchingRule('https://notexample.com',[rule]),undefined);
  assert.equal(hostname('chrome://settings'),'');
});
test('network blocking, no duplicate rules, offline fail closed', () => {
  assert.deepEqual(networkRules([rule]),[]);
  const closed=failClosed([rule,rule]);
  assert.equal(networkRules(closed).length,1);
  assert.equal(networkRules(closed)[0].condition.urlFilter,'||example.com^');
});
test('overlapping rules always select a blocking rule regardless of order', () => {
  const work={...rule,id:'work'},daily={...rule,id:'daily',blocked:true};
  assert.equal(matchingRule('https://example.com',[work,daily]),daily);
  assert.equal(matchingRule('https://example.com',[daily,work]),daily);
  assert.equal(matchingRule('https://sub.example.com',[work,daily]),daily);
  assert.equal(networkRules([work,daily]).length,1);
  assert.equal(matchingRule('https://example.com',[work,{...daily,blocked:false}]),work);
});
for (const failFirstUpdate of [false, true]) test('foreground, quotas and disconnect; initial transient failure=' + failFirstUpdate, async () => {
  const listeners={}, events = name => ({addListener: fn => {listeners[name]=fn;}});
  let focused=true, idle='active', incognito=false, url='https://example.com/page', rules=[], response={ok:true,rules:[rule]};
  let sent=[], updates=[], timer, nativeMessage, nativeDisconnect, failUpdate=failFirstUpdate, offline=false, clock=Date.now();
  const FakeDate=class extends Date{constructor(...args){super(...(args.length?args:[clock]));}static now(){return clock;}};
  const port={postMessage: msg => {sent.push(msg);if(!offline)queueMicrotask(()=>nativeMessage(response));},disconnect:()=>nativeDisconnect(),onMessage:{addListener:fn=>nativeMessage=fn},onDisconnect:{addListener:fn=>nativeDisconnect=fn}};
  const storage={rules:[rule]};
  const chrome={
    storage:{local:{get:async keys=>Object.fromEntries([].concat(keys).map(key=>[key,storage[key]])),set:async data=>Object.assign(storage,data)}},
    tabs:{get:async id=>({id,url,incognito}),query:async ()=>[{id:1,url,active:true,incognito}],update:async(id,data)=>{updates.push(data.url);url=data.url;},onActivated:events('activate'),onUpdated:events('updated'),onRemoved:events('removed')},
    webNavigation:{onBeforeNavigate:events('beforeNavigate'),onErrorOccurred:events('navigationError')},
    windows:{getLastFocused:async()=>({focused,id:1}),onFocusChanged:events('focus')},
    idle:{setDetectionInterval:()=>{},queryState:async()=>idle,onStateChanged:events('idle')},
    alarms:{create:async()=>{},onAlarm:events('alarm')},
    declarativeNetRequest:{getDynamicRules:async()=>rules,updateDynamicRules:async data=>{if(failUpdate){failUpdate=false;throw new Error('Transient network-rule failure');}rules=data.addRules;}},
    action:{setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}},
    runtime:{connectNative:()=>port,getURL:p=>'chrome-extension://test/'+p,onMessage:events('message')}
  };
  const context=vm.createContext({chrome,navigator:{userAgent:'Edg/155'},URL,console:{error:()=>{}},Date:FakeDate,encodeURIComponent,setInterval:fn=>{timer=fn;},setTimeout});
  const module=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/background.js'),'utf8'),{context});
  await module.link(async()=>{const dep=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/rules.mjs'),'utf8'),{context});await dep.link(()=>{});return dep;});
  await module.evaluate();
  const settle=()=>new Promise(resolve=>setTimeout(resolve,25));
  await settle();timer();await settle();url='https://example.com/page';timer();await settle();
  assert.equal(sent.at(-1).browser,'Edge');assert.equal(sent.at(-1).active,true);
  focused=false;listeners.focus();await settle();assert.equal(sent.at(-1).active,false);
  focused=true;idle='idle';listeners.idle();await settle();assert.equal(sent.at(-1).active,false);
  idle='active';url='https://other.test';listeners.activate();await settle();assert.equal(sent.at(-1).domain,'');
  url='https://example.com';response={ok:true,rules:[{...rule,blocked:true,reason:'Limite diário atingido'}]};timer();await settle();
  assert.equal(rules.length,1);assert.ok(updates.at(-1).includes('#sample'));
  url='https://example.com';response={ok:true,rules:[rule,{...rule,id:'daily',blocked:true}]};timer();await settle();
  assert.ok(updates.at(-1).includes('#daily'));assert.equal(sent.at(-1).active,false);
  response={ok:true,rules:[rule]};timer();await settle();assert.equal(rules.length,0);
  response={ok:true,rules:[],archivedRules:[{...rule,archived:true}]};timer();await settle();assert.equal(rules.length,0);timer();await settle();assert.equal(sent.at(-1).active,false);
  response={ok:true,rules:[rule]};timer();await settle();
  incognito=true;url='https://example.com';timer();await settle();assert.equal(sent.at(-1).active,true);
  response={ok:true,rules:[{...rule,blocked:true}]};timer();await settle();assert.equal(updates.at(-1),'about:blank');
  incognito=false;url='chrome-error://chromewebdata/';listeners.navigationError({tabId:1,frameId:0,url:'https://example.com',error:'net::ERR_BLOCKED_BY_CLIENT'});await settle();assert.ok(updates.at(-1).includes('blocked.html?site=example.com&from='+encodeURIComponent('https://example.com')));assert.ok(updates.at(-1).endsWith('#sample'));
  const updateCount=updates.length;listeners.navigationError({tabId:1,frameId:2,url:'https://example.com',error:'net::ERR_BLOCKED_BY_CLIENT'});await settle();assert.equal(updates.length,updateCount);
  url='https://other.test';listeners.beforeNavigate({tabId:1,frameId:0,url:'https://example.com'});await settle();assert.equal(updates.length,updateCount);
  response={ok:true,rules:[rule]};timer();await settle();assert.equal(rules.length,0);
  // A short outage keeps the last decisions: the allowed site stays allowed while reconnecting.
  offline=true;nativeDisconnect();await settle();assert.equal(rules.length,0);
  clock+=30000;timer();await settle();assert.equal(rules.length,0);
  // Without the panel for longer than the grace period, every configured site is blocked again.
  clock+=61000;timer();await settle();assert.equal(rules.length,1);
  offline=false;clock+=16000;timer();await settle();timer();await settle();assert.equal(rules.length,0);
  assert.ok(storage.graceStartedAt>0);
  // A second drop within 10 minutes gets no new grace period: it blocks at once.
  offline=true;nativeDisconnect();await settle();assert.equal(rules.length,1);
});

async function startWorker(storage, clock) {
  let rules=[];
  const events={addListener(){}};
  const FakeDate=class extends Date{constructor(...args){super(...(args.length?args:[clock]));}static now(){return clock;}};
  const port={postMessage(){},disconnect(){},onMessage:events,onDisconnect:events};
  const chrome={
    storage:{local:{get:async keys=>Object.fromEntries([].concat(keys).map(key=>[key,storage[key]])),set:async data=>Object.assign(storage,data)}},
    tabs:{get:async id=>({id,url:'https://example.com'}),query:async()=>[],update:async()=>{},onActivated:events,onUpdated:events,onRemoved:events},
    webNavigation:{onBeforeNavigate:events,onErrorOccurred:events},
    windows:{getLastFocused:async()=>({focused:false,id:1}),onFocusChanged:events},
    idle:{setDetectionInterval(){},queryState:async()=>'active',onStateChanged:events},
    alarms:{create:async()=>{},onAlarm:events},
    declarativeNetRequest:{getDynamicRules:async()=>rules,updateDynamicRules:async data=>{rules=data.addRules;}},
    action:{setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}},
    runtime:{connectNative:()=>port,getURL:p=>'chrome-extension://test/'+p,onMessage:events}
  };
  const context=vm.createContext({chrome,navigator:{userAgent:'Chrome/155'},URL,console:{error:()=>{}},Date:FakeDate,encodeURIComponent,setInterval(){},setTimeout});
  const module=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/background.js'),'utf8'),{context});
  await module.link(async()=>{const dep=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/rules.mjs'),'utf8'),{context});await dep.link(()=>{});return dep;});
  await module.evaluate();await new Promise(resolve=>setTimeout(resolve,25));
  return rules.length;
}

test('restarted worker uses the grace period only when recent and not already spent', async () => {
  const clock=Date.UTC(2026,10,24,15,0,0);
  assert.equal(await startWorker({rules:[rule],syncedAt:clock-10000},clock),0);
  assert.equal(await startWorker({rules:[rule],syncedAt:clock-10000,graceStartedAt:clock-60000},clock),1);
  assert.equal(await startWorker({rules:[rule],syncedAt:clock-10000,graceStartedAt:clock-700000},clock),0);
  assert.equal(await startWorker({rules:[rule],syncedAt:clock-200000},clock),1);
  assert.equal(await startWorker({rules:[rule],syncedAt:clock-10000,graceStartedAt:clock+3600000},clock),1);
});

test('popup lists time limits before permanent blocks and shows the study session', () => {
  const permanent={id:'p',name:'TikTok',domains:['tiktok.com'],always:true,blocked:true};
  const quota={id:'q',name:'Instagram',domains:['instagram.com'],always:false,blocked:false};
  const daily={id:'d',name:'YouTube',domains:['youtube.com'],always:false,blocked:false};
  assert.deepEqual(displayOrder([permanent,quota,{...permanent,id:'x'},daily]).map(r=>r.id),['q','d','p','x']);
  const now=Date.UTC(2026,10,24,13,0,0);
  assert.equal(studySession([quota,daily],now),null);
  const session=studySession([{...quota,focusUntil:now/1000+3600},{...daily,focusUntil:now/1000+5400},permanent],now);
  assert.equal(session.until,now+5400000);assert.deepEqual(session.rules.map(r=>r.id),['q','d']);
  assert.equal(remaining(5400000),'1h 30min');assert.equal(remaining(45000),'1 min');
});

test('blocked page revokes the reopen button after communication fails', async () => {
  const nodes=new Map();
  const document={getElementById:id=>{if(!nodes.has(id))nodes.set(id,{textContent:'',hidden:true});return nodes.get(id);}};
  let state={connected:true,rules:[rule]}, fail=false, refresh;
  const context=vm.createContext({document,URLSearchParams,URL,decodeURIComponent,
    location:{search:'?site=example.com',hash:'#sample'},history:{length:0},window:{close(){}},
    chrome:{runtime:{sendMessage:async()=>{if(fail)throw new Error('Worker unavailable');return state;}}},
    setInterval:fn=>{refresh=fn;}});
  const module=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/blocked.js'),'utf8'),{context});
  await module.link(async()=>{const dep=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/rules.mjs'),'utf8'),{context});await dep.link(()=>{});return dep;});
  await module.evaluate();await refresh();
  assert.equal(nodes.get('open').hidden,false);
  fail=true;await refresh();
  assert.equal(nodes.get('open').hidden,true);
  assert.ok(nodes.get('heading').textContent.includes('conexão'));
  fail=false;state={connected:true,rules:[rule,{...rule,id:'daily',minutes:40,usedSeconds:2400,blocked:true,reason:'Limite diário atingido'}]};await refresh();
  assert.equal(nodes.get('open').hidden,true);
  assert.ok(nodes.get('usage').textContent.startsWith('40 de 40'));
});

test('blocked page returns to the exact page after a connection-only block', async () => {
  const nodes=new Map();
  const document={getElementById:id=>{if(!nodes.has(id))nodes.set(id,{textContent:'',hidden:true});return nodes.get(id);}};
  const page='https://example.com/watch?v=abc&t=42';
  for (const [from,expected] of [[page,page],['https://evil.test/x','https://example.com'],['https://sub.example.com/x','https://example.com'],['javascript:alert(1)','https://example.com']]) {
    let replaced=null, refresh;
    const location={search:'?site=example.com&from='+encodeURIComponent(from)+'&why=offline',hash:'#sample',replace:url=>{replaced=url;}};
    const context=vm.createContext({document,URLSearchParams,URL,decodeURIComponent,Date,location,history:{length:0},window:{close(){}},
      chrome:{runtime:{sendMessage:async()=>({connected:true,rules:[rule]})}},setInterval:fn=>{refresh=fn;}});
    const module=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/blocked.js'),'utf8'),{context});
    await module.link(async()=>{const dep=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/rules.mjs'),'utf8'),{context});await dep.link(()=>{});return dep;});
    await module.evaluate();await refresh();
    assert.equal(replaced,expected);
  }
});
