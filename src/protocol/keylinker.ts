// KeyLinker / EasySMX X20 Bluetooth GATT Protocol & Packet Serialization

export const KEYLINKER_SERVICE_UUID = '0000ffe0-0000-1000-8000-00805f9b34fb';
export const KEYLINKER_CHAR_NOTIFY_UUID = '0000ffe1-0000-1000-8000-00805f9b34fb';
export const KEYLINKER_CHAR_WRITE_UUID = '0000ffe2-0000-1000-8000-00805f9b34fb';

export enum PacketOpcode {
  QUERY_INFO = 0x01,
  READ_BUTTONS = 0x02,
  WRITE_BUTTONS = 0x03,
  READ_MACROS = 0x04,
  WRITE_MACROS = 0x05,
  SET_VIBRATION = 0x06,
  SET_POWER_TIMEOUT = 0x07,
  WRITE_CURVES = 0x08,
  FACTORY_RESET = 0x09,
  QUERY_BATTERY = 0x0a,
}

export function buildKeyLinkerPacket(opcode: PacketOpcode, payload: Uint8Array = new Uint8Array(0)): Uint8Array {
  // KeyLinker framing: [0xAA, 0x55, Opcode, Length, ...Payload, Checksum]
  const packet = new Uint8Array(5 + payload.length);
  packet[0] = 0xaa;
  packet[1] = 0x55;
  packet[2] = opcode;
  packet[3] = payload.length;
  packet.set(payload, 4);

  // Simple additive checksum
  let sum = 0;
  for (let i = 2; i < 4 + payload.length; i++) {
    sum = (sum + packet[i]) & 0xff;
  }
  packet[4 + payload.length] = sum;
  return packet;
}

export interface BleConnectionHandler {
  connected: boolean;
  device: BluetoothDevice | null;
  writeChar: BluetoothRemoteGATTCharacteristic | null;
  notifyChar: BluetoothRemoteGATTCharacteristic | null;
}

export async function requestX20Device(): Promise<BluetoothDevice | null> {
  if (typeof navigator === 'undefined' || !navigator.bluetooth) {
    throw new Error('Web Bluetooth is not supported in this browser. You can still use the Gamepad API or Demo Mode.');
  }

  try {
    const device = await navigator.bluetooth.requestDevice({
      filters: [
        { namePrefix: 'X20' },
        { namePrefix: 'EasySMX' },
        { namePrefix: 'KeyLinker' },
      ],
      optionalServices: [KEYLINKER_SERVICE_UUID, 'battery_service', 'device_information'],
    });
    return device;
  } catch (err: any) {
    if (err.name === 'NotFoundError') {
      return null;
    }
    throw err;
  }
}
