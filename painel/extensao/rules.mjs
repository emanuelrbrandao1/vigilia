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
export function failClosed(rules) { return rules.map(r => ({ ...r, blocked: true, reason: 'Painel local desconectado' })); }
