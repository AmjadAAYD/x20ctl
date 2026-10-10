"""Development-only oracle and evidence register; never shipped in native runtime."""
from pathlib import Path
import hashlib, json, random, sys, zipfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from x20ctl import protocol as p

def digest(data):return hashlib.sha256(data).hexdigest()
def main():
    out=ROOT/'artifacts/product-engine-phase';out.mkdir(parents=True,exist_ok=True)
    source=Path(r'C:\Users\amjad\Desktop\input-20261008-161452-2dd4446a.zip')
    vectors=[];counts={}
    def emit(kind,*fields):vectors.append('\t'.join(map(str,(kind,*fields))));counts[kind]=counts.get(kind,0)+1
    def hx(value):return value.hex() or '-'
    rng=random.Random(20261009)
    for index in range(1000):
        payload=rng.randbytes(index%16);op=rng.randrange(256);serial=rng.randrange(256);nonce=rng.randrange(256);length=rng.randrange(8)*32+len(payload)+5
        raw=p.build(op,payload,serial=serial,nonce=nonce,length_field=length)
        emit('frame',op,length,serial,nonce,hx(payload),raw.hex())
        if index%10==0:
            parsed=p.parse(raw);emit('decode',raw.hex(),f'{parsed.opcode},{parsed.length},{parsed.serial},{parsed.nonce},{parsed.payload.hex()},{int(parsed.crc_valid)}')
    for slot in range(16):
        for counter in range(1,17):emit('serial',slot,counter,p.save_button_serial(slot,counter))
    for size in range(16):
        for counter in range(8):emit('length',size,counter,p.host_length(size,counter))
    for count in (0,1,2,10,20,47):
        for trigger in (0,1):
            for loop in (0,5,500,20475):
                steps=[p.MacroStep(rng.randrange(1<<24),rng.randrange(65536)*5) for _ in range(count)]
                emit('macro',loop,trigger,';'.join(f'{s.mask}:{s.duration_ms}' for s in steps) or '-',p.build_macro_payload(steps,loop_interval_ms=loop,trigger=trigger).hex())
    for percent in range(101):
        current=bytes([6,31,254,90,12,99,65]);value=round(percent*255/100);expected=current[:1]+bytes([value,value])+current[3:];emit('vibration',percent,current.hex(),expected.hex())
    for index in range(256):
        curve=p.Curve(10,90,(35,35),(136,204),flags=index);raw=curve.to_bytes();emit('curve',raw.hex(),p.Curve.parse(raw).to_bytes().hex())
    for count in range(16):
        sources=bytes(range(1,count+1));targets=bytes(s if rng.randrange(2) else rng.randrange(1,32) for s in sources);emit('mapping',hx(sources),hx(targets),p.build_changekey_payload(sources,targets).hex())
    captured=0
    capture=ROOT/'captures/probe_log.jsonl'
    for line in capture.read_text(encoding='utf-8').splitlines():
        entry=json.loads(line)
        for field in ('raw_reply',):
            raw=entry.get(field)
            if raw:
                packet=p.parse(bytes.fromhex(raw));emit('decode',raw,f'{packet.opcode},{packet.length},{packet.serial},{packet.nonce},{packet.payload.hex()},{int(packet.crc_valid)}');captured+=1
    members=[];sessions=[]
    with zipfile.ZipFile(source) as archive:
        if archive.testzip() is not None:raise ValueError('Archive CRC failure')
        manifest=json.loads(archive.read('manifest.json'));metadata=json.loads(archive.read('metadata.json'))
        for entry in manifest['files']:
            raw=archive.read(entry['path']);actual=digest(raw)
            if len(raw)!=entry['size'] or actual!=entry['sha256']:raise ValueError('Member hash mismatch: '+entry['path'])
            members.append({'path':entry['path'],'bytes':len(raw),'sha256':actual,'manifestVerified':True})
        for entry in archive.namelist():
            if not entry.endswith('.jsonl'):continue
            frames=[json.loads(line) for line in archive.read(entry).decode('utf-8-sig').splitlines() if line.strip()]
            for frame in frames:
                values=frame.get('values',{})
                for axis in ('lx','ly','rx','ry'):
                    if axis in values:
                        raw=values[axis]
                        if not -32768<=raw<=32767:raise ValueError('Invalid recorded XInput axis')
                        emit('axis',raw,format(raw/(32768 if raw<0 else 32767),'.17g'))
            sessions.append({'file':entry,'samples':len(frames),'source':'observed XInput snapshots; model/transport are owner claims'})
    register={'archive':str(source),'sha256':digest(source.read_bytes()),'members':members,'memberCount':len(members),'claimedModel':metadata['claimedModel'],'claimedTransport':metadata['claimedTransport'],'modelDetected':metadata['modelDetected'],'configurationWrites':metadata['configurationWrites'],'classification':'CONFIRMED archive bytes; owner-claimed X05 Pro; gameplay only; no configuration authorization','sessions':sessions,'x15ConfigurationEvidenceInArchive':False}
    (out/'supplied-archive-register.json').write_text(json.dumps(register,indent=2),encoding='utf-8')
    path=out/'reference-vectors.tsv';path.write_text('\n'.join(vectors)+'\n',encoding='ascii')
    report={'vectors':len(vectors),'byKind':counts,'independentCapturedConfigReplies':captured,'captureSource':str(capture),'captureSha256':digest(capture.read_bytes()),'pythonOracleSha256':digest((ROOT/'x20ctl/protocol.py').read_bytes()),'vectorsSha256':digest(path.read_bytes()),'hardwareWrites':False,'x15WriteParity':'UNVERIFIED: no command/ACK/readback/persistence oracle supplied'}
    (out/'reference-vector-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))

if __name__=='__main__':main()
