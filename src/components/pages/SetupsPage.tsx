import { useState } from 'react';
import { ArrowUpFromLine, FolderOpen, Plus, Save, Trash2 } from 'lucide-react';
import { controller, controllerImage } from '../../controllers';
import type { Profile } from '../../types/gamepad';

export function SetupsPage({ profiles, draft, ready, onNew, onSave, onImport, onExport, onLoad, onDelete, onRename }: {
  profiles: Profile[]; draft: Profile; ready: boolean;
  onNew: () => void; onSave: () => void; onImport: () => void; onExport: () => void;
  onLoad: (profile: Profile) => void; onDelete: (profile: Profile) => void;
  onRename: (profile: Profile, name: string) => void;
}) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = profiles.find(profile => profile.id === selectedId) ?? profiles.find(profile => profile.id === draft.id) ?? profiles[0];
  return <section className="profile-library">
    <div className="library-heading"><div><h2>My setups</h2><p>{profiles.length} saved {profiles.length === 1 ? 'setup' : 'setups'} · stored on this computer</p></div>
      <button className="button secondary" onClick={onNew}><Plus size={16} />New setup</button></div>
    {selected ? <div className="setups-workbench">
      <div className="setup-selection-list" aria-label="Saved setups">
        {profiles.map(saved => <button key={saved.id} className="setup-selection-row" aria-pressed={selected.id === saved.id}
          data-gamepad-action onClick={() => setSelectedId(saved.id)}>
          <img src={controllerImage(controller('x20'))} alt="" />
          <span><strong>{saved.name}</strong><small>X20 · {saved.id === draft.id ? 'Open in editor' : 'Saved locally'}</small></span>
          <span aria-hidden="true">→</span>
        </button>)}
      </div>
      <SetupInspector key={selected.id + ':' + selected.name} profile={selected} onLoad={() => onLoad(selected)}
        onDelete={() => onDelete(selected)} onRename={name => onRename(selected, name)} />
    </div> : <div className="metal-panel library-empty"><FolderOpen size={38} strokeWidth={1} /><h3>Your setups belong here</h3>
      <p>Save a draft or import an existing setup.</p><div className="dialog-actions">
        <button className="button primary" disabled={!ready} onClick={onSave}><Save size={16} />Save current setup</button>
        <button className="button secondary" disabled={!ready} onClick={onImport}>Import JSON</button>
      </div></div>}
    <div className="metal-panel library-current"><span className="engraved">CURRENT DRAFT</span><strong>{draft.name}</strong>
      <span>Review changes before applying to hardware.</span><button className="button secondary" disabled={!ready} onClick={onExport}><ArrowUpFromLine size={16} />Export JSON</button></div>
  </section>;
}

function SetupInspector({ profile, onLoad, onDelete, onRename }: { profile: Profile; onLoad: () => void; onDelete: () => void; onRename: (name: string) => void }) {
  const [name, setName] = useState(profile.name);
  const mappings = Object.entries(profile.remaps).filter(([key, value]) => key !== value).length;
  const macros = Object.values(profile.macros).filter(steps => steps.length).length;
  return <aside className="metal-panel setup-inspector" aria-label="Selected setup">
    <span className="engraved">X20 · LOCAL SETUP</span><h3>{profile.name}</h3>
    <dl className="setup-detail-values"><div><dt>Mappings changed</dt><dd>{mappings}</dd></div>
      <div><dt>Macro sequences</dt><dd>{macros}</dd></div><div><dt>Draft vibration</dt><dd>{profile.vibration}%</dd></div>
      <div><dt>Created</dt><dd>{new Date(profile.createdAt).toLocaleDateString()}</dd></div></dl>
    <label>Setup name<input aria-label="Saved setup name" value={name} maxLength={100} onChange={event => setName(event.target.value)} /></label>
    <button className="button secondary" disabled={!name.trim() || name.trim() === profile.name} onClick={() => onRename(name.trim())}>Rename setup</button>
    <button className="button primary" onClick={onLoad}>Load setup into draft</button>
    <button className="button danger" aria-label={`Delete ${profile.name}`} onClick={onDelete}><Trash2 size={15} />Delete setup</button>
  </aside>;
}
