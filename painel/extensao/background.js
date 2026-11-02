import { defaults, hostname, matchingRule, networkRules, failClosed } from './rules.mjs';

const browser = /Edg\//.test(navigator.userAgent) ? 'Edge' : 'Chrome';
const RECONNECT_DELAY_MS = 5000;
const RESPONSE_TIMEOUT_MS = 5000;
const STATUS_FRESHNESS_MS = 6000;
const REDIRECT_DEBOUNCE_MS = 2000;
const IDLE_SECONDS = 60;
let port;
let snapshot = { rules: defaults };
let connected = false;
let ready = false;
let signature = '';
let pending = false;
let lastSent = 0;
let lastReceived = 0;
let applying = Promise.resolve();
let reconnectAt = 0;
let initialized;
const redirects = new Map();

function blockedPageUrl(tab, rule, url) {
  if (tab.incognito) return 'about:blank';
  return chrome.runtime.getURL('blocked.html')
    + '?site=' + encodeURIComponent(hostname(url))
    + '#' + encodeURIComponent(rule.id);
}

async function showBlocked(tab, rules, url = tab.pendingUrl || tab.url) {
  const rule = matchingRule(url, rules);
  if (!rule?.blocked || tab.id == null) return;
  const target = blockedPageUrl(tab, rule, url);
  const previous = redirects.get(tab.id);
  if (previous?.target === target && Date.now() - previous.at < REDIRECT_DEBOUNCE_MS) return;
  redirects.set(tab.id, { target, at: Date.now() });
  try {
    await chrome.tabs.update(tab.id, { url: target });
  } catch {
    redirects.delete(tab.id);
  }
}

async function blockedNavigation(details) {
  if (details.frameId !== 0 || details.tabId < 0) return;
  await initialize();
  if (!matchingRule(details.url, snapshot.rules)?.blocked) return;
  try {
    const tab = await chrome.tabs.get(details.tabId);
    const current = hostname(tab.pendingUrl || tab.url);
    if (current && current !== hostname(details.url)) return;
    await showBlocked(tab, snapshot.rules, details.url);
  } catch {
    // Closed tab. The network block remains active.
  }
}

function initialize() {
  if (!initialized) {
    initialized = initializeRules().catch(error => {
      initialized = null;
      throw error;
    });
  }
  return initialized;
}

async function initializeRules() {
  const cached = await chrome.storage.local.get('rules');
  snapshot.rules = failClosed(cached.rules || defaults);
  await enforce(snapshot.rules);
  chrome.idle.setDetectionInterval(IDLE_SECONDS);
  await chrome.alarms.create('reconnect', { periodInMinutes: 0.5 });
}

async function updateNetworkRules(rules) {
  const next = networkRules(rules);
  const key = JSON.stringify(next);
  if (key === signature) return;
  ready = false;
  const old = await chrome.declarativeNetRequest.getDynamicRules();
  await chrome.declarativeNetRequest.updateDynamicRules({
    removeRuleIds: old.map(rule => rule.id),
    addRules: next
  });
  signature = key;
}

function enforce(rules) {
  // Serialize replacements so an older update cannot overwrite newer rules.
  applying = applying.catch(() => {}).then(async () => {
    await updateNetworkRules(rules);
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      await showBlocked(tab, rules);
    }
    ready = true;
  });
  return applying;
}

async function disconnected(error) {
  connected = false;
  ready = false;
  pending = false;
  snapshot = { ...snapshot, error, rules: failClosed(snapshot.rules) };
  await chrome.action.setBadgeText({ text: '!' });
  await chrome.action.setBadgeBackgroundColor({ color: '#a34637' });
  await enforce(snapshot.rules);
  ready = false;
}

async function receiveSnapshot(current, message) {
  if (port !== current) return;
  lastReceived = Date.now();
  try {
    if (!message.ok || !Array.isArray(message.rules)) {
      throw new Error(message.error || 'Resposta inválida do painel.');
    }
    snapshot = message;
    await enforce(message.rules);
    if (port !== current) return;
    connected = true;
    // Cache configuration, never tab URLs or navigation history.
    const cached = await chrome.storage.local.get('rules');
    if (port !== current) return;
    const rules = message.rules.map(({ usedSeconds, windowUsedSeconds, ...rule }) => rule);
    if (JSON.stringify(cached.rules) !== JSON.stringify(rules)) {
      await chrome.storage.local.set({ rules });
    }
    await chrome.action.setBadgeText({ text: '' });
  } catch (error) {
    await disconnected(error.message);
  } finally {
    pending = false;
  }
}

function connect() {
  if (port || Date.now() < reconnectAt) return;
  reconnectAt = Date.now() + RECONNECT_DELAY_MS;
  const current = chrome.runtime.connectNative('br.local.foco');
  port = current;
  current.onMessage.addListener(message => receiveSnapshot(current, message));
  current.onDisconnect.addListener(() => {
    const error = chrome.runtime.lastError?.message || 'Conexão local encerrada.';
    if (port === current) {
      port = null;
      void disconnected(error);
    }
  });
}

async function activeAllowedDomain() {
  const [win, idle] = await Promise.all([
    chrome.windows.getLastFocused(),
    chrome.idle.queryState(IDLE_SECONDS)
  ]);
  if (!win.focused || idle !== 'active') return '';
  const [tab] = await chrome.tabs.query({ active: true, windowId: win.id });
  const rule = matchingRule(tab?.url, snapshot.rules);
  return rule && !rule.blocked ? hostname(tab.url) : '';
}

async function tick() {
  await initialize();
  if (pending && Date.now() - lastSent > RESPONSE_TIMEOUT_MS) {
    port?.disconnect();
    port = null;
    await disconnected('O painel local não respondeu.');
  }
  connect();
  if (!port || pending) return;
  pending = true;
  lastSent = Date.now();
  try {
    const domain = await activeAllowedDomain();
    port.postMessage({ type: 'sample', browser, active: !!domain, domain, ready });
  } catch (error) {
    pending = false;
    await disconnected(error.message);
  }
}

const runTick = () => void tick().catch(console.error);
chrome.tabs.onActivated.addListener(runTick);
chrome.tabs.onUpdated.addListener((_id, change) => {
  if (change.url || change.status === 'complete') runTick();
});
chrome.tabs.onRemoved.addListener(id => {
  redirects.delete(id);
  runTick();
});
// Navigation errors can replace the tab URL with Chrome's internal error
// document before tabs.query sees it. The event retains the blocked URL.
const navigationFilter = { url: [{ schemes: ['http', 'https'] }] };
chrome.webNavigation.onBeforeNavigate.addListener(
  details => void blockedNavigation(details).catch(console.error), navigationFilter
);
chrome.webNavigation.onErrorOccurred.addListener(details => {
  if (details.error === 'net::ERR_BLOCKED_BY_CLIENT') void blockedNavigation(details).catch(console.error);
}, navigationFilter);
chrome.windows.onFocusChanged.addListener(runTick);
chrome.idle.onStateChanged.addListener(runTick);
chrome.alarms.onAlarm.addListener(runTick);
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg.type === 'status') {
    sendResponse({
      ...snapshot,
      connected: connected && Date.now() - lastReceived < STATUS_FRESHNESS_MS,
      browser
    });
  }
});
setInterval(runTick, 1000);
runTick();
