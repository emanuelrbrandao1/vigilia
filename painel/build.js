const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = __dirname;
const manifestPath = path.join(root, 'extensao', 'manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
if (!manifest.key) {
  const {publicKey} = crypto.generateKeyPairSync('rsa', {modulusLength: 2048});
  manifest.key = publicKey.export({type:'spki', format:'der'}).toString('base64');
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
}
const id = [...crypto.createHash('sha256').update(Buffer.from(manifest.key,'base64')).digest().subarray(0,16)]
  .flatMap(byte => [byte >> 4, byte & 15]).map(n => String.fromCharCode(97+n)).join('');
fs.writeFileSync(path.join(root,'native-host.json'), JSON.stringify({
  name:'br.local.foco', description:'Foco local: configurações e cotas compartilhadas',
  path:path.join(root,'FocoHost.exe'), type:'stdio', allowed_origins:[`chrome-extension://${id}/`]
},null,2)+'\n');
fs.writeFileSync(path.join(root,'extension-id.txt'),id+'\n');
console.log('Extension ID: '+id);
