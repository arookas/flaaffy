
using arookas.IO.Binary;
using arookas.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace arookas {

	enum Errand {

		Shock,
		Whap,
		Wave,
		Cotton,
		Jolt,
		Charge,

	}

	interface IErrand {

		void LoadParams(string[] arguments);
		void Perform();

	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	class ErrandAttribute : Attribute {

		Errand mErrand;

		public Errand Errand { get { return mErrand; } }

		public ErrandAttribute(Errand errand) {
			if (!errand.IsDefined()) {
				throw new ArgumentOutOfRangeException("errand");
			}

			mErrand = errand;
		}

		static Dictionary<Errand, Type> sErrands;

		public static void GatherErrandsInAssembly() {
			GatherErrandsInAssembly(Assembly.GetExecutingAssembly());
		}
		public static void GatherErrandsInAssembly(Assembly assembly) {
			if (sErrands == null) {
				sErrands = new Dictionary<Errand, Type>(32);
			}

			var query =
				from type in assembly.GetTypes()
				where !type.IsAbstract
				where type.GetInterfaces().Contains(typeof(IErrand)) // needs to implement IErrand
				where type.GetConstructor(Type.EmptyTypes) != null // requires a parameterless constructor
				let attribute = type.GetCustomAttribute<ErrandAttribute>()
				where attribute != null
				let action = attribute.Errand
				select new { action, type };

			foreach (var result in query) {
				sErrands[result.action] = result.type;
			}
		}

		public static IErrand CreateErrand(Errand errand) {
			if (sErrands == null) {
				GatherErrandsInAssembly();
			}

			Type type;

			if (!sErrands.TryGetValue(errand, out type)) {
				return null;
			}

			return (Activator.CreateInstance(type) as IErrand);
		}

	}

	abstract class SimpleConverterErrand : IErrand {

		protected string mInputFile, mOutputFile;
		protected IOFormat mInputFormat, mOutputFormat;

		public virtual void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter parameter;

			parameter = mareep.GetLastCmdParam(cmdline, "-input");

			if (parameter == null) {
				mareep.WriteError("SYSTEM: missing -input parameter.");
			}

			if (parameter.Count != 2) {
				mareep.WriteError("SYSTEM: -input parameter requires two arguments.");
			}

			mInputFile = parameter[0];

			if (!File.Exists(mInputFile)) {
				mareep.WriteError("SYSTEM: input file '{0}' could not be found.", mInputFile);
			}

			if (!Enum.TryParse(parameter[1], true, out mInputFormat)) {
				mareep.WriteError("SYSTEM: unknown input format '{0}'.", parameter[1]);
			}

			parameter = mareep.GetLastCmdParam(cmdline, "-output");

			if (parameter == null) {
				mareep.WriteError("SYSTEM: missing -output parameter.");
			}

			if (parameter.Count != 2) {
				mareep.WriteError("SYSTEM: -output parameter requires two arguments.");
			}

			mOutputFile = parameter[0];

			if (!Enum.TryParse(parameter[1], true, out mOutputFormat)) {
				mareep.WriteError("SYSTEM: unknown output format '{0}'.", parameter[1]);
			}
		}
		public abstract void Perform();

		protected static bool IsFormatBinary(IOFormat format) {
			return (format == IOFormat.LittleBinary || format == IOFormat.BigBinary);
		}
		protected static Endianness GetFormatEndianness(IOFormat format) {
			switch (format) {
				case IOFormat.LittleBinary: return Endianness.Little;
				case IOFormat.BigBinary: return Endianness.Big;
				default: throw new ArgumentOutOfRangeException("format");
			}
		}

		protected static xDocument CreateXmlInput(Stream stream) {
			return new xDocument(stream);
		}
		protected static aBinaryReader CreateLittleBinaryInput(Stream stream) {
			return new aBinaryReader(stream, Endianness.Little, Encoding.GetEncoding(932));
		}
		protected static aBinaryReader CreateBigBinaryInput(Stream stream) {
			return new aBinaryReader(stream, Endianness.Big, Encoding.GetEncoding(932));
		}

		protected static XmlWriter CreateXmlOutput(Stream stream) {
			var settings = new XmlWriterSettings() {
				NewLineChars = "\n",
				Indent = true,
				IndentChars = "\t",
				CloseOutput = false,
				WriteEndDocumentOnClose = true,
			};

			return XmlWriter.Create(stream, settings);
		}
		protected static aBinaryWriter CreateLittleBinaryOutput(Stream stream) {
			return new aBinaryWriter(stream, Endianness.Little, Encoding.GetEncoding(932));
		}
		protected static aBinaryWriter CreateBigBinaryOutput(Stream stream) {
			return new aBinaryWriter(stream, Endianness.Big, Encoding.GetEncoding(932));
		}

		protected enum IOFormat {

			Xml,
			LittleBinary,
			BigBinary,

		}

	}

	static partial class mareep {

		static Errand ReadErrand(string[] arguments) {
			var cmdline = new aCommandLine(arguments);

			var parameter = mareep.GetLastCmdParam(cmdline, "-errand");

			if (parameter == null) {
				mareep.WriteError("SYSTEM: missing -errand parameter.");
			}

			if (parameter.Count == 0) {
				mareep.WriteError("SYSTEM: missing errand name.");
			}

			Errand errand;

			if (!Enum.TryParse(parameter[0], true, out errand)) {
				mareep.WriteError("SYSTEM: unknown errand '{0}'.", parameter[0]);
			}

			return errand;
		}
		static IErrand InitErrand(Errand errand) {
			var instance = ErrandAttribute.CreateErrand(errand);

			if (instance == null) {
				mareep.WriteError("SYSTEM: unknown errand '{0}'.", errand);
			}

			return instance;
		}

	}

}
