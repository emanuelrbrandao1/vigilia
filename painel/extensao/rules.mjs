export const defaults = [
  { id: 'inicial-x', name: 'X / Twitter', domains: ['x.com', 'twitter.com'], blocked: true, reason: 'Conecte o painel local' },
  { id: 'inicial-tiktok', name: 'TikTok', domains: ['tiktok.com'], blocked: true, reason: 'Conecte o painel local' }
];
export function hostname(url) {
  try { const u = new URL(url); return ['http:', 'https:'].includes(u.protocol) ? u.hostname.replace(/\.$/, '').toLowerCase() : ''; }
  catch { return ''; }
}
export function matches(host, domain) { return host === domain || host.endsWith('.' + domain); }
export function matchingRule(url, rules) {
  const host = hostname(url);
  return rules.find(r => r.domains.some(d => matches(host, d)));
}
export function networkRules(rules) {
  let id = 1;
  return [...new Set(rules.filter(r => r.blocked).flatMap(r => r.domains))].sort().map(domain => ({
    id: id++, priority: 1, action: { type: 'block' },
    condition: { urlFilter: '||' + domain + '^', resourceTypes: ['main_frame', 'sub_frame'] }
  }));
}
export function failClosed(rules) { return rules.map(r => ({ ...r, blocked: true, reason: 'Painel local desconectado' })); }
