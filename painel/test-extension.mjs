import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { hostname, matchingRule, networkRules, failClosed } from './extensao/rules.mjs';
const root = path.dirname(fileURLToPath(import.meta.url));
const rule = {id:'sample',name:'Example',domains:['example.com'],blocked:false,minutes:1,usedSeconds:0};
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
test('foreground, focus changes, inactive computer, quota and native disconnect', async () => {
  const listeners={}, events = name => ({addListener: fn => {listeners[name]=fn;}});
  let focused=true, idle='active', incognito=false, url='https://example.com/page', rules=[], response={ok:true,rules:[rule]};
  let sent=[], updates=[], timer, nativeMessage, nativeDisconnect;
  const port={postMessage: msg => {sent.push(msg);queueMicrotask(()=>nativeMessage(response));},disconnect:()=>nativeDisconnect(),onMessage:{addListener:fn=>nativeMessage=fn},onDisconnect:{addListener:fn=>nativeDisconnect=fn}};
  const storage={rules:[rule]};
  const chrome={
    storage:{local:{get:async key=>({[key]:storage[key]}),set:async data=>Object.assign(storage,data)}},
    tabs:{query:async ()=>[{id:1,url,active:true,incognito}],update:async(id,data)=>{updates.push(data.url);url=data.url;},onActivated:events('activate'),onUpdated:events('updated'),onRemoved:events('removed')},
    windows:{getLastFocused:async()=>({focused,id:1}),onFocusChanged:events('focus')},
    idle:{setDetectionInterval:()=>{},queryState:async()=>idle,onStateChanged:events('idle')},
    alarms:{create:async()=>{},onAlarm:events('alarm')},
    declarativeNetRequest:{getDynamicRules:async()=>rules,updateDynamicRules:async data=>{rules=data.addRules;}},
    action:{setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}},
    runtime:{connectNative:()=>port,getURL:p=>'chrome-extension://test/'+p,onMessage:events('message')}
  };
  const context=vm.createContext({chrome,navigator:{userAgent:'Edg/155'},URL,console,Date,encodeURIComponent,setInterval:fn=>{timer=fn;},setTimeout});
  const module=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/background.js'),'utf8'),{context});
  await module.link(async()=>{const dep=new vm.SourceTextModule(fs.readFileSync(path.join(root,'extensao/rules.mjs'),'utf8'),{context});await dep.link(()=>{});return dep;});
  await module.evaluate();
  const settle=()=>new Promise(resolve=>setTimeout(resolve,25));
  await settle();url='https://example.com/page';timer();await settle();
  assert.equal(sent.at(-1).browser,'Edge');assert.equal(sent.at(-1).active,true);
  focused=false;listeners.focus();await settle();assert.equal(sent.at(-1).active,false);
  focused=true;idle='idle';listeners.idle();await settle();assert.equal(sent.at(-1).active,false);
  idle='active';url='https://other.test';listeners.activate();await settle();assert.equal(sent.at(-1).domain,'');
  url='https://example.com';response={ok:true,rules:[{...rule,blocked:true,reason:'Limite diário atingido'}]};timer();await settle();
  assert.equal(rules.length,1);assert.ok(updates.at(-1).includes('blocked.html#sample'));
  response={ok:true,rules:[rule]};timer();await settle();assert.equal(rules.length,0);
  response={ok:true,rules:[],archivedRules:[{...rule,archived:true}]};timer();await settle();assert.equal(rules.length,0);timer();await settle();assert.equal(sent.at(-1).active,false);
  response={ok:true,rules:[rule]};timer();await settle();
  incognito=true;url='https://example.com';timer();await settle();assert.equal(sent.at(-1).active,true);
  response={ok:true,rules:[{...rule,blocked:true}]};timer();await settle();assert.equal(updates.at(-1),'about:blank');
  nativeDisconnect();await settle();assert.equal(rules.length,1);
});
