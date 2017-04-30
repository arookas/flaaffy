
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

	enum Action {

		Shock,
		Whap,
		Wave,
		Cotton,
		Jolt,

	}

	interface IPerformer {

		void LoadParams(string[] arguments);
		void Perform();

	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	class PerformerAttribute : Attribute {

		Action mAction;

		public Action Action { get { return mAction; } }

		public PerformerAttribute(Action action) {
			if (!action.IsDefined()) {
				throw new ArgumentOutOfRangeException("action");
			}

			mAction = action;
		}

		static Dictionary<Action, Type> sPerformers;

		public static void GatherPerformersInAssembly() {
			GatherPerformersInAssembly(Assembly.GetExecutingAssembly());
		}
		public static void GatherPerformersInAssembly(Assembly assembly) {
			if (sPerformers == null) {
				sPerformers = new Dictionary<Action, Type>(32);
			}

			var query =
				from type in assembly.GetTypes()
				where !type.IsAbstract
				where type.GetInterfaces().Contains(typeof(IPerformer)) // needs to implement IPerformer
				where type.GetConstructor(Type.EmptyTypes) != null // requires a parameterless constructor
				let attribute = type.GetCustomAttribute<PerformerAttribute>()
				where attribute != null
				let action = attribute.Action
				select new { action, type };

			foreach (var result in query) {
				sPerformers[result.action] = result.type;
			}
		}

		public static IPerformer CreatePerformer(Action action) {
			if (sPerformers == null) {
				GatherPerformersInAssembly();
			}

			Type type;

			if (!sPerformers.TryGetValue(action, out type)) {
				return null;
			}

			return (Activator.CreateInstance(type) as IPerformer);
		}

	}

	abstract class InputOutputPerformer : IPerformer {

		protected string mInputFile, mOutputFile;
		protected IOFormat mInputFormat, mOutputFormat;

		public virtual void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			var inputParam = mareep.GetLastCmdParam(cmdline, "-input");

			if (inputParam == null) {
				mareep.WriteError("Missing -input parameter.");
			}

			if (inputParam.Count != 2) {
				mareep.WriteError("-input parameter requires two arguments.");
			}

			mInputFile = inputParam[0];

			if (!File.Exists(mInputFile)) {
				mareep.WriteError("Input file \"{0}\" could not be found.", mInputFile);
			}

			if (!Enum.TryParse(inputParam[1], true, out mInputFormat)) {
				mareep.WriteError("Unknown input format \"{0}\".", inputParam[1]);
			}

			var outputParam = mareep.GetLastCmdParam(cmdline, "-output");

			if (outputParam == null) {
				mareep.WriteError("Missing -output parameter.");
			}

			if (outputParam.Count != 2) {
				mareep.WriteError("-output parameter requires two arguments.");
			}

			mOutputFile = outputParam[0];

			if (!Enum.TryParse(outputParam[1], true, out mOutputFormat)) {
				mareep.WriteError("Unknown output format \"{0}\".", outputParam[1]);
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

		static Action ReadAction(string[] arguments) {
			var cmdline = new aCommandLine(arguments);

			var param = mareep.GetLastCmdParam(cmdline, "-action");

			if (param == null) {
				mareep.WriteError("Missing -action parameter.");
			}

			Action action;

			if (!Enum.TryParse(param[0], true, out action)) {
				mareep.WriteError("Unknown action \"{0}\".", param[0]);
			}

			return action;
		}
		static IPerformer InitPerformer(Action action) {
			var performer = PerformerAttribute.CreatePerformer(action);

			if (performer == null) {
				mareep.WriteError("Unknown action \"{0}\".\n", action);
			}

			return performer;
		}

	}

}
