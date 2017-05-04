
using System;
using arookas.Xml;
using System.Xml;

namespace arookas {

	static partial class mareep {

		public static int AsInt32(this xAttribute attribute, int missing = -1, int error = -1) {
			return (attribute != null ? (attribute | error) : missing);
		}
		public static int AsKeyNumber(this xAttribute attribute, int missing = 60, int error = -1) {
			if (attribute == null) {
				return missing;
			}

			var keynumber = mareep.ConvertKey(attribute.Value);

			if (0 <= keynumber && keynumber <= 127) {
				return keynumber;
			}

			keynumber = (attribute | -1);

			if (0 <= keynumber && keynumber <= 127) {
				return keynumber;
			}

			return error;
		}

		public static void WriteAttributeString(this XmlWriter writer, string name, int value) {
			writer.WriteAttributeString(name, value.ToString());
		}
		public static void WriteAttributeString(this XmlWriter writer, string name, float value) {
			writer.WriteAttributeString(name, value.ToString("R"));
		}

	}

}
