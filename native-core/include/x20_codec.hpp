#pragma once
#include <array>
#include <cstdint>
#include <span>
#include <string>
#include <vector>
namespace x20ctl {
using Bytes=std::vector<std::uint8_t>;
std::uint8_t crc8(std::span<const std::uint8_t> bytes);
Bytes scramble(std::span<const std::uint8_t> bytes);
Bytes unscramble(std::span<const std::uint8_t> bytes);
Bytes frame(std::uint8_t op,std::uint8_t length,std::uint8_t serial,std::uint8_t nonce,std::span<const std::uint8_t> payload);
struct Packet {std::uint8_t op,length,serial,nonce;Bytes payload;bool crc_valid;};
Packet decode(std::span<const std::uint8_t> bytes);
std::uint8_t save_serial(int slot,int counter);
std::uint8_t host_length(int payload_size,int counter);
struct MacroEntry {std::uint32_t mask;int duration_ms;};
Bytes macro_payload(std::span<const MacroEntry> entries,int loop_ms,int trigger);
Bytes mapping(std::span<const std::uint8_t> sources,std::span<const std::uint8_t> targets);
Bytes vibration(int percent,std::span<const std::uint8_t> current_body);
Bytes curve(std::span<const std::uint8_t> channel);
double normalize_axis(int raw);
Bytes from_hex(const std::string& text);
std::string hex(std::span<const std::uint8_t> bytes);
// Physical identity/transport/evidence are engine-owned. This milestone has no config transport.
bool authorize_write(const std::string& model,const std::string& feature);
}
