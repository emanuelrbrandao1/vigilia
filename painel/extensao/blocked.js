import { matchingRule, matches } from './rules.mjs';
let target;
const site = new URLSearchParams(location.search).get('site') || '';
const set = (id, text) => { const node=document.getElementById(id); if(node.textContent!==text) node.textContent=text; };
document.getElementById('back').onclick = () => history.length > 1 ? history.back() : window.close();
document.getElementById('open').onclick = () => { if(target) location.href='https://'+target; };
async function refresh() {
 try {
  const state=await chrome.runtime.sendMessage({type:'status'});
  const original=state.rules?.find(r=>r.id===decodeURIComponent(location.hash.slice(1)));
  const domain=site || original?.domains[0];
  const candidates=(state.rules||[]).filter(r=>domain&&r.domains.some(d=>matches(domain,d)));
  const rule=candidates.find(r=>r.blocked&&r.reason==='Limite diário atingido') || (domain&&matchingRule('https://'+domain,state.rules||[])) || original;
  const daily=rule?.blocked&&rule.reason?.startsWith('Limite diário atingido');
  set('heading',daily?'Seu limite de hoje chegou ao fim.':rule?.blocked?'Este acesso está em pausa.':'Seu acesso está disponível.');
  set('site',domain||'');
  set('reason',rule?(rule.blocked?rule.reason:'Todas as regras permitem o acesso agora.'):'Consulte suas regras no painel do Vigília.');
  const used=rule?.windowQuota?rule.windowUsedSeconds:rule?.usedSeconds;
  set('usage',rule?.minutes>0&&!rule.always?Math.floor((used||0)/60)+' de '+rule.minutes+' minutos utilizados'+(rule.windowQuota?' nesta faixa.':' hoje.'):'');
  set('resume',daily?'O limite diário renova à meia-noite do computador. As outras regras continuam valendo.':rule?.windowQuota?'A cota renova na próxima faixa. Outras regras também podem restringir o acesso.':rule?.always?'Este site tem um bloqueio permanente. Consulte as opções de pausa no painel.':'Confira os horários e limites no painel do Vigília.');
  set('status',state.connected?'Regras sincronizadas com o painel local.':'Sem conexão. O bloqueio permanece até reconectar.');
  target=state.connected&&domain&&!candidates.some(r=>r.blocked)?domain:null;
  document.getElementById('open').hidden=!target;
 } catch {set('status','Abra a extensão para conferir a conexão com o painel.');}
}
void refresh();setInterval(refresh,1000);
