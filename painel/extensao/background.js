import { defaults, hostname, matchingRule, networkRules, failClosed, OFFLINE_REASON } from './rules.mjs';

const browser = /Edg\//.test(navigator.userAgent) ? 'Edge' : 'Chrome';
const RECONNECT_DELAY_MS = 5000;
// dados.json can be busy for a few seconds (OneDrive, antivirus); that is not an outage.
const RESPONSE_TIMEOUT_MS = 15000;
// A short outage keeps the last synchronized decisions instead of blocking every
// configured site at once and throwing open tabs out of their pages. Longer outages still fail closed.
const OFFLINE_GRACE_MS = 90000;
// One grace period per 10 minutes: repeated drops, or a connector closed on purpose,
// cannot keep a site usable while its time is not being counted.
const GRACE_COOLDOWN_MS = 600000;
const SYNC_PERSIST_MS = 15000;
const MAX_RETURN_URL = 2000;
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
let lastSynced = 0;
let persistedSync = 0;
let closed = true;
let graceStartedAt = 0;
let graceMono = -Infinity;
let applying = Promise.resolve();
let reconnectAt = 0;
let initialized;
const redirects = new Map();

const monotonic = () => (typeof performance === 'object' ? performance.now() : Date.now());

function recentlySynced() {
  const elapsed = Date.now() - lastSynced;
  // A clock moved backwards must not make old decisions look fresh.
  return lastSynced > 0 && elapsed >= -5000 && elapsed < OFFLINE_GRACE_MS;
}

async function startGrace() {
  const since = Date.now() - graceStartedAt;
  // A start time in the future (clock moved backwards) also denies a new grace period.
  if (!recentlySynced() || (graceStartedAt > 0 && !(since >= GRACE_COOLDOWN_MS))) return false;
  graceStartedAt = Date.now();
  graceMono = monotonic();
  await chrome.storage.local.set({ graceStartedAt });
  return true;
}

// Measured with a monotonic clock, so changing the system time cannot stretch it.
function withinGrace() {
  return monotonic() - graceMono < OFFLINE_GRACE_MS;
}

function blockedPageUrl(tab, rule, url) {
  if (tab.incognito) return 'about:blank';
  // The full address lets the page return to the same video or post. It stays in this tab only.
  const from = /^https?:/i.test(url || '') && url.length <= MAX_RETURN_URL ? '&from=' + encodeURIComponent(url) : '';
  const why = rule.reason === OFFLINE_REASON ? '&why=offline' : '';
  return chrome.runtime.getURL('blocked.html')
    + '?site=' + encodeURIComponent(hostname(url)) + from + why
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
  const cached = await chrome.storage.local.get(['rules', 'syncedAt', 'graceStartedAt']);
  lastSynced = Number(cached.syncedAt) || 0;
  persistedSync = lastSynced;
  graceStartedAt = Number(cached.graceStartedAt) || 0;
  const rules = cached.rules || defaults;
  // A restarted service worker resumes the last decisions only if they are recent
  // and the grace budget allows it; otherwise it fails closed as before.
  closed = !(await startGrace());
  snapshot.rules = closed ? failClosed(rules) : rules;
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

async function failClose(error) {
  closed = true;
  snapshot = { ...snapshot, error, rules: failClosed(snapshot.rules) };
  await chrome.action.setBadgeText({ text: '!' });
  await chrome.action.setBadgeBackgroundColor({ color: '#a34637' });
  await enforce(snapshot.rules);
  ready = false;
}

async function disconnected(error) {
  const wasConnected = connected;
  connected = false;
  ready = false;
  pending = false;
  snapshot = { ...snapshot, error };
  // Another notice about the same outage keeps the grace period already running.
  if (!closed && !wasConnected && withinGrace()) return;
  if (closed || !(await startGrace())) {
    await failClose(error);
    return;
  }
  // Keep the last synchronized rules while reconnecting.
  await chrome.action.setBadgeText({ text: '…' });
  await chrome.action.setBadgeBackgroundColor({ color: '#6b6e78' });
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
    closed = false;
    lastSynced = Date.now();
    // Cache configuration, never tab URLs or navigation history.
    const cached = await chrome.storage.local.get(['rules']);
    if (port !== current) return;
    const rules = message.rules.map(({ usedSeconds, windowUsedSeconds, ...rule }) => rule);
    const update = {};
    if (JSON.stringify(cached.rules) !== JSON.stringify(rules)) update.rules = rules;
    if (update.rules || lastSynced - persistedSync >= SYNC_PERSIST_MS) update.syncedAt = lastSynced;
    if (update.syncedAt) {
      await chrome.storage.local.set(update);
      persistedSync = update.syncedAt;
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
  if (!connected && !closed && !withinGrace()) await failClose(snapshot.error || OFFLINE_REASON);
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
      reconnecting: !connected && !closed,
      browser
    });
  }
});
setInterval(runTick, 1000);
runTick();
