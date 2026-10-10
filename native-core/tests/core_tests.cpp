#include "x20_codec.hpp"
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <cmath>
using namespace x20ctl;
static std::vector<std::string> split(const std::string& line,char separator){std::istringstream stream(line);std::vector<std::string> result;std::string item;while(std::getline(stream,item,separator))result.push_back(item);return result;}
int main(int argc,char** argv){try{if(argc!=2)throw std::runtime_error("pass reference vectors path");std::ifstream source(argv[1]);if(!source)throw std::runtime_error("missing reference vectors");std::string line;int checked=0,captures=0;while(std::getline(source,line)){if(line.empty())continue;auto f=split(line,'\t');std::string actual;
if(f[0]=="frame")actual=hex(frame(static_cast<std::uint8_t>(std::stoi(f[1])),static_cast<std::uint8_t>(std::stoi(f[2])),static_cast<std::uint8_t>(std::stoi(f[3])),static_cast<std::uint8_t>(std::stoi(f[4])),from_hex(f[5])));
else if(f[0]=="decode"){auto packet=decode(from_hex(f[1]));actual=std::to_string(packet.op)+","+std::to_string(packet.length)+","+std::to_string(packet.serial)+","+std::to_string(packet.nonce)+","+hex(packet.payload)+","+(packet.crc_valid?"1":"0");++captures;}
else if(f[0]=="macro"){std::vector<MacroEntry> steps;if(f[3]!="-")for(auto entry:split(f[3],';')){auto parts=split(entry,':');steps.push_back({static_cast<std::uint32_t>(std::stoul(parts[0])),std::stoi(parts[1])});}actual=hex(macro_payload(steps,std::stoi(f[1]),std::stoi(f[2])));}
else if(f[0]=="mapping")actual=hex(mapping(from_hex(f[1]),from_hex(f[2])));
else if(f[0]=="vibration")actual=hex(vibration(std::stoi(f[1]),from_hex(f[2])));
else if(f[0]=="curve")actual=hex(curve(from_hex(f[1])));
else if(f[0]=="axis"){if(std::abs(normalize_axis(std::stoi(f[1]))-std::stod(f.back()))>1e-12)throw std::runtime_error("axis mismatch");++checked;continue;}
else if(f[0]=="serial")actual=std::to_string(save_serial(std::stoi(f[1]),std::stoi(f[2])));
else if(f[0]=="length")actual=std::to_string(host_length(std::stoi(f[1]),std::stoi(f[2])));
else throw std::runtime_error("unknown vector");if(actual!=f.back())throw std::runtime_error("parity mismatch at vector "+std::to_string(checked)+": "+f[0]);++checked;}
if(checked<1000)throw std::runtime_error("insufficient parity fixture coverage");int rejects=0;try{decode(Bytes(4));}catch(const std::invalid_argument&){++rejects;}try{frame(0,0,0,0,Bytes(16));}catch(const std::invalid_argument&){++rejects;}try{macro_payload(std::vector<MacroEntry>(48),0,0);}catch(const std::invalid_argument&){++rejects;}try{macro_payload(std::array{MacroEntry{0,7}},0,0);}catch(const std::invalid_argument&){++rejects;}if(rejects!=4||authorize_write("x20","buttons")||authorize_write("x15","buttons"))throw std::runtime_error("unsafe gates");std::cout<<"PASS vectors="<<checked<<" decode_cases="<<captures<<" malformed_rejections="<<rejects<<" hardware_writes=false\n";return 0;}catch(const std::exception& error){std::cerr<<error.what()<<"\n";return 1;}}
