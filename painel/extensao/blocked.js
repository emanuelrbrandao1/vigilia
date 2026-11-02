import { hostname, matchingRule, matches } from './rules.mjs';

let target;
const requestedSite = new URLSearchParams(location.search).get('site') || '';
const site = hostname('https://' + requestedSite) === requestedSite ? requestedSite : '';

function setText(id, text) {
  const node = document.getElementById(id);
  if (node.textContent !== text) node.textContent = text;
}

function selectRule(domain, rules, original) {
  const candidates = rules.filter(rule =>
    domain && rule.domains.some(configured => matches(domain, configured))
  );
  const dailyRule = candidates.find(rule =>
    rule.blocked && rule.reason === 'Limite diário atingido'
  );
  const rule = dailyRule || (domain && matchingRule('https://' + domain, rules)) || original;
  return { rule, candidates };
}

function headingText(connected, rule, daily) {
  if (!connected) return 'Aguardando conexão com o painel.';
  if (daily) return 'Seu limite de hoje chegou ao fim.';
  if (rule?.blocked) return 'Este acesso está em pausa.';
  return 'Seu acesso está disponível.';
}

function usageText(rule) {
  if (!(rule?.minutes > 0) || rule.always) return '';
  const used = rule.windowQuota ? rule.windowUsedSeconds : rule.usedSeconds;
  return Math.floor((used || 0) / 60) + ' de ' + rule.minutes
    + ' minutos utilizados' + (rule.windowQuota ? ' nesta faixa.' : ' hoje.');
}

function resumeText(rule, daily) {
  if (daily) return 'O limite diário renova à meia-noite do computador. As outras regras continuam valendo.';
  if (rule?.windowQuota) return 'A cota renova na próxima faixa. Outras regras também podem restringir o acesso.';
  if (rule?.always) return 'Este site tem um bloqueio permanente. Consulte as opções de pausa no painel.';
  return 'Confira os horários e limites no painel do Vigília.';
}

function renderStatus(state) {
  const original = state.rules?.find(rule => rule.id === decodeURIComponent(location.hash.slice(1)));
  const domain = site || original?.domains[0];
  const { rule, candidates } = selectRule(domain, state.rules || [], original);
  const daily = rule?.blocked && rule.reason?.startsWith('Limite diário atingido');

  setText('heading', headingText(state.connected, rule, daily));
  setText('site', domain || '');
  setText('reason', rule
    ? (rule.blocked ? rule.reason : 'Todas as regras permitem o acesso agora.')
    : 'Consulte suas regras no painel do Vigília.');
  setText('usage', usageText(rule));
  setText('resume', resumeText(rule, daily));
  setText('status', state.connected
    ? 'Regras sincronizadas com o painel local.'
    : 'Sem conexão. O bloqueio permanece até reconectar.');
  target = state.connected && domain && !candidates.some(candidate => candidate.blocked) ? domain : null;
  document.getElementById('open').hidden = !target;
}

function renderDisconnected() {
  target = null;
  document.getElementById('open').hidden = true;
  setText('heading', 'Aguardando conexão com o painel.');
  setText('status', 'Abra a extensão para conferir a conexão com o painel.');
}

async function refresh() {
  try {
    const state = await chrome.runtime.sendMessage({ type: 'status' });
    renderStatus(state);
  } catch {
    renderDisconnected();
  }
}

document.getElementById('back').onclick = () => history.length > 1 ? history.back() : window.close();
document.getElementById('open').onclick = () => {
  if (target) location.href = 'https://' + target;
};
void refresh();
setInterval(refresh, 1000);
