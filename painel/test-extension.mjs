import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defaults, hostname, matchingRule, networkRules, failClosed } from './extensao/rules.mjs';
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
  let sent=[], updates=[], timer, nativeMessage, nativeDisconnect, failUpdate=failFirstUpdate;
  const port={postMessage: msg => {sent.push(msg);queueMicrotask(()=>nativeMessage(response));},disconnect:()=>nativeDisconnect(),onMessage:{addListener:fn=>nativeMessage=fn},onDisconnect:{addListener:fn=>nativeDisconnect=fn}};
  const storage={rules:[rule]};
  const chrome={
    storage:{local:{get:async key=>({[key]:storage[key]}),set:async data=>Object.assign(storage,data)}},
    tabs:{get:async id=>({id,url,incognito}),query:async ()=>[{id:1,url,active:true,incognito}],update:async(id,data)=>{updates.push(data.url);url=data.url;},onActivated:events('activate'),onUpdated:events('updated'),onRemoved:events('removed')},
    webNavigation:{onBeforeNavigate:events('beforeNavigate'),onErrorOccurred:events('navigationError')},
    windows:{getLastFocused:async()=>({focused,id:1}),onFocusChanged:events('focus')},
    idle:{setDetectionInterval:()=>{},queryState:async()=>idle,onStateChanged:events('idle')},
    alarms:{create:async()=>{},onAlarm:events('alarm')},
    declarativeNetRequest:{getDynamicRules:async()=>rules,updateDynamicRules:async data=>{if(failUpdate){failUpdate=false;throw new Error('Transient network-rule failure');}rules=data.addRules;}},
    action:{setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}},
    runtime:{connectNative:()=>port,getURL:p=>'chrome-extension://test/'+p,onMessage:events('message')}
  };
  const context=vm.createContext({chrome,navigator:{userAgent:'Edg/155'},URL,console:{error:()=>{}},Date,encodeURIComponent,setInterval:fn=>{timer=fn;},setTimeout});
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
  incognito=false;url='chrome-error://chromewebdata/';listeners.navigationError({tabId:1,frameId:0,url:'https://example.com',error:'net::ERR_BLOCKED_BY_CLIENT'});await settle();assert.ok(updates.at(-1).includes('blocked.html?site=example.com#sample'));
  const updateCount=updates.length;listeners.navigationError({tabId:1,frameId:2,url:'https://example.com',error:'net::ERR_BLOCKED_BY_CLIENT'});await settle();assert.equal(updates.length,updateCount);
  url='https://other.test';listeners.beforeNavigate({tabId:1,frameId:0,url:'https://example.com'});await settle();assert.equal(updates.length,updateCount);
  nativeDisconnect();await settle();assert.equal(rules.length,1);
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
