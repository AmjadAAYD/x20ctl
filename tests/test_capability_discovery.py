import pytest
import json
import struct
from uuid import UUID
from x20ctl import protocol as p

E1="d7f010e1-660d-46e9-96c3-19c4148bdab5"
E2="d7f010e2-660d-46e9-96c3-19c4148bdab5"

def frame(op,payload,serial,direction):
 return {"stream":"one","direction":direction,"characteristicUuid":E1 if direction=="write" else E2,
         "data":p.build(op if direction=="write" else p.Op.RESPONSE,payload,serial=serial,nonce=7)}

def test_captured_capabilities_are_dynamic_without_authorizing_writes():
 from x20ctl.scanning.capability_discovery import discover
 events=[frame(0x91,b"",1,"write"),frame(0x91,bytes.fromhex("0710013001022447"),1,"notify"),
         frame(p.Op.HOST_MENU,b"\x00\x01",2,"write"),frame(p.Op.HOST_MENU,bytes([10,3,3,3,1,3,1,0,0,0,0]),2,"notify"),
         frame(p.Op.HOST_MENU,b"\x00\x03",3,"write"),frame(p.Op.HOST_MENU,bytes([4,3,1,2,93]),3,"notify")]
 result=discover(events)
 profile=result["profiles"][0]
 assert profile["reportedFeatures"]["remapping"] is True
 assert profile["reportedFeatures"]["macroSlots"]==["M1","M2"]
 assert profile["supportedSourceCodes"]==[1,2,93]
 assert profile["identity"]["deviceFamily"]==3
 assert profile["modelIdentified"] is False and profile["configurationWritesAuthorized"] is False


def test_unpaired_menu_response_does_not_become_capabilities():
 from x20ctl.scanning.capability_discovery import discover
 result=discover([frame(p.Op.HOST_MENU,bytes([10,3,3,3,1,3,1,0,0,0,0]),2,"notify")])
 assert result["profiles"]==[]


def test_crc_corruption_and_wrong_characteristic_are_rejected():
 from x20ctl.scanning.capability_discovery import discover
 event=frame(0x91,bytes.fromhex("0710013001022447"),1,"notify")
 event["data"]=event["data"][:-1]+bytes([event["data"][-1]^1])
 assert discover([event])["profiles"]==[]


def record(raw, incoming):
 return struct.pack(">IIIIq",len(raw),len(raw),int(incoming),0,0)+raw


def acl(att,incoming,handle=11,boundary=2):
 body=struct.pack("<HH",len(att),4)+att if boundary==2 else att
 raw=b"\x02"+struct.pack("<HH",handle|(boundary<<12),len(body))+body
 return record(raw,incoming)


def discovery(handle=11,write_handle=102,notify_handle=104):
 from x20ctl.scanning.capability_discovery import SERVICE
 uuid=lambda value: UUID(value).bytes[::-1]
 return [
  acl(b"\x10"+struct.pack("<HHH",1,65535,0x2800),False,handle),
  acl(b"\x11\x14"+struct.pack("<HH",100,110)+uuid(SERVICE),True,handle),
  acl(b"\x08"+struct.pack("<HHH",100,110,0x2803),False,handle),
  acl(b"\x09\x15"+struct.pack("<HBH",write_handle-1,8,write_handle)+uuid(E1)
      +struct.pack("<HBH",notify_handle-1,16,notify_handle)+uuid(E2),True,handle)]


def capture(parts):
 return b"btsnoop\0"+struct.pack(">II",1,1002)+b"".join(parts)


def exchange(serial=2,handle=11,write_handle=102,notify_handle=104,fragment=False):
 query=p.build(p.Op.HOST_MENU,b"\x00\x01",serial=serial,nonce=7)
 response=p.build(p.Op.RESPONSE,bytes([10,3,3,3,1,3,1,0,0,0,0]),serial=serial,nonce=7)
 att=b"\x1b"+struct.pack("<H",notify_handle)+response
 if fragment:
  reply=[acl(att[:9],True,handle)]
  # First fragment's L2CAP header declares the whole ATT PDU.
  first=bytearray(reply[0]); struct.pack_into("<H",first,29,len(att))
  reply=[bytes(first),acl(att[9:],True,handle,boundary=1)]
 else:
  reply=[acl(att,True,handle)]
 return [acl(b"\x12"+struct.pack("<H",write_handle)+query,False,handle),*reply]


@pytest.mark.parametrize("fragment",[False,True])
def test_android_capture_uses_discovered_handles_and_reassembles_acl(fragment):
 from x20ctl.scanning.capability_discovery import analyze_trace
 result=analyze_trace(capture(discovery(write_handle=106,notify_handle=108)+exchange(write_handle=106,notify_handle=108,fragment=fragment)))
 assert result["profiles"][0]["reportedFeatures"]["macroSlots"]==["M1","M2"]
 assert result["configurationWritesAuthorized"] is False


def test_no_discovery_or_missing_query_does_not_invent_handles_or_capabilities():
 from x20ctl.scanning.capability_discovery import analyze_trace
 assert analyze_trace(capture(exchange()))["profiles"]==[]
 assert analyze_trace(capture(discovery()+exchange()[1:]))["profiles"]==[]


def test_disconnect_discards_handle_map_even_when_connection_handle_is_reused():
 from x20ctl.scanning.capability_discovery import analyze_trace
 disconnect=record(b"\x04\x05\x04\x00\x0b\x00\x13",True)
 assert analyze_trace(capture(discovery()+[disconnect]+exchange()))["profiles"]==[]


def test_two_connections_do_not_mix_requests_or_claim_model_identity():
 from x20ctl.scanning.capability_discovery import analyze_trace
 result=analyze_trace(capture(discovery(11)+discovery(12)+exchange(handle=11)+exchange(handle=12)))
 assert len(result["profiles"])==2
 assert all(not item["modelIdentified"] for item in result["profiles"])
 assert analyze_trace(capture(discovery(11)+discovery(12)+exchange(handle=11)[:1]+exchange(handle=12)[1:]))["profiles"]==[]


def test_wrong_response_opcode_and_serial_reuse_are_not_feature_evidence():
 from x20ctl.scanning.capability_discovery import discover
 query=frame(p.Op.HOST_MENU,b"\x00\x01",2,"write")
 response=frame(0,bytes([10,3,3,3,1,3,1,0,0,0,0]),2,"notify")
 wrong={**response,"data":p.build(p.Op.HOST_MENU,bytes([10,3,3,3,1,3,1,0,0,0,0]),serial=2,nonce=7)}
 assert discover([query,wrong])["profiles"]==[]
 assert discover([query,frame(p.Op.WRITE_CHANGEKEY,b"\x00",2,"write"),response])["profiles"]==[]


def test_menu_continuation_requires_complete_ordered_chunks():
 from x20ctl.scanning.capability_discovery import discover
 first=[frame(p.Op.HOST_MENU,b"\x00\x03",1,"write"),frame(0,b"\x04\x03\x01",1,"notify")]
 continuation=[frame(p.Op.HOST_MENU,b"\x01\x03",2,"write"),frame(0,b"\x02\x5d",2,"notify")]
 assert discover(first)["profiles"]==[]
 assert discover(continuation)["profiles"]==[]
 assert discover(first+continuation)["profiles"][0]["supportedSourceCodes"]==[1,2,93]


def test_windows_trace_is_retained_as_unsupported_instead_of_inventing_protocol():
 from x20ctl.scanning.capability_discovery import analyze_trace
 payload=b"\x04\x0e\x00"
 data=struct.pack("<IHHIIII",0xA1B2C3D4,2,4,0,0,65535,201)+struct.pack("<IIII",0,0,len(payload),len(payload))+payload
 assert analyze_trace(data)["status"]=="unsupported_capture"


def test_incomplete_container_is_rejected():
 from x20ctl.scanning.capability_discovery import analyze_trace
 with pytest.raises(ValueError): analyze_trace(capture(discovery()+exchange())[:-1])


def test_scanner_import_and_removal_update_saved_profile_and_review(tmp_path):
 from x20ctl.desktop.research_scan import ResearchScanner
 from x20ctl.scanning.capability_discovery import discover
 scanner=ResearchScanner(tmp_path)
 scanner.folder=tmp_path
 scanner.output=tmp_path/"evidence"
 scanner.output.mkdir()
 scanner.options={"model":"X15"}
 scanner.attachment_scopes={}
 scanner.view["prompt"]={"id":"attach","kind":"file","choices":["trace"]}
 scanner.view["state"]="waiting"
 path=tmp_path/"synthetic.btsnoop"
 path.write_bytes(capture(discovery()+exchange()))
 scanner.attach_file("attach",path)
 saved=json.loads((scanner.output/"attachments/capability-profile.json").read_text())
 assert saved==scanner.status()["detectedCapabilities"]
 assert saved["profiles"][0]["reportedFeatures"]["remapping"] is True
 scanner.view["state"]="review"
 scanner.remove_attachment("attachments/trace.btsnoop")
 assert scanner.status()["detectedCapabilities"] is None
 assert not (scanner.output/"attachments/capability-profile.json").exists()
 event=frame(0x91,bytes.fromhex("0710013001022447"),1,"notify");event["characteristicUuid"]="unknown"
 assert discover([event])["profiles"]==[]
