using RIoT2.Core.Utils;
using System;
using System.Globalization;
using System.Linq;

namespace RIoT2.Core.Models
{
    public class AdvertisementData
    {
        public string AdvertisementType { get; set; }
        public short Rssi { get; set; }
        public string LocalName { get; set; }
        public string Data { get; set; }
        public ulong Address { get; set; }

        public BeaconData GetAsBeacon() 
        {
            try 
            {
                var bytes = ParseHexBytes(Data);
                return BeaconData.FromBytes(bytes);
            }
            catch 
            {
                // not beacon
            }
            return null;
        }

        public string AddressAsHex()
        {
            return string.Format("{0:X4}", Address);
        }
        
        public string ToJson()
        {
            return Json.Serialize(this);
        }

        private static byte[] ParseHexBytes(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                throw new ArgumentException("Beacon data is required.", nameof(data));

            var hex = data.Replace("-", "").Replace(" ", "");
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Beacon data must contain an even number of hex characters.", nameof(data));

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
    }

    public class BeaconData
    {
        public Guid Uuid { get; set; }
        public ushort Major { get; set; }
        public ushort Minor { get; set; }
        public sbyte TxPower { get; set; }
        public static BeaconData FromBytes(byte[] bytes)
        {
            if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }
            if (bytes.Length != 23) { throw new ArgumentException("Byte array length was expected to be 23", "bytes"); }
            if (bytes[0] != 0x02) { throw new ArgumentException("First byte in array was exptected to be 0x02", "bytes"); }
            if (bytes[1] != 0x15) { throw new ArgumentException("Second byte in array was expected to be 0x15", "bytes"); }
            return new BeaconData
            {
                Uuid = new Guid(
                        BitConverter.ToInt32(bytes.Skip(2).Take(4).Reverse().ToArray(), 0),
                        BitConverter.ToInt16(bytes.Skip(6).Take(2).Reverse().ToArray(), 0),
                        BitConverter.ToInt16(bytes.Skip(8).Take(2).Reverse().ToArray(), 0),
                        bytes.Skip(10).Take(8).ToArray()),
                Major = BitConverter.ToUInt16(bytes.Skip(18).Take(2).Reverse().ToArray(), 0),
                Minor = BitConverter.ToUInt16(bytes.Skip(20).Take(2).Reverse().ToArray(), 0),
                TxPower = (sbyte)bytes[22]
            };
        }
    }
}
