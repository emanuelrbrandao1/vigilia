let target;
document.querySelector('#back').onclick = () => history.length > 1 ? history.back() : window.close();
document.querySelector('#open').onclick = () => { if (target) location.href = 'https://' + target; };
async function refresh() {
  try {
    const state = await chrome.runtime.sendMessage({ type: 'status' });
    const rule = state.rules.find(r => r.id === decodeURIComponent(location.hash.slice(1)));
    document.querySelector('#reason').textContent = rule ? rule.name + ' • ' + rule.reason : 'A regra foi alterada. Abra o endereço novamente para verificar o acesso.';
    document.querySelector('#status').textContent = state.connected ? 'Chrome e Edge usam a mesma cota diária.' : 'Sem conexão com o painel local.';
    target = state.connected && rule && !rule.blocked ? rule.domains[0] : null;
    document.querySelector('#open').hidden = !target;
  } catch { document.querySelector('#status').textContent = 'Abra a extensão para verificar a conexão.'; }
}
void refresh(); setInterval(refresh, 1000);
