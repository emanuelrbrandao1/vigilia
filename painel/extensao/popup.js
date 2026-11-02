function appendUsage(card, rule) {
  if (!(rule.minutes > 0) || rule.always) return;
  const used = rule.windowQuota ? rule.windowUsedSeconds : rule.usedSeconds;
  const progress = document.createElement('progress');
  const usage = document.createElement('p');
  progress.max = rule.minutes * 60;
  progress.value = Math.min(progress.max, Math.max(0, used || 0));
  progress.setAttribute('aria-label', 'Consumo de ' + rule.name);
  usage.textContent = Math.floor((used || 0) / 60) + ' de ' + rule.minutes
    + ' min' + (rule.windowQuota ? ' na faixa' : ' hoje');
  card.append(progress, usage);
}

function ruleCard(rule) {
  const card = document.createElement('article');
  const header = document.createElement('header');
  const name = document.createElement('strong');
  const badge = document.createElement('span');
  const detail = document.createElement('p');
  name.textContent = rule.name;
  badge.className = 'badge';
  badge.textContent = rule.blocked ? 'Bloqueado' : 'Liberado';
  header.append(name, badge);
  detail.textContent = rule.reason;
  card.append(header, detail);
  appendUsage(card, rule);
  if (rule.changeStatus) {
    const pending = document.createElement('p');
    pending.textContent = rule.changeStatus;
    card.append(pending);
  }
  return card;
}

function renderStatus(state) {
  const connection = state.connected
    ? state.browser + ' conectado · cota compartilhada'
    : 'Sem conexão. Os sites configurados permanecem bloqueados.';
  const label = document.querySelector('#connection');
  if (label.textContent !== connection) label.textContent = connection;
  const list = document.querySelector('#rules');
  list.replaceChildren();
  for (const rule of state.rules || []) list.append(ruleCard(rule));
  if (!state.rules?.length) {
    const empty = document.createElement('p');
    empty.textContent = 'Nenhuma regra ativa. Adicione uma no painel.';
    list.append(empty);
  }
}

async function refresh() {
  try {
    const state = await chrome.runtime.sendMessage({ type: 'status' });
    renderStatus(state);
  } catch {
    document.querySelector('#connection').textContent = 'Não foi possível conectar. Abra a extensão novamente.';
  }
}

void refresh();
setInterval(refresh, 1500);
