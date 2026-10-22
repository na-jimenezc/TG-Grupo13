// JoyconProtocol.cs
//
// Port a C# del protocolo UDP de common/protocol.h (servidor joycon_vrpn_server).
// No depende de UnityEngine, así que también se puede usar/probar fuera de Unity.
//
// Formato (todo little-endian, empaquetado byte a byte, sin padding):
//
//   Header (6 bytes):  magic u32 = 0x5256434A | version u8 = 1 | type u8
//   Hello      (cliente -> servidor): solo el header, type = 1
//   PoseUpdate (servidor -> cliente): header (type = 2) + count u8 +
//              count * [ sensorIndex u8 | x f32 | y f32 | z f32 | w f32 ]   (17 bytes c/u)
//
// El cuaternión llega tal cual lo produce el filtro AHRS del servidor
// (marco diestro, ver common/ahrs_filter.h). La conversión al marco de Unity
// (zurdo, Y arriba) se hace en PostureMath.ToUnity, no aquí.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PujJoycon
{
    public enum MessageType : byte
    {
        Hello = 1,
        PoseUpdate = 2,
    }

    /// <summary>Cuaternión crudo tal como lo envía el servidor (sin conversión de ejes).</summary>
    [Serializable]
    public struct RawQuaternion
    {
        public float X, Y, Z, W;

        public RawQuaternion(float x, float y, float z, float w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        public static readonly RawQuaternion Identity = new RawQuaternion(0f, 0f, 0f, 1f);

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4}, {W:F4})";
    }

    public struct SensorPose
    {
        public byte SensorIndex;
        public RawQuaternion Orientation;
    }

    public static class JoyconProtocol
    {
        public const uint Magic = 0x5256434A;
        public const byte Version = 1;
        public const int MaxSensors = 8;
        public const int DefaultPort = 60123;

        public const int HeaderSize = 6;
        public const int BytesPerSensor = 17;
        public const int MaxPacketSize = HeaderSize + 1 + MaxSensors * BytesPerSensor;

        // Reinterpreta los bits de un uint como float sin asignar memoria y sin
        // depender del endianness del host (los bytes se ensamblan a mano).
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public uint U;
            [FieldOffset(0)] public float F;
        }

        public static byte[] EncodeHello()
        {
            var buf = new byte[HeaderSize];
            WriteHeader(buf, MessageType.Hello);
            return buf;
        }

        /// <summary>Solo para pruebas (servidor falso): codifica un PoseUpdate igual que el servidor C++.</summary>
        public static byte[] EncodePoseUpdate(IList<SensorPose> sensors)
        {
            var buf = new byte[HeaderSize + 1 + sensors.Count * BytesPerSensor];
            WriteHeader(buf, MessageType.PoseUpdate);
            buf[HeaderSize] = (byte)sensors.Count;
            int offset = HeaderSize + 1;
            foreach (var s in sensors)
            {
                buf[offset] = s.SensorIndex;
                WriteFloat(buf, offset + 1, s.Orientation.X);
                WriteFloat(buf, offset + 5, s.Orientation.Y);
                WriteFloat(buf, offset + 9, s.Orientation.Z);
                WriteFloat(buf, offset + 13, s.Orientation.W);
                offset += BytesPerSensor;
            }
            return buf;
        }

        /// <summary>Valida magic/versión y devuelve el tipo. False si no es un paquete de este protocolo.</summary>
        public static bool TryDecodeHeader(byte[] data, int length, out MessageType type)
        {
            type = 0;
            if (data == null || length < HeaderSize) return false;
            if (ReadU32(data, 0) != Magic) return false;
            if (data[4] != Version) return false;
            type = (MessageType)data[5];
            return true;
        }

        /// <summary>
        /// Decodifica un PoseUpdate en <paramref name="output"/> (sin asignar memoria).
        /// Devuelve cuántos sensores se escribieron, o -1 si el paquete está truncado/corrupto.
        /// Llamar a TryDecodeHeader antes para confirmar el tipo.
        /// </summary>
        public static int DecodePoseUpdate(byte[] data, int length, SensorPose[] output)
        {
            if (data == null || length < HeaderSize + 1) return -1;
            int count = data[HeaderSize];
            if (count > output.Length) return -1;
            if (HeaderSize + 1 + count * BytesPerSensor > length) return -1;

            int offset = HeaderSize + 1;
            for (int i = 0; i < count; i++)
            {
                output[i].SensorIndex = data[offset];
                output[i].Orientation = new RawQuaternion(
                    ReadFloat(data, offset + 1),
                    ReadFloat(data, offset + 5),
                    ReadFloat(data, offset + 9),
                    ReadFloat(data, offset + 13));
                offset += BytesPerSensor;
            }
            return count;
        }

        private static void WriteHeader(byte[] buf, MessageType type)
        {
            WriteU32(buf, 0, Magic);
            buf[4] = Version;
            buf[5] = (byte)type;
        }

        private static void WriteU32(byte[] buf, int offset, uint v)
        {
            buf[offset] = (byte)(v & 0xFF);
            buf[offset + 1] = (byte)((v >> 8) & 0xFF);
            buf[offset + 2] = (byte)((v >> 16) & 0xFF);
            buf[offset + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static uint ReadU32(byte[] p, int offset)
        {
            return (uint)p[offset]
                 | ((uint)p[offset + 1] << 8)
                 | ((uint)p[offset + 2] << 16)
                 | ((uint)p[offset + 3] << 24);
        }

        private static void WriteFloat(byte[] buf, int offset, float f)
        {
            var bits = new FloatBits { F = f };
            WriteU32(buf, offset, bits.U);
        }

        private static float ReadFloat(byte[] p, int offset)
        {
            var bits = new FloatBits { U = ReadU32(p, offset) };
            return bits.F;
        }
    }
}
