"""Local native-process review. No configuration traffic or controller writes."""
from pathlib import Path
import json,queue,struct,subprocess,threading,time
ROOT=Path(__file__).resolve().parents[1]
EXE=ROOT/'artifacts/product-engine-phase/native-build/Release/x20ctl-engine.exe'
def main():
    checks=[]
    def check(value,name):
        checks.append({'name':name,'passed':bool(value)})
        if not value:raise AssertionError(name)
    proc=subprocess.Popen([str(EXE)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=subprocess.CREATE_NO_WINDOW)
    inbox=queue.Queue()
    def read():
        try:
            while True:
                head=proc.stdout.read(4)
                if not head:return
                size=struct.unpack('<I',head)[0]
                if not 0<size<=2*1024*1024:raise ValueError('invalid engine frame')
                body=proc.stdout.read(size);inbox.put(json.loads(body))
        except Exception as error:inbox.put(error)
    thread=threading.Thread(target=read,daemon=True);thread.start();events=[];counter=0
    def request(method,params=None,version=1):
        nonlocal counter
        counter+=1;identity=f'review-{counter}';data=json.dumps({'protocolVersion':version,'requestId':identity,'method':method,'params':params or {}}).encode();frame=struct.pack('<I',len(data))+data
        proc.stdin.write(frame[:1]);proc.stdin.flush();proc.stdin.write(frame[1:]);proc.stdin.flush();end=time.monotonic()+3
        while time.monotonic()<end:
            message=inbox.get(timeout=max(.01,end-time.monotonic()))
            if isinstance(message,Exception):raise message
            if message.get('kind')=='event':events.append(message);continue
            if message.get('requestId')==identity:return message
        raise TimeoutError(method)
    try:
        hello=request('handshake');check(hello['ok'] and hello['result']['maxFrameBytes']==2097152,'fragmented framing and protocol handshake')
        epoch=hello['result']['engineEpoch'];caps={}
        for model in ('x20','x15'):
            value=request('capabilities',{'model':model});caps[model]=value['result'];check(value['ok'] and not value['result']['configurationConnected'] and all(not f['liveWriteVerified'] for f in value['result']['features'].values()),model+' reports independent locked feature gates')
        fake=request('apply',{'model':'x20','feature':'buttons','verifiedWrite':True,'sessionValid':True,'payload':'00'});check(not fake['ok'] and fake['error']['code']=='WRITES_LOCKED','direct caller cannot authorize writes through frontend claims')
        check(not request('rawPacket',{'hex':'9006010000'})['ok'],'raw packet dispatch is not exposed')
        check(not request('handshake',version=99)['ok'],'incompatible protocol is rejected')
        sub=request('subscribe',{'stream':'connection'});identity=sub['result']['subscriptionId'];event=inbox.get(timeout=3);check(event['kind']=='event' and event['subscriptionId']==identity and event['engineEpoch']==epoch and event['sequence']>=1 and event['timestampUs']>=0 and event['droppedEventCount']==0,'asynchronous stream has epoch sequence timestamp and explicit loss')
        check(request('unsubscribe')['result']['ended'],'unsubscribe ends stream')
        sources=request('listGameplaySources')['result'];check(all(source['modelIdentity'] is None for source in sources['sources']),'read-only XInput enumeration never establishes controller model identity')
        request('shutdown');proc.stdin.close();check(proc.wait(timeout=3)==0,'owned native host shuts down cleanly')
        bad=subprocess.Popen([str(EXE)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=subprocess.CREATE_NO_WINDOW)
        try:bad.stdin.write(struct.pack('<I',2097153));bad.stdin.flush();check(bad.wait(timeout=3)!=0,'oversized framing rejected before allocation without shutdown deadlock')
        finally:
            if bad.poll() is None:bad.kill()
        report={'status':'passed','checks':checks,'capabilities':caps,'gameplaySources':sources,'hardwareAccess':'read-only XInput slot enumeration only; no BLE/configuration session','hardwareWrites':False,'directWriteRefused':True}
        (ROOT/'artifacts/product-engine-phase/native-ipc-review.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps({'status':'passed','checks':len(checks),'xinputSources':len(sources['sources'])}))
    finally:
        if proc.poll() is None:proc.kill()
        proc.wait(timeout=3)
if __name__=='__main__':main()
