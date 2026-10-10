#include "x20_codec.hpp"
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/base.h>
#include <Windows.h>
#include <Xinput.h>
#include <io.h>
#include <fcntl.h>
#include <atomic>
#include <chrono>
#include <cmath>
#include <stdexcept>
#include <condition_variable>
#include <deque>
#include <iostream>
#include <mutex>
#include <thread>
using namespace winrt::Windows::Data::Json;
using namespace std::chrono_literals;
static constexpr std::uint32_t max_frame=2*1024*1024;
static JsonValue text(const std::string& value){return JsonValue::CreateStringValue(winrt::to_hstring(value));}
static JsonValue number(double value){return JsonValue::CreateNumberValue(value);}
static JsonValue boolean(bool value){return JsonValue::CreateBooleanValue(value);}
static JsonObject object(std::initializer_list<std::pair<const wchar_t*,IJsonValue>> values){JsonObject result;for(auto& [key,value]:values)result.Insert(key,value);return result;}
static JsonArray strings(std::initializer_list<const char*> values){JsonArray result;for(auto value:values)result.Append(text(value));return result;}
// The documented XInputGetState hides the Guide/Home button. XInputGetStateEx (xinput1_4 ordinal 100, also used by
// Steam and DS4Windows) reports it as 0x0400 in the same layout. Read-only gameplay input either way; fall back if absent.
struct XInputStateEx{DWORD packet;XINPUT_GAMEPAD gamepad;DWORD reserved;};
static DWORD read_gamepad(DWORD slot,XINPUT_STATE& state){
    using GetStateEx=DWORD(WINAPI*)(DWORD,XInputStateEx*);
    static GetStateEx ex=[]{HMODULE m=LoadLibraryW(L"xinput1_4.dll");return m?reinterpret_cast<GetStateEx>(GetProcAddress(m,reinterpret_cast<LPCSTR>(100))):nullptr;}();
    if(ex){XInputStateEx s{};DWORD r=ex(slot,&s);if(r==ERROR_SUCCESS){state.dwPacketNumber=s.packet;state.Gamepad=s.gamepad;}return r;}
    return XInputGetState(slot,&state);
}
static bool write_frame(const JsonObject& message){auto json=winrt::to_string(message.Stringify());auto size=static_cast<std::uint32_t>(json.size());char prefix[4];for(int i=0;i<4;++i)prefix[i]=static_cast<char>(size>>(i*8));std::cout.write(prefix,4);std::cout.write(json.data(),size);std::cout.flush();return static_cast<bool>(std::cout);}
int main(){try{
    _setmode(_fileno(stdin),_O_BINARY);_setmode(_fileno(stdout),_O_BINARY);winrt::init_apartment();
    std::mutex mutex;std::condition_variable wake;std::deque<JsonObject> replies,events;std::atomic<bool> stopping=false;std::string epoch=std::to_string(GetCurrentProcessId())+"-"+std::to_string(GetTickCount64());std::string subscription,input_context;int input_slot=-1;std::uint64_t sequence=0,drops=0;auto start=std::chrono::steady_clock::now();
    auto enqueue=[&](JsonObject value,bool reply){std::lock_guard lock(mutex);if(!reply&&winrt::to_string(value.GetNamedString(L"subscriptionId"))!=subscription)return;auto& queue=reply?replies:events;std::size_t capacity=reply?32:64;if(queue.size()>=capacity){if(reply){stopping=true;wake.notify_all();return;}queue.pop_front();++drops;}queue.push_back(value);wake.notify_one();};
    std::jthread writer([&]{winrt::init_apartment();while(true){JsonObject next{nullptr};{std::unique_lock lock(mutex);wake.wait(lock,[&]{return stopping||!replies.empty()||!events.empty();});if(replies.empty()&&events.empty()&&stopping)break;auto& queue=replies.empty()?events:replies;next=queue.front();queue.pop_front();}if(!write_frame(next)){stopping=true;break;}}});
    std::jthread input([&]{winrt::init_apartment();while(!stopping){std::this_thread::sleep_for(20ms);std::string id,context;int slot;std::uint64_t seq,dropped;{std::lock_guard lock(mutex);id=subscription;context=input_context;slot=input_slot;if(id.empty())continue;seq=++sequence;dropped=drops;}auto timestamp=std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now()-start).count();JsonObject payload;
        if(slot>=0){XINPUT_STATE state{};DWORD status=read_gamepad(static_cast<DWORD>(slot),state);JsonArray down;static const std::pair<const char*,unsigned int> buttons[]={{"HOME",0x400},{"A",0x1000},{"B",0x2000},{"X",0x4000},{"Y",0x8000},{"LB",0x100},{"RB",0x200},{"L3",0x40},{"R3",0x80},{"START",0x10},{"SELECT",0x20},{"DPAD_UP",1},{"DPAD_DOWN",2},{"DPAD_LEFT",4},{"DPAD_RIGHT",8}};if(status==ERROR_SUCCESS)for(auto [name,mask]:buttons)if(state.Gamepad.wButtons&mask)down.Append(text(name));payload=object({{L"connected",boolean(status==ERROR_SUCCESS)},{L"slot",number(slot)},{L"modelIdentity",JsonValue::CreateNullValue()},{L"buttons",down},{L"leftX",number(x20ctl::normalize_axis(state.Gamepad.sThumbLX))},{L"leftY",number(x20ctl::normalize_axis(state.Gamepad.sThumbLY))},{L"rightX",number(x20ctl::normalize_axis(state.Gamepad.sThumbRX))},{L"rightY",number(x20ctl::normalize_axis(state.Gamepad.sThumbRY))},{L"lt",number(state.Gamepad.bLeftTrigger/255.0)},{L"rt",number(state.Gamepad.bRightTrigger/255.0)}});}
        else payload=object({{L"configurationConnected",boolean(false)},{L"writesAuthorized",boolean(false)}});
        enqueue(object({{L"protocolVersion",number(1)},{L"kind",text("event")},{L"stream",text(slot<0?"connection":"input")},{L"subscriptionId",text(id)},{L"engineEpoch",text(epoch)},{L"sequence",number(static_cast<double>(seq))},{L"timestampUs",number(static_cast<double>(timestamp))},{L"sourceId",text(slot<0?"native-engine":"xinput:"+std::to_string(slot))},{L"type",text("snapshot")},{L"inputContext",text(context)},{L"droppedEventCount",number(static_cast<double>(dropped))},{L"dropCountMode",text("cumulative")},{L"payload",payload}}),false);
    }});
    struct StopGuard {std::atomic<bool>& stopping;std::condition_variable& wake;~StopGuard(){stopping=true;wake.notify_all();}} guard{stopping,wake};
    while(!stopping){char prefix[4];std::cin.read(prefix,4);if(std::cin.gcount()==0)break;if(std::cin.gcount()!=4)throw std::runtime_error("truncated frame header");std::uint32_t size=0;for(int i=0;i<4;++i)size|=static_cast<std::uint32_t>(static_cast<unsigned char>(prefix[i]))<<(i*8);if(size==0||size>max_frame)throw std::runtime_error("frame limit exceeded");std::string json(size,'\0');std::cin.read(json.data(),size);if(static_cast<std::uint32_t>(std::cin.gcount())!=size)throw std::runtime_error("truncated frame body");std::string id;JsonObject response;
        try{auto request=JsonObject::Parse(winrt::to_hstring(json));id=winrt::to_string(request.GetNamedString(L"requestId"));if(id.empty()||id.size()>128||request.GetNamedNumber(L"protocolVersion")!=1)throw std::invalid_argument("unsupported protocol or request identity");auto method=winrt::to_string(request.GetNamedString(L"method"));JsonObject result;
            if(method=="handshake")result=object({{L"engineVersion",text("0.1.0-native")},{L"engineEpoch",text(epoch)},{L"maxFrameBytes",number(max_frame)},{L"maxQueuedEvents",number(64)},{L"clockUnits",text("monotonic microseconds")},{L"methods",strings({"handshake","capabilities","validateDraft","listGameplaySources","subscribe","unsubscribe","shutdown"})}});
            else if(method=="capabilities"){auto parameters=request.GetNamedObject(L"params");auto model=winrt::to_string(parameters.GetNamedString(L"model"));JsonObject features;for(auto feature:{"buttons","curves","macros","vibration","triggers"})features.Insert(winrt::to_hstring(feature),object({{L"codecImplemented",boolean(model=="x20"&&std::string(feature)!="triggers")},{L"liveReadVerified",boolean(false)},{L"liveWriteVerified",boolean(false)},{L"readbackVerified",boolean(false)},{L"persistenceVerified",boolean(false)},{L"authorization",text("LOCKED")}}));result=object({{L"model",text(model)},{L"physicalIdentity",JsonValue::CreateNullValue()},{L"configurationConnected",boolean(false)},{L"features",features},{L"blockers",strings({"No verified physical identity/session","Native BLE configuration transport not yet connected","Feature-specific ACK/readback/persistence evidence required"})}});}
            else if(method=="validateDraft"){auto parameters=request.GetNamedObject(L"params");auto model=winrt::to_string(parameters.GetNamedString(L"model"));double strength=parameters.GetNamedNumber(L"vibration",70);bool valid=model=="x20"&&strength>=0&&strength<=100&&std::floor(strength)==strength;result=object({{L"valid",boolean(valid)},{L"scope",text("model and local vibration range only")},{L"hardwareAuthorized",boolean(false)}});}
            else if(method=="listGameplaySources"){JsonArray sources;for(DWORD slot=0;slot<4;++slot){XINPUT_STATE state{};if(XInputGetState(slot,&state)==ERROR_SUCCESS)sources.Append(object({{L"slot",number(slot)},{L"sourceId",text("xinput:"+std::to_string(slot))},{L"modelIdentity",JsonValue::CreateNullValue()},{L"kind",text("read-only XInput gameplay")}}));}result=object({{L"sources",sources},{L"configurationIdentityEstablished",boolean(false)}});}
            else if(method=="subscribe"){auto parameters=request.GetNamedObject(L"params");std::string stream=winrt::to_string(parameters.GetNamedString(L"stream"));int slot=-1;if(stream=="input"){double raw=parameters.GetNamedNumber(L"slot");if(raw<0||raw>3||std::floor(raw)!=raw||(parameters.GetNamedString(L"context")!=L"TESTER_CAPTURE"&&parameters.GetNamedString(L"context")!=L"UI_NAVIGATION"))throw std::invalid_argument("input requires explicit slot and capture context");slot=static_cast<int>(raw);}else if(stream!="connection")throw std::invalid_argument("unsupported stream");{std::lock_guard lock(mutex);subscription="subscription-"+id;input_slot=slot;input_context=stream=="input"?winrt::to_string(parameters.GetNamedString(L"context")):"NONE";sequence=0;events.clear();drops=0;}result=object({{L"subscriptionId",text("subscription-"+id)}});}
            else if(method=="unsubscribe"){std::lock_guard lock(mutex);subscription.clear();input_slot=-1;events.clear();result=object({{L"ended",boolean(true)}});}
            else if(method=="shutdown"){result=object({{L"stopped",boolean(true)}});stopping=true;}
            else {std::string code=(method=="apply"||method=="write")?"WRITES_LOCKED":"METHOD_NOT_ALLOWED";response=object({{L"protocolVersion",number(1)},{L"requestId",text(id)},{L"ok",boolean(false)},{L"error",object({{L"code",text(code)},{L"message",text("No verified configuration session or writable feature. No packet sent.")}})}});enqueue(response,true);continue;}
            response=object({{L"protocolVersion",number(1)},{L"requestId",text(id)},{L"ok",boolean(true)},{L"result",result}});
        }catch(const winrt::hresult_error&){response=object({{L"protocolVersion",number(1)},{L"requestId",text(id)},{L"ok",boolean(false)},{L"error",object({{L"code",text("BAD_REQUEST")},{L"message",text("Malformed JSON or missing typed fields")}})}});}catch(const std::exception& error){response=object({{L"protocolVersion",number(1)},{L"requestId",text(id)},{L"ok",boolean(false)},{L"error",object({{L"code",text("BAD_REQUEST")},{L"message",text(error.what())}})}});}enqueue(response,true);
    }
    stopping=true;wake.notify_all();input.join();writer.join();return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<"\n";return 1;}catch(const winrt::hresult_error& error){std::cerr<<winrt::to_string(error.message())<<"\n";return 1;}}
