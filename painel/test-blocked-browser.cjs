// Optional integration check: requires an installed native connector and a
// configured blocked URL. Runs only in a fresh, temporary Chrome profile.
const {chromium}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
(async()=>{
 const target=process.env.FOCO_TEST_BLOCKED_URL;
 if(!target||!/^https?:\/\//.test(target))throw Error('Set FOCO_TEST_BLOCKED_URL to a configured blocked site.');
 const extension=path.join(__dirname,'extensao');
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'vigilia-blocked-browser-'));
 const executablePath=process.env.FOCO_CHROME_PATH||path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe');
 const context=await chromium.launchPersistentContext(profile,{executablePath,headless:true,ignoreDefaultArgs:['--disable-extensions'],args:['--enable-unsafe-extension-debugging'],viewport:{width:1280,height:900}});
 try{
  const session=await context.browser().newBrowserCDPSession();
  const loaded=await session.send('Extensions.loadUnpacked',{path:extension});
  const id=loaded.id||fs.readFileSync(path.join(__dirname,'extension-id.txt'),'utf8').trim();
  const statusPage=await context.newPage();await statusPage.goto('chrome-extension://'+id+'/popup.html');
  let blocked=false;
  for(let attempt=0;attempt<60;attempt++){
   blocked=await statusPage.evaluate(async target=>{
   const state=await chrome.runtime.sendMessage({type:'status'});
   const host=new URL(target).hostname;
   return state.connected&&state.rules.some(r=>r.blocked&&r.domains.some(d=>host===d||host.endsWith('.'+d)));
   },target);
   if(blocked)break;
   await statusPage.waitForTimeout(250);
  }
  assert.equal(blocked,true,'Configured target must be blocked by the connected native host');
  const page=await context.newPage();try{await page.goto(target,{timeout:10000});}catch{}
  for(let attempt=0;attempt<60;attempt++){
   if(page.url().startsWith('chrome-extension://'+id+'/blocked.html')){
    try{await page.locator('#heading').waitFor({state:'visible',timeout:250});break;}catch{}
   }
   await page.waitForTimeout(100);
  }
  assert.ok(page.url().startsWith('chrome-extension://'+id+'/blocked.html'),page.url());
  await page.waitForFunction(()=>!document.querySelector('#reason').textContent.includes('Conferindo'));
  assert.equal(await page.locator('#open').isVisible(),false);
  assert.ok(await page.locator('#heading').textContent());
  console.log('PASS: blocked navigation opens Vigilia instead of Chrome error; reopen remains hidden.');
  console.log(await page.locator('#usage').textContent());
  await page.screenshot({path:path.join(__dirname,'preview-blocked-browser.png')});
 }finally{await context.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
