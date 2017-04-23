
using arookas.IO.Binary;

namespace arookas {

	static class BinaryUtility {

		public static int Read24(this aBinaryReader reader) {
			var byte1 = reader.Read8();
			var byte2 = reader.Read8();
			var byte3 = reader.Read8();

			if (reader.Endianness == Endianness.Big) {
				return ((byte1 << 16) | (byte2 << 8) | byte3);
			} else {
				return ((byte3 << 16) | (byte2 << 8) | byte1);
			}
		}

		public static void Write24(this aBinaryWriter writer, int value) {
			var byte1 = (byte)((value >> 16) & 0xFF);
			var byte2 = (byte)((value >> 8) & 0xFF);
			var byte3 = (byte)(value & 0xFF);

			if (writer.Endianness == Endianness.Big) {
				writer.Write8(byte1);
				writer.Write8(byte2);
				writer.Write8(byte3);
			} else {
				writer.Write8(byte3);
				writer.Write8(byte2);
				writer.Write8(byte1);
			}
		}

	}

}
