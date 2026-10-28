import { defaults, hostname, matchingRule, networkRules, failClosed } from './rules.mjs';

const browser = /Edg\//.test(navigator.userAgent) ? 'Edge' : 'Chrome';
let port, snapshot = { rules: defaults }, connected = false, ready = false;
let signature = '', pending = false, lastSent = 0, lastReceived = 0, applying = Promise.resolve();
let reconnectAt = 0;
const initialized = (async () => {
  const cached = await chrome.storage.local.get('rules');
  snapshot.rules = failClosed(cached.rules || defaults);
  await enforce(snapshot.rules);
  chrome.idle.setDetectionInterval(60);
  await chrome.alarms.create('reconnect', { periodInMinutes: 0.5 });
})();

function enforce(rules) {
  // Serialize replacements so an older update cannot overwrite newer rules.
  applying = applying.catch(() => {}).then(async () => {
    const next = networkRules(rules), key = JSON.stringify(next);
    if (key !== signature) {
      ready = false;
      const old = await chrome.declarativeNetRequest.getDynamicRules();
      await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: old.map(r => r.id), addRules: next });
      signature = key;
    }
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      const rule = matchingRule(tab.pendingUrl || tab.url, rules);
      if (rule?.blocked && tab.id) {
        // Spanning keeps one counter across normal/private windows. Chromium does
        // not allow its extension pages as top-level private tabs in this mode.
        const blockedUrl = tab.incognito ? 'about:blank' : chrome.runtime.getURL('blocked.html') + '#' + encodeURIComponent(rule.id);
        try { await chrome.tabs.update(tab.id, { url: blockedUrl }); }
        catch { /* The tab may close between the query and redirect. */ }
      }
    }
    ready = true;
  });
  return applying;
}

async function disconnected(error) {
  connected = false; ready = false; pending = false;
  snapshot = { ...snapshot, error, rules: failClosed(snapshot.rules) };
  await chrome.action.setBadgeText({ text: '!' });
  await chrome.action.setBadgeBackgroundColor({ color: '#a34637' });
  await enforce(snapshot.rules);
  ready = false;
}

function connect() {
  if (port || Date.now() < reconnectAt) return;
  reconnectAt = Date.now() + 5000;
  const current = chrome.runtime.connectNative('br.local.foco');
  port = current;
  current.onMessage.addListener(async message => {
    if (port !== current) return;
    lastReceived = Date.now();
    try {
      if (!message.ok || !Array.isArray(message.rules)) throw new Error(message.error || 'Resposta inválida do painel.');
      snapshot = message;
      await enforce(message.rules);
      connected = true;
      // Cache configuration, never tab URLs or navigation history.
      const cached = await chrome.storage.local.get('rules');
      const rules = message.rules.map(({usedSeconds, windowUsedSeconds, ...r}) => r);
      if (JSON.stringify(cached.rules) !== JSON.stringify(rules)) await chrome.storage.local.set({ rules });
      await chrome.action.setBadgeText({ text: '' });
    } catch (e) { await disconnected(e.message); }
    finally { pending = false; }
  });
  current.onDisconnect.addListener(() => {
    const error = chrome.runtime.lastError?.message || 'Conexão local encerrada.';
    if (port === current) { port = null; void disconnected(error); }
  });
}

async function tick() {
  await initialized;
  if (pending && Date.now() - lastSent > 5000) { port?.disconnect(); port = null; await disconnected('O painel local não respondeu.'); }
  connect();
  if (!port || pending) return;
  pending = true; lastSent = Date.now();
  try {
    const [win, idle] = await Promise.all([chrome.windows.getLastFocused(), chrome.idle.queryState(60)]);
    let domain = '';
    if (win.focused && idle === 'active') {
      const [tab] = await chrome.tabs.query({ active: true, windowId: win.id });
      const rule = matchingRule(tab?.url, snapshot.rules);
      if (rule && !rule.blocked) domain = hostname(tab.url);
    }
    port.postMessage({ type: 'sample', browser, active: !!domain, domain, ready });
  } catch (e) { pending = false; await disconnected(e.message); }
}

chrome.tabs.onActivated.addListener(() => void tick());
chrome.tabs.onUpdated.addListener((_id, change) => { if (change.url || change.status === 'complete') void tick(); });
chrome.tabs.onRemoved.addListener(() => void tick());
chrome.windows.onFocusChanged.addListener(() => void tick());
chrome.idle.onStateChanged.addListener(() => void tick());
chrome.alarms.onAlarm.addListener(() => void tick());
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg.type === 'status') sendResponse({ ...snapshot, connected: connected && Date.now() - lastReceived < 6000, browser });
});
setInterval(() => void tick().catch(console.error), 1000);
void tick().catch(console.error);
