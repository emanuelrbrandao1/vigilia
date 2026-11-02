const {chromium}=require('playwright');
const path=require('node:path'),fs=require('node:fs'),os=require('node:os'),assert=require('node:assert/strict');
(async()=>{
 const extension=path.join(__dirname,'extensao');
 const chromeBrowser=process.env.FOCO_TEST_BROWSER==='chrome';
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'foco-browser-test-'));
 const context=await chromium.launchPersistentContext(profile,{executablePath:chromeBrowser?'C:/Program Files/Google/Chrome/Application/chrome.exe':'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,ignoreDefaultArgs:['--disable-extensions'],args:chromeBrowser?['--enable-unsafe-extension-debugging']:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
 try {
  if(chromeBrowser){const session=await context.browser().newBrowserCDPSession();console.log(await session.send('Extensions.loadUnpacked',{path:extension}));}
  let worker=context.serviceWorkers()[0];if(!worker)worker=await context.waitForEvent('serviceworker',{timeout:12000});
  console.log('Extension worker: '+worker.url());
  const page=await context.newPage();
  const popup='chrome-extension://'+fs.readFileSync(path.join(__dirname,'extension-id.txt'),'utf8').trim()+'/popup.html';
  await page.goto(popup);
  let status;
  for(let attempt=0;attempt<60;attempt++){
   status=await page.evaluate(async()=>{
   const snapshot=await chrome.runtime.sendMessage({type:'status'});
   if(!snapshot.connected)return false;
   const rules=await chrome.declarativeNetRequest.getDynamicRules();
   const expected=[...new Set(snapshot.rules.filter(r=>r.blocked).flatMap(r=>r.domains))].length;
   if(rules.length!==expected)return false;
   return {connected:snapshot.connected,activeRules:snapshot.rules.length,networkRules:rules.length};
   });
   if(status)break;
   await page.waitForTimeout(250);
  }
  assert.equal(status?.connected,true,'Native connector must connect and apply matching network rules');
  console.log(JSON.stringify(status));
  await page.screenshot({path:path.join(__dirname,'preview-extensao.png')});
 }finally{await context.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
