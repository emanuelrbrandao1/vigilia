const {chromium}=require('playwright');
const path=require('node:path'),fs=require('node:fs'),os=require('node:os');
(async()=>{
 const extension=path.join(__dirname,'extensao');
 const chromeBrowser=process.env.FOCO_TEST_BROWSER==='chrome';
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'foco-browser-test-'));
 const context=await chromium.launchPersistentContext(profile,{executablePath:chromeBrowser?'C:/Program Files/Google/Chrome/Application/chrome.exe':'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,ignoreDefaultArgs:['--disable-extensions'],args:chromeBrowser?['--enable-unsafe-extension-debugging']:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
 try {
  if(chromeBrowser){const session=await context.browser().newBrowserCDPSession();console.log(await session.send('Extensions.loadUnpacked',{path:extension}));}
  let worker=context.serviceWorkers()[0];if(!worker)worker=await context.waitForEvent('serviceworker',{timeout:12000});
  console.log('Extension worker: '+worker.url());
  const result=await worker.evaluate(async()=>{
   for(let i=0;i<20;i++) {const rules=await chrome.declarativeNetRequest.getDynamicRules();if(rules.length)return {rules,manifest:chrome.runtime.getManifest().name};await new Promise(r=>setTimeout(r,250));}
   throw new Error('No rules installed');
  });console.log(JSON.stringify(result));
  const page=await context.newPage();
  const popup='chrome-extension://'+fs.readFileSync(path.join(__dirname,'extension-id.txt'),'utf8').trim()+'/popup.html';
  await page.goto(popup);await page.waitForTimeout(1800);console.log(await page.locator('#connection').textContent());
  await page.screenshot({path:path.join(__dirname,'preview-extensao.png')});
 }finally{await context.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
