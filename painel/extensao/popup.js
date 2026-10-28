async function refresh() {
 try {
  const state = await chrome.runtime.sendMessage({type:'status'});
  const connection=state.connected?state.browser+' conectado · cota compartilhada':'Sem conexão. Os sites configurados permanecem bloqueados.';
  const label=document.querySelector('#connection');if(label.textContent!==connection)label.textContent=connection;
  const list=document.querySelector('#rules');list.replaceChildren();
  for(const r of state.rules||[]){
   const card=document.createElement('article'),header=document.createElement('header'),name=document.createElement('strong'),badge=document.createElement('span'),detail=document.createElement('p');
   name.textContent=r.name;badge.className='badge';badge.textContent=r.blocked?'Bloqueado':'Liberado';header.append(name,badge);
   detail.textContent=r.reason;card.append(header,detail);
   if(r.minutes>0&&!r.always){
    const used=r.windowQuota?r.windowUsedSeconds:r.usedSeconds,progress=document.createElement('progress'),usage=document.createElement('p');
    progress.max=r.minutes*60;progress.value=Math.min(progress.max,Math.max(0,used||0));progress.setAttribute('aria-label','Consumo de '+r.name);
    usage.textContent=Math.floor((used||0)/60)+' de '+r.minutes+' min'+(r.windowQuota?' na faixa':' hoje');card.append(progress,usage);
   }
   if(r.changeStatus){const pending=document.createElement('p');pending.textContent=r.changeStatus;card.append(pending);}
   list.append(card);
  }
  if(!state.rules?.length){const empty=document.createElement('p');empty.textContent='Nenhuma regra ativa. Adicione uma no painel.';list.append(empty);}
 }catch{document.querySelector('#connection').textContent='Não foi possível conectar. Abra a extensão novamente.';}
}
void refresh();setInterval(refresh,1500);
