"""Independent polynomial long-division check; imports no implementation under test."""
from pathlib import Path
import json,struct
root=Path(__file__).resolve().parents[1]
def crc(data):
 state=0
 for byte in data:
  for bit in range(8):
   feedback=((state>>31)&1)^((byte>>bit)&1);state=(state<<1)&0xffffffff
   if feedback:state^=0x04c11db7
 return int(f'{state:032b}'[::-1],2)
assert crc(b'123456789')==0x2dfd2d88
folder=root/'ground-truth/outputs/gradient'
if folder.exists():
 asset=next(folder.glob('*_cropped.bin')).read_bytes();meta=(folder/'metadata.bin').read_bytes();calls=[bytes.fromhex(json.loads(line)['hex']) for line in (folder/'serial-writes.jsonl').read_text().splitlines()]
else:
 fixture=root.parents[1]/'desktop-dotnet/X20Ctl.Zone.Tests/Data/Screen';asset=(fixture/'gradient.bin').read_bytes();meta=(fixture/'gradient.metadata').read_bytes();raw=(fixture/'vendor-upload.frames').read_bytes();calls=[];offset=0
 while offset<len(raw):
  length=struct.unpack_from('<I',raw,offset)[0];offset+=4;calls.append(raw[offset:offset+length]);offset+=length

head=struct.pack('<IIII',0x19714568,3,len(asset),crc(asset));assert head+struct.pack('<I',crc(head))+bytes([255])*(4096-20)==meta
packet=b'\x6c'+struct.pack('<I',0x410000)+b'\x00'+struct.pack('<H',2048)+asset[:2048];assert packet+struct.pack('<I',crc(packet))==next(p for p in calls if p[0]==0x6c)
query=b'\x6e'+struct.pack('<III',0x410000,len(asset),crc(asset));assert query+struct.pack('<I',crc(query))+bytes(47)==next(p for p in calls if p[0]==0x6e)
h=next(p for p in calls if p[0]==0x4b);assert crc(h[:8]+struct.pack('<I',0x19717589))==int.from_bytes(h[8:12],'little');assert crc(h[:32])==int.from_bytes(h[32:36],'little')
print('Independent CRC, metadata, data frame, CRC query and handshake: exact')
