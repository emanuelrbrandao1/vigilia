// A fresh installation must not inherit the developer's personal rules.
// Existing installations still fail closed using their cached configuration.
export const defaults = [];
export function hostname(url) {
  try { const u = new URL(url); return ['http:', 'https:'].includes(u.protocol) ? u.hostname.replace(/\.$/, '').toLowerCase() : ''; }
  catch { return ''; }
}
export function matches(host, domain) { return host === domain || host.endsWith('.' + domain); }
export function matchingRule(url, rules) {
  const host = hostname(url);
  const matching = rules.filter(r => r.domains.some(d => matches(host, d)));
  return matching.find(r => r.blocked) || matching[0];
}
export function networkRules(rules) {
  let id = 1;
  return [...new Set(rules.filter(r => r.blocked).flatMap(r => r.domains))].sort().map(domain => ({
    id: id++, priority: 1, action: { type: 'block' },
    condition: { urlFilter: '||' + domain + '^', resourceTypes: ['main_frame', 'sub_frame'] }
  }));
}
export const OFFLINE_REASON = 'Painel local desconectado';
export function failClosed(rules) { return rules.map(r => ({ ...r, blocked: true, reason: OFFLINE_REASON })); }
// Time limits change during the day, so they come first; permanent blocks go last.
export function displayOrder(rules) {
  return rules.map((rule, index) => ({ rule, index }))
    .sort((a, b) => Number(!!a.rule.always) - Number(!!b.rule.always) || a.index - b.index)
    .map(item => item.rule);
}
export function studySession(rules, now = Date.now()) {
  const studying = rules.filter(rule => rule.focusUntil * 1000 > now);
  if (!studying.length) return null;
  return { until: Math.max(...studying.map(rule => rule.focusUntil)) * 1000, rules: studying };
}
const pad = value => String(value).padStart(2, '0');
export function clockTime(ms) { const date = new Date(ms); return pad(date.getHours()) + ':' + pad(date.getMinutes()); }
export function remaining(ms) {
  const minutes = Math.max(1, Math.ceil(ms / 60000));
  return minutes >= 60 ? Math.floor(minutes / 60) + 'h ' + pad(minutes % 60) + 'min' : minutes + ' min';
}
