
using arookas.IO.Binary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace arookas.SequenceAssembler {

	[Performer(Action.Cotton)]
	class CottonPerformer : IPerformer {

		string mInput, mOutput;
		Endianness mEndianness;

		public void LoadParams(string[] arguments) {
			aCommandLineParameter param;
			var cmdline = new aCommandLine(arguments);

			param = mareep.GetLastCmdParam(cmdline, "-input");

			if (param == null) {
				mareep.WriteError("Missing -input parameter.");
			} else if (param.Count != 1) {
				mareep.WriteError("Bad -input parameter.");
			} else {
				mInput = param[0];
			}

			param = mareep.GetLastCmdParam(cmdline, "-output");

			if (param == null) {
				mareep.WriteError("Missing -output parameter.");
			} else if (param.Count != 1) {
				mareep.WriteError("Bad -output parameter.");
			} else {
				mOutput = param[0];
			}

			mEndianness = Endianness.Big;

			if (mareep.GetLastCmdParam(cmdline, "-le") != null) {
				mEndianness = Endianness.Little;
			}
		}

		public void Perform() {
			using (var instream = mareep.OpenFile(mInput)) {
				var reader = new StreamReader(instream, Encoding.UTF8);

				using (var outstream = mareep.CreateFile(mOutput)) {
					var writer = new aBinaryWriter(outstream, mEndianness, Encoding.GetEncoding(932));
					var asm = new BmsAssembler(reader, writer);

					while (asm.ReadLine());

					asm.Link();
				}
			}
		}

	}

	partial class BmsAssembler {

		int mLineNumber;
		aBinaryWriter mWriter;
		StreamReader mReader;
		Dictionary<string, int> mLabels;
		Dictionary<string, BmsImmediate> mVariables;
		List<Relocation> mRelocations;

		public BmsAssembler(StreamReader reader, aBinaryWriter writer) {
			mReader = reader;
			mWriter = writer;
			mLabels = new Dictionary<string, int>();
			mVariables = new Dictionary<string, BmsImmediate>();
			mRelocations = new List<Relocation>();
			mLineNumber = 1;
		}

		public bool ReadLine() {
			if (mReader.EndOfStream) {
				return false;
			}

			string word;
			var line = mReader.ReadLine();
			var cursor = 0;
			
			while (cursor < line.Length) {
				if (!GetNextWord(line, ref cursor, out word)) {
					break;
				}

				var type = GetWordType(word);

				if (type == BmsWordType.Comment) {
					break;
				}

				switch (type) {
					case BmsWordType.Directive: ReadDirective(word, line, ref cursor); break;
					case BmsWordType.Command: ReadCommand(word, line, ref cursor); break;
					case BmsWordType.LabelDefinition: ReadLabelDefinition(word, line, ref cursor); break;
				}
			}

			++mLineNumber;
			return true;
		}
		void ReadDirective(string word, string line, ref int cursor) {
			switch (word) {
				case ".define": ReadDefineDirective(line, ref cursor); break;
				case ".align": ReadAlignDirective(line, ref cursor); break;
				case ".int8": ReadPodDirective(word, BmsArgumentType.Int8, line, ref cursor); break;
				case ".int16": ReadPodDirective(word, BmsArgumentType.Int16, line, ref cursor); break;
				case ".int24": ReadPodDirective(word, BmsArgumentType.Int24, line, ref cursor); break;
				case ".int32": ReadPodDirective(word, BmsArgumentType.Int32, line, ref cursor); break;
				default: Warning("unknown directive '{0}'.", word); break;
			}
		}
		void ReadDefineDirective(string line, ref int cursor) {
			string name;

			if (!GetNextWord(line, ref cursor, out name)) {
				Warning("missing variable name in .define.");
				return;
			} else if (GetWordType(name) != BmsWordType.VariableName) {
				Warning("bad variable name '{0}' in .define.", name);
				return;
			}

			string immediate;

			if (!GetNextWord(line, ref cursor, out immediate)) {
				Warning("missing value in .define.");
				return;
			} else if (GetWordType(immediate) != BmsWordType.Immediate) {
				Warning("bad variable value '{0}' in .define.", immediate);
				return;
			}

			DefineVariable(name, immediate);
		}
		void ReadPodDirective(string word, BmsArgumentType podtype, string line, ref int cursor) {
			string pod;

			if (!GetNextWord(line, ref cursor, out pod)) {
				Warning("missing value in {0}.", word);
				return;
			}

			var type = GetWordType(pod);

			if (type == BmsWordType.Immediate) {
				var immediate = new BmsImmediate(pod);
				immediate.Write(this, mWriter, podtype);
			} else if (type == BmsWordType.LabelReference) {
				if (podtype != BmsArgumentType.Int24) {
					Error("writing a label as POD requires 24-bit.");
				}

				var reference = ReadLabelReference(pod);
				reference.Write(this, mWriter);
			} else if (type == BmsWordType.VariableName) {
				if (!IsVariableDefined(pod)) {
					Error("undefined variable '{0}'.", pod);
				}

				mVariables[pod].Write(this, mWriter, podtype);
			} else {
				Error("bad argument '{0}'.", pod);
			}
		}
		void ReadAlignDirective(string line, ref int cursor) {
			string word;

			if (!GetNextWord(line, ref cursor, out word)) {
				Warning("missing value in .align.");
				return;
			}

			var alignment = 0;
			var type = GetWordType(word);

			if (type == BmsWordType.Immediate) {
				alignment = new BmsImmediate(word).Value;
			} else if (type == BmsWordType.VariableName) {
				if (!IsVariableDefined(word)) {
					Error("undefined variable '{0}'.", word);
				}

				alignment = mVariables[word].Value;
			} else {
				Error("bad argument '{0}'.", word);
			}

			if (alignment < 0) {
				Error("bad argument '{0}'.", word);
			}

			mWriter.WritePadding(alignment, 0);
		}
		void ReadCommand(string word, string line, ref int cursor) {
			ReadCommand(word, ReadArguments(line, ref cursor));
		}
		void ReadLabelDefinition(string word, string line, ref int cursor) {
			var symbol = word.Substring(0, (word.Length - 1)); // remove the :
			DefineLabel(symbol);
		}

		bool IsRegisterName(string word) {
			return (TryGetRegisterIndex(word) >= 0);
		}
		int GetRegisterIndex(string word) {
			var index = TryGetRegisterIndex(word);

			if (index < 0) {
				Error("unknown register '{0}'.", word);
			}

			return index;
		}
		int TryGetRegisterIndex(string word) {
			switch (word) {
				case "rcmp": return 3;
				case "rx": return 4;
				case "ry": return 5;
				case "rpreset": return 6;
				case "rpitch": return 7;

				case "rbank": return 32;
				case "rprogram": return 33;
				case "rxy": return 35;

				case "rar0": return 40;
				case "rar1": return 41;
				case "rar2": return 42;
				case "rar3": return 43;
				case "rchild": return 44;
				case "rchannel": return 45;
				case "rloop": return 48;
			}

			int index;

			if (!Int32.TryParse(word.Substring(1), NumberStyles.None, null, out index)) {
				return -1;
			}

			if ((index > 13 && index < 32) || (index > 35 && index < 40) || (index > 48 && index < 64) || index > 79) {
				return -1;
			}

			return index;
		}

		bool IsConditionName(string word) {
			return (TryGetConditionIndex(word) >= 0);
		}
		int GetConditionIndex(string word) {
			var index = TryGetConditionIndex(word);

			if (index < 0) {
				Error("unknown condition identifier '{0}'.", word);
			}

			return index;
		}
		int TryGetConditionIndex(string word) {
			switch (word) {
				case "eq": return 1;
				case "ne": return 2;
				case "one": return 3;
				case "le": return 4;
				case "gt": return 5;
			}

			return -1;
		}

		bool GetNextWord(string line, ref int cursor, out string word) {
			if (cursor >= line.Length) {
				word = null;
				return false;
			}
			while (cursor < line.Length && Char.IsWhiteSpace(line[cursor])) {
				++cursor;
			}
			var start = cursor;
			if (cursor >= line.Length) {
				word = null;
				return false;
			}
			if (line[cursor] == ',') {
				word = ",";
				++cursor;
				return true;
			}
			while (cursor < line.Length && !Char.IsWhiteSpace(line[cursor]) && line[cursor] != ',') {
				++cursor;
			}
			var length = (cursor - start);
			if (length == 0) {
				word = null;
				return false;
			}
			word = line.Substring(start, length);
			return true;
		}
		BmsWordType GetWordType(string word) {
			if (word[0] == '#') {
				return BmsWordType.Comment;
			}

			if (word == ",") {
				return BmsWordType.ArgumentSeparator;
			}

			if (Regex.IsMatch(word, @"^\.[a-z][a-z0-9]*$")) {
				return BmsWordType.Directive;
			}

			if (Regex.IsMatch(word, @"^[A-Z][A-Z0-9_]*:$")) {
				return BmsWordType.LabelDefinition;
			}

			if (Regex.IsMatch(word, @"^[ABCDEFG][#-b][0-9]+$")) {
				return BmsWordType.KeyNumber;
			}

			if (Regex.IsMatch(word, @"^[A-Z][A-Z0-9_]*$")) {
				return BmsWordType.VariableName;
			}

			if (Regex.IsMatch(word, @"^-?\$?[0-9]+[bBsShH]?$")) {
				return BmsWordType.Immediate;
			}

			if (IsRegisterName(word)) {
				return BmsWordType.RegisterReference;
			}

			if (Regex.IsMatch(word, @"^\[r[a-z0-9]+\]$")) {
				return BmsWordType.RegisterDereference;
			}

			if (Regex.IsMatch(word, @"^@[A-Z][A-Z0-9_]*$")) {
				return BmsWordType.LabelReference;
			}

			if (IsConditionName(word)) {
				return BmsWordType.Condition;
			}

			if (Regex.IsMatch(word, @"^[a-z]+$")) {
				return BmsWordType.Command;
			}

			return BmsWordType.Unknown;
		}

		public void Warning(string message) {
			Warning("{0}", message);
		}
		public void Warning(string format, params object[] arguments) {
			var message = String.Format(format, arguments);
			mareep.WriteWarning("BMS: line {0}: {1}\n", mLineNumber, message);
		}
		public void Error(string message) {
			Error("{0}", message);
		}
		public void Error(string format, params object[] arguments) {
			var message = String.Format(format, arguments);
			mareep.WriteError("BMS: line {0}: {1}\n", mLineNumber, message);
		}

		public void AddRelocation(string symbol) {
			var relocation = new Relocation();
			relocation.offset = (int)mWriter.Position;
			relocation.symbol = symbol;
			mRelocations.Add(relocation);
		}
		public void Link() {
			mWriter.Keep();

			for (var i = mRelocations.Count - 1; i >= 0; --i) {
				var relocation = mRelocations[i];

				if (!IsLabelDefined(relocation.symbol)) {
					continue;
				}

				mWriter.Goto(relocation.offset);
				mWriter.Write24(mLabels[relocation.symbol]);
				mRelocations.RemoveAt(i);
			}

			mWriter.Back();

			if (mRelocations.Count > 0) {
				foreach (var relocation in mRelocations) {
					mareep.WriteMessage("  {0}\n", relocation.symbol);
				}
				mareep.WriteError("Failed to link {0} symbol(s).", mRelocations.Count);
			}
		}

		public void DefineLabel(string label) {
			mLabels[label] = (int)mWriter.Position;
		}
		public bool IsLabelDefined(string name) {
			return mLabels.ContainsKey(name);
		}

		public void DefineVariable(string name, int value) {
			mVariables[name] = new BmsImmediate(value);
		}
		public void DefineVariable(string name, BmsArgumentType type, int value) {
			mVariables[name] = new BmsImmediate(type, value);
		}
		public void DefineVariable(string name, string input) {
			mVariables[name] = new BmsImmediate(input);
		}
		public bool IsVariableDefined(string name) {
			return mVariables.ContainsKey(name);
		}

		struct Relocation {

			public int offset;
			public string symbol;

		}

	}

	partial class BmsAssembler {

		static readonly int[] srKeyBases = new int[7] { 0, 2, 4, 5, 7, 9, 11, };

		BmsArgument[] ReadArguments(string line, ref int cursor) {
			var arguments = new List<BmsArgument>(8);

			string word;
			var separator = false;

			for (;;) {

				if (!GetNextWord(line, ref cursor, out word)) {
					break;
				}

				var type = GetWordType(word);

				if (type == BmsWordType.Comment) {
					break;
				} else if (type == BmsWordType.ArgumentSeparator) {
					if (separator || arguments.Count == 0) {
						Error("empty argument.");
					}
					separator = true;
					continue;
				}

				separator = false;
				BmsArgument argument = null;

				switch (type) {
					case BmsWordType.Immediate: argument = ReadImmediate(word); break;
					case BmsWordType.KeyNumber: argument = ReadKeyNumber(word); break;
					case BmsWordType.Condition: argument = ReadCondition(word); break;
					case BmsWordType.VariableName: argument = ReadVariable(word); break;
					case BmsWordType.LabelReference: argument = ReadLabelReference(word); break;
					case BmsWordType.RegisterReference: argument = ReadRegisterReference(word); break;
					case BmsWordType.RegisterDereference: argument = ReadRegisterDereference(word); break;
				}

				if (argument == null) {
					Error("bad argument '{0}'.", word);
				}

				arguments.Add(argument);
			}

			if (separator) {
				Error("empty argument.");
			}

			return arguments.ToArray();
		}
		BmsImmediate ReadImmediate(string word) {
			return new BmsImmediate(word);
		}
		BmsImmediate ReadKeyNumber(string word) {
			var keynumber = mareep.ConvertKey(word);

			if (keynumber < 0 || keynumber > 127) {
				Error("key '{0}' is out of 0-127 range.", word);
			}

			return new BmsImmediate(BmsArgumentType.Int8, keynumber);
		}
		BmsImmediate ReadCondition(string word) {
			return new BmsImmediate(BmsArgumentType.Int8, GetConditionIndex(word));
		}
		BmsImmediate ReadVariable(string word) {
			if (!IsVariableDefined(word)) {
				Error("undefined variable '{0}'.", word);
			}

			var variable = mVariables[word];
			return new BmsImmediate(variable.Type, variable.Value);
		}
		BmsImmediate ReadRegisterReference(string word) {
			var index = GetRegisterIndex(word);

			return new BmsImmediate(BmsArgumentType.Int8, index);
		}
		BmsRegisterDereference ReadRegisterDereference(string word) {
			var index = GetRegisterIndex(word.Substring(1, word.Length - 2));
			return new BmsRegisterDereference(index);
		}
		BmsLabelReference ReadLabelReference(string word) {
			var symbol = word.Substring(1); // remove @ symbol
			if (IsLabelDefined(symbol)) {
				return new BmsLabelReference(symbol, mLabels[symbol]);
			}
			return new BmsLabelReference(symbol);
		}

		void EnsureArgumentCount(BmsArgument[] arguments, int count) {
			EnsureArgumentCount(arguments, count, count);
		}
		void EnsureArgumentCount(BmsArgument[] arguments, int minimum, int maximum) {
			if (arguments.Length < minimum) {
				Error("not enough arguments.");
			} else if (arguments.Length > maximum) {
				Error("too many arguments.");
			}
		}
		void EnsureArgumentType(BmsArgument[] arguments, int index, params BmsArgumentType[] types) {
			if (index < 0 || index >= arguments.Length) {
				return;
			}

			foreach (var type in types) {
				if (arguments[index].Type == type) {
					return;
				}
			}

			var immediate = (arguments[index] as BmsImmediate);

			if (immediate != null && types.Any(type => type >= BmsArgumentType.Int8 && type <= BmsArgumentType.Int32)) {
				immediate.SetType(types.Last(type => type >= BmsArgumentType.Int8 && type <= BmsArgumentType.Int32));
				return;
			}

			Error("bad type for argument {0}.", index);
		}

		bool GenerateCommandOverrides(out byte opcode, out byte flags, BmsArgument[] arguments, params int[] ignores) {
			opcode = (byte)(0xB0 + arguments.Length);
			flags = 0;

			for (var i = 0; i < arguments.Length; ++i) {
				if (ignores.Contains(i)) {
					continue;
				}

				if (arguments[i].Type != BmsArgumentType.RegisterDereference) {
					continue;
				}

				flags |= (byte)(0x80 >> i);
			}

			return (flags != 0);
		}

		void ReadCommand(string command, BmsArgument[] arguments) {
			switch (command) {
				case "noteon": ReadNoteOnCommand(command, arguments); break;
				case "notefon": ReadNoteOnCommand(command, arguments); break;
				case "notesweep": ReadNoteOnCommand(command, arguments); break;
				case "notefsweep": ReadNoteOnCommand(command, arguments); break;
				case "noteoff": ReadNoteOffCommand(command, arguments); break;
				case "wait": ReadWaitCommand(command, arguments); break;
				case "timedparam": ReadTimedParamCommand(command, arguments); break;
				case "load": ReadLoadCommand(command, arguments); break;
				case "add": ReadAddCommand(command, arguments); break;
				case "multiply": ReadMultiplyCommand(command, arguments); break;
				case "compare": ReadCompareCommand(command, arguments); break;
				case "subtract": ReadSubtractCommand(command, arguments); break;
				case "bshift": ReadBitwiseCommand(command, arguments); break;
				case "bshiftu": ReadBitwiseCommand(command, arguments); break;
				case "band": ReadBitwiseCommand(command, arguments); break;
				case "bor": ReadBitwiseCommand(command, arguments); break;
				case "bxor": ReadBitwiseCommand(command, arguments); break;
				case "negate": ReadBitwiseCommand(command, arguments); break;
				case "random": ReadBitwiseCommand(command, arguments); break;
				case "opentrack": ReadOpenTrackCommand(command, arguments); break;
				case "opentrackbros": ReadOpenTrackCommand(command, arguments); break;
				case "call": ReadCallCommand(command, arguments); break;
				case "ret": ReadRetCommand(command, arguments); break;
				case "jmp": ReadCallCommand(command, arguments); break;
				case "loops": ReadSimple16Command(command, arguments); break;
				case "loope": ReadSimpleCommand(command, arguments); break;
				case "readport": ReadReadPortCommand(command, arguments); break;
				case "writeport": ReadWritePortCommand(command, arguments); break;
				case "checkportimport": ReadSimple8Command(command, arguments); break;
				case "checkportexport": ReadSimple8Command(command, arguments); break;
				case "connectname": ReadConnectNameCommand(command, arguments); break;
				case "parentwriteport": ReadParentWritePortCommand(command, arguments); break;
				case "childwriteport": ReadParentWritePortCommand(command, arguments); break;
				case "setlastnote": ReadSimple8Command(command, arguments); break;
				case "timerelate": ReadSimple8Command(command, arguments); break;
				case "simpleosc": ReadSimple8Command(command, arguments); break;
				case "simpleenv": ReadSimple8Command(command, arguments); break;
				case "simpleadsr": ReadSimpleAdsrCommand(command, arguments); break;
				case "transpose": ReadSimple8Command(command, arguments); break;
				case "closetrack": ReadSimple8Command(command, arguments); break;
				case "outswitch": ReadSimple8Command(command, arguments); break;
				case "updatesync": ReadSimple16Command(command, arguments); break;
				case "busconnect": ReadBusConnectCommand(command, arguments); break;
				case "pausestatus": ReadSimple8Command(command, arguments); break;
				case "setinterrupt": ReadSetInterruptCommand(command, arguments); break;
				case "disinterrupt": ReadSimple8Command(command, arguments); break;
				case "clri": ReadSimpleCommand(command, arguments); break;
				case "seti": ReadSimpleCommand(command, arguments); break;
				case "reti": ReadSimpleCommand(command, arguments); break;
				case "inttimer": ReadIntTimerCommand(command, arguments); break;
				case "connectopen": ReadSimpleCommand(command, arguments); break;
				case "connectclose": ReadSimpleCommand(command, arguments); break;
				case "synccpu": ReadSimple16Command(command, arguments); break;
				case "flushall": ReadSimpleCommand(command, arguments); break;
				case "flushrelease": ReadSimpleCommand(command, arguments); break;
				case "panpowset": ReadPanPowSetCommand(command, arguments); break;
				case "iirset": ReadIirSetCommand(command, arguments); break;
				case "firset": ReadSimpleOffsetCommand(command, arguments); break;
				case "extset": ReadSimpleOffsetCommand(command, arguments); break;
				case "panswset": ReadPanPowSetCommand(command, arguments); break;
				case "oscroute": ReadSimple8Command(command, arguments); break;
				case "iircutoff": ReadSimple8Command(command, arguments); break;
				case "oscfull": ReadOscFullCommand(command, arguments); break;
				case "volumemode": ReadSimple8Command(command, arguments); break;
				case "checkwave": ReadSimple16Command(command, arguments); break;
				case "printf": ReadPrintfCommand(command, arguments); break;
				case "nop": ReadSimpleCommand(command, arguments); break;
				case "tempo": ReadSimple16Command(command, arguments); break;
				case "timebase": ReadSimple16Command(command, arguments); break;
				case "finish": ReadSimpleCommand(command, arguments); break;
				default: Error("unknown command '{0}'.", command); break;
			}
		}
		void ReadNoteOnCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 3);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte flags = 0;

			if (arguments[0].Type == BmsArgumentType.RegisterDereference) {
				flags |= 0x80;
			}

			switch (command) {
				case "notefon": flags |= 0x20; break;
				case "notesweep": flags |= 0x40; break;
				case "notefsweep": flags |= 0x60; break;
			}

			if (arguments[1].Type == BmsArgumentType.RegisterDereference) {
				var dereference = (arguments[1] as BmsRegisterDereference);
				dereference.Bitflag = true;
			}

			if (arguments[2].Type == BmsArgumentType.RegisterDereference) {
				var dereference = (arguments[2] as BmsRegisterDereference);

				if (dereference < 0 || dereference > 6) {
					Error("register index {0} out of bounds.", dereference.RegisterIndex);
				}

				flags |= 0x18;
				flags |= (byte)((dereference + 1) & 0x7);
			} else {
				var immediate = (arguments[2] as BmsImmediate);

				if (immediate < 1 || immediate > 7) {
					Error("channel index must be 1-7.");
				}

				flags |= ((byte)(immediate & 0x7));
			}

			arguments[0].Write(this, mWriter);
			mWriter.Write8(flags);
			arguments[1].Write(this, mWriter);
		}
		void ReadNoteOffCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			if (arguments.Length > 1) {
				EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.Int16);
			}

			if (arguments[0].Type == BmsArgumentType.RegisterDereference) {
				mWriter.Write8(0xF9);

				var dereference = (arguments[0] as BmsRegisterDereference);

				if (dereference.RegisterIndex < 0 || dereference.RegisterIndex > 7) {
					Error("register index must be 0-7.");
				}

				var flags = (byte)(dereference.RegisterIndex & 0x7);

				if (arguments.Length > 1) {
					flags |= 0x80;
				}

				mWriter.Write8(flags);
			} else {
				byte opcode = 0x80;

				var immediate = (arguments[0] as BmsImmediate);

				if (immediate < 1 || immediate > 7) {
					Error("channel index must be 1-7.");
				}

				opcode |= (byte)(immediate & 0x7);

				if (arguments.Length > 1) {
					opcode |= 0x8;
				}

				mWriter.Write8(opcode);
			}

			if (arguments.Length > 1) {
				var immediate = (arguments[1] as BmsImmediate);

				if (immediate > 100) {
					immediate.SetValue(BmsArgumentType.Int8, (103 + immediate / 20));
				}

				immediate.Write(this, mWriter, BmsArgumentType.Int8);
			}
		}
		void ReadWaitCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.Int16, BmsArgumentType.Int24, BmsArgumentType.RegisterDereference);

			byte opcode = 0;

			switch (arguments[0].Type) {
				case BmsArgumentType.Int8: opcode = 0x80; break;
				case BmsArgumentType.Int16: opcode = 0x88; break;
				case BmsArgumentType.Int24: opcode = 0xEA; break;
				case BmsArgumentType.RegisterDereference: opcode = 0xCF; break;
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadTimedParamCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2, 3);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.Half16, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			if (arguments.Length > 2) {
				EnsureArgumentType(arguments, 2, BmsArgumentType.Int8, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			}

			byte opcode = 0x90;

			switch (arguments[1].Type) {
				case BmsArgumentType.Int8: opcode |= 0x4; break;
				case BmsArgumentType.Half16: opcode |= 0x8; break;
				case BmsArgumentType.Int16: opcode |= 0xC; break;
			}

			if (arguments.Length > 2) {
				switch (arguments[2].Type) {
					case BmsArgumentType.RegisterDereference: opcode |= 0x1; break;
					case BmsArgumentType.Int8: opcode |= 0x2; break;
					case BmsArgumentType.Int16: opcode |= 0x3; break;
				}
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadLoadCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.Half16, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode = 0xA0;

			switch (arguments[1].Type) {
				case BmsArgumentType.Int8: opcode |= 0x4; break;
				case BmsArgumentType.Half16: opcode |= 0x8; break;
				case BmsArgumentType.Int16: opcode |= 0xC; break;
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadAddCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference, BmsArgumentType.Int8, BmsArgumentType.Int16);

			byte opcode = 0xA1;

			switch (arguments[1].Type) {
				case BmsArgumentType.Int8: opcode |= 0x4; break;
				case BmsArgumentType.Int16: opcode |= 0xC; break;
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadMultiplyCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference, BmsArgumentType.Int8, BmsArgumentType.Int16);

			byte opcode = 0xA2;

			switch (arguments[1].Type) {
				case BmsArgumentType.Int8: opcode |= 0x4; break;
				case BmsArgumentType.Int16: opcode |= 0xC; break;
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadCompareCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference, BmsArgumentType.Int8, BmsArgumentType.Int16);

			byte opcode = 0xA3;

			switch (arguments[1].Type) {
				case BmsArgumentType.Int8: opcode |= 0x4; break;
				case BmsArgumentType.Int16: opcode |= 0xC; break;
			}

			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadTableCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
		}
		void ReadSubtractCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8);

			mWriter.Write8(0xAB);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadBitwiseCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode = 0;

			switch (command) {
				case "bshift": opcode = 0x10; break;
				case "bshiftu": opcode = 0x20; break;
				case "band": opcode = 0x30; break;
				case "bor": opcode = 0x40; break;
				case "bxor": opcode = 0x50; break;
				case "negate": EnsureArgumentCount(arguments, 1); opcode = 0x60; break;
				case "random": opcode = 0x90; break;
			}

			if (arguments.Length > 1) {
				switch (arguments[1].Type) {
					case BmsArgumentType.Int8: opcode |= 0x4; break;
					case BmsArgumentType.Int16: opcode |= 0xC; break;
				}
			} else {
				opcode |= 0x8;
			}

			mWriter.Write8(0xA9);
			mWriter.Write8(opcode);

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadOpenTrackCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xC1);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xC1);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadCallCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1, 3);
			EnsureArgumentType(arguments, (arguments.Length - 1), BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);

			byte opcode = 0;

			switch (command) {
				case "call": opcode = 0xC4; break;
				case "jmp": opcode = 0xC8; break;
			}

			mWriter.Write8(opcode);
			
			byte flags = 0;

			if (arguments.Length == 1) {
				if (arguments[0].Type == BmsArgumentType.RegisterDereference) {
					flags |= 0x80;
				}
			} else if (arguments.Length == 2) {
				if (arguments[0].Type == BmsArgumentType.RegisterDereference) {
					flags |= 0xC0;

					if (arguments[1].Type == BmsArgumentType.RegisterDereference) {
						flags |= 0x20;
					}
				} else {
					EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
					var immediate = (arguments[0] as BmsImmediate);
					flags |= (byte)(immediate & 0xF);

					if (arguments[1].Type == BmsArgumentType.RegisterDereference) {
						flags |= 0x80;
					}
				}
			} else if (arguments.Length == 3) {
				EnsureArgumentType(arguments, 0, BmsArgumentType.Int8);
				EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference);
				EnsureArgumentType(arguments, 2, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);

				flags |= 0xC0;

				var immediate = (arguments[0] as BmsImmediate);
				flags |= (byte)(immediate & 0xF);

				if (arguments[2].Type == BmsArgumentType.RegisterDereference) {
					flags |= 0x20;
				}
			}

			mWriter.Write8(flags);

			foreach (var argument in arguments.Skip((flags & 0xF) > 0 ? 1 : 0)) {
				argument.Write(this, mWriter);
			}
		}
		void ReadRetCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 0, 1);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xC6);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xC6);
			}

			if (arguments.Length == 0) {
				mWriter.Write8(0);
			} else {
				arguments[0].Write(this, mWriter);
			}
		}
		void ReadReadPortCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xCB);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xCB);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadWritePortCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments, 1)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xCC);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xCC);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadConnectNameCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1, 2);

			byte opcode = 0, flags = 0;
			var overrides = false;

			if (arguments.Length == 1) {
				EnsureArgumentType(arguments, 0, BmsArgumentType.Int32);
			} else {
				EnsureArgumentType(arguments, 0, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
				EnsureArgumentType(arguments, 1, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
				overrides = GenerateCommandOverrides(out opcode, out flags, arguments);
			}

			if (overrides) {
				mWriter.Write8(opcode);
			}

			mWriter.Write8(0xD0);

			if (overrides) {
				mWriter.Write8(flags);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadParentWritePortCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.RegisterDereference);

			byte opcode, flags;
			var overrides = GenerateCommandOverrides(out opcode, out flags, arguments, 1);

			if (overrides) {
				mWriter.Write8(opcode);
			}

			switch (command) {
				case "parentwriteport": opcode = 0xD1; break;
				case "childwriteport": opcode = 0xD2; break;
			}

			mWriter.Write8(opcode);

			if (overrides) {
				mWriter.Write8(flags);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadSimpleAdsrCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 5);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 3, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 3, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xD8);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xD8);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadBusConnectCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xDD);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xDD);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadSetInterruptCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xDF);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xDF);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadIntTimerCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 2);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xE4);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xE4);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadPanPowSetCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 5);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 3, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 4, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xEB);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xEB);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadIirSetCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 4);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 3, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xEC);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xEC);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadPanSwSetCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 3);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xEF);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xEF);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadOscFullCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 3);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 1, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);
			EnsureArgumentType(arguments, 2, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);
			
			byte opcode, flags;

			if (GenerateCommandOverrides(out opcode, out flags, arguments)) {
				mWriter.Write8(opcode);
				mWriter.Write8(0xF2);
				mWriter.Write8(flags);
			} else {
				mWriter.Write8(0xF2);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadPrintfCommand(string command, BmsArgument[] arguments) {
			// TODO
		}
		void ReadSimpleCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 0);

			byte opcode = 0;

			switch (command) {
				case "loope": opcode = 0xCA; break;
				case "clri": opcode = 0xE1; break;
				case "seti": opcode = 0xE2; break;
				case "reti": opcode = 0xE3; break;
				case "connectopen": opcode = 0xE5; break;
				case "connectclose": opcode = 0xE6; break;
				case "flushall": opcode = 0xE8; break;
				case "flushrelease": opcode = 0xE9; break;
				case "nop": opcode = 0xFC; break;
				case "finish": opcode = 0xFF; break;
			}

			mWriter.Write8(opcode);
		}
		void ReadSimple8Command(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int8, BmsArgumentType.RegisterDereference);

			byte opcode, flags;
			var overrides = GenerateCommandOverrides(out opcode, out flags, arguments);

			if (overrides) {
				mWriter.Write8(opcode);
			}

			switch (command) {
				case "checkportimport": opcode = 0xCD; break;
				case "checkportexport": opcode = 0xCE; break;
				case "setlastnote": opcode = 0xD4; break;
				case "timerelate": opcode = 0xD5; break;
				case "simpleosc": opcode = 0xD6; break;
				case "simpleenv": opcode = 0xD7; break;
				case "transpose": opcode = 0xD9; break;
				case "closetrack": opcode = 0xDA; break;
				case "outswitch": opcode = 0xDB; break;
				case "pausestatus": opcode = 0xDE; break;
				case "disinterrupt": opcode = 0xE0; break;
				case "oscroute": opcode = 0xF0; break;
				case "iircutof": opcode = 0xF1; break;
				case "volumemode": opcode = 0xF3; break;
			}

			mWriter.Write8(opcode);

			if (overrides) {
				mWriter.Write8(flags);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadSimple16Command(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int16, BmsArgumentType.RegisterDereference);

			byte opcode, flags;
			var overrides = GenerateCommandOverrides(out opcode, out flags, arguments);

			if (overrides) {
				mWriter.Write8(opcode);
			}

			switch (command) {
				case "loops": opcode = 0xC9; break;
				case "updatesync": opcode = 0xDC; break;
				case "synccpu": opcode = 0xE7; break;
				case "checkwave": opcode = 0xFA; break;
				case "tempo": opcode = 0xFD; break;
				case "timebase": opcode = 0xFE; break;
			}

			mWriter.Write8(opcode);

			if (overrides) {
				mWriter.Write8(flags);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		void ReadSimpleOffsetCommand(string command, BmsArgument[] arguments) {
			EnsureArgumentCount(arguments, 1);
			EnsureArgumentType(arguments, 0, BmsArgumentType.Int24, BmsArgumentType.LabelReference, BmsArgumentType.RegisterDereference);

			byte opcode, flags;
			var overrides = GenerateCommandOverrides(out opcode, out flags, arguments);

			if (overrides) {
				mWriter.Write8(opcode);
			}

			switch (command) {
				case "firset": opcode = 0xED; break;
				case "extset": opcode = 0xEE; break;
			}

			mWriter.Write8(opcode);

			if (overrides) {
				mWriter.Write8(flags);
			}

			foreach (var argument in arguments) {
				argument.Write(this, mWriter);
			}
		}
		
	}

	enum BmsWordType {

		Unknown,
		Comment,
		Directive,
		LabelDefinition,
		Command,
		ArgumentSeparator,
		Immediate,
		KeyNumber,
		Condition,
		VariableName,
		LabelReference,
		RegisterReference,
		RegisterDereference,
		StringLiteral,

	}

	enum BmsArgumentType {

		Int8,
		Half16,
		Int16,
		Int24,
		Int32,
		LabelReference,
		RegisterDereference,
		StringLiteral,

	}

	abstract class BmsArgument {

		public abstract BmsArgumentType Type { get; }

		public void Write(BmsAssembler asm, aBinaryWriter writer) {
			Write(asm, writer, Type);
		}
		public abstract void Write(BmsAssembler asm, aBinaryWriter writer, BmsArgumentType type);

	}

	class BmsImmediate : BmsArgument {

		BmsArgumentType mType;
		int mValue;

		public override BmsArgumentType Type { get { return mType; } }
		public int Value { get { return mValue; } }

		public BmsImmediate() : this(BmsArgumentType.Int16, 0) { }
		public BmsImmediate(int value) {
			mValue = value;
			FindBestType(value, out mType);
		}
		public BmsImmediate(BmsArgumentType type, int value) {
			SetValue(type, value);
		}
		public BmsImmediate(string input) {
			ParseValue(input, out mType, out mValue);
		}

		void FindBestType(int value, out BmsArgumentType type) {
			if ((value >= 0 && value <= 255) || (value >= -128 && value <= 127)) {
				type = BmsArgumentType.Int8;
			} else if ((value >= -32768 && value < 0 && (value % 256) == 0) || (value >= 0 && value <= 32766 && (value % 258) == 0)) {
				type = BmsArgumentType.Half16;
			} else if ((value >= 0 && value <= 65535) || (value >= -32768 && value <= 32767)) {
				type = BmsArgumentType.Int16;
			} else if ((value >= 0 && value <= 16777215) || (value >= -8388608 && value <= 8388607)) {
				type = BmsArgumentType.Int24;
			} else {
				type = BmsArgumentType.Int32;
			}
		}

		public void ParseValue(string input, out BmsArgumentType type, out int value) {
			var negative = false;
			var hexadecimal = false;
			var i = 0;

			if (input[i] == '-') {
				negative = true;
				++i;
			}

			if (input[i] == '$') {
				hexadecimal = true;
				++i;
			}

			value = 0;
			if (hexadecimal) {
				while (i < input.Length && ((input[i] >= '0' && input[i] <= '9') || (input[i] >= 'A' && input[i] <= 'F') || (input[i] >= 'a' && input[i] <= 'f'))) {
					value *= 16;
					if (input[i] >= 'a') {
						value += (10 + (input[i] - 'a'));
					} else if (input[i] >= 'A') {
						value += (10 + (input[i] - 'A'));
					} else {
						value += (input[i] - '0');
					}
					++i;
				}
			} else {
				while (i < input.Length && input[i] >= '0' && input[i] <= '9') {
					value = (value * 10 + (input[i] - '0'));
					++i;
				}
			}

			if (negative) {
				value = -value;
			}

			if (i < input.Length) {
				switch (input[i]) {
					case 'b':
					case 'B': {
						type = BmsArgumentType.Int8;
						break;
					}
					case 's':
					case 'S': {
						type = BmsArgumentType.Half16;
						value *= (value < 0 ? 256 : 258);
						break;
					}
					case 'h':
					case 'H': {
						type = BmsArgumentType.Int16;
						break;
					}
					case 'q':
					case 'Q': {
						type = BmsArgumentType.Int24;
						break;
					}
					case 'w':
					case 'W': {
						type = BmsArgumentType.Int32;
						break;
					}
					default: FindBestType(value, out type); break;
				}
			} else {
				FindBestType(value, out type);
			}
		}
		public void SetType(BmsArgumentType type) {
			if (type < BmsArgumentType.Int8 || type > BmsArgumentType.Int32) {
				throw new ArgumentOutOfRangeException("type");
			}
			
			mType = type;
		}
		public void SetValue(BmsArgumentType type, int value) {
			if (type < BmsArgumentType.Int8 || type > BmsArgumentType.Int32) {
				throw new ArgumentOutOfRangeException("type");
			}

			mType = type;
			mValue = value;
		}

		public override void Write(BmsAssembler asm, aBinaryWriter writer, BmsArgumentType type) {
			switch (type) {
				case BmsArgumentType.Int8: writer.Write8((byte)(mValue & 0xFF)); break;
				case BmsArgumentType.Half16: writer.WriteS8((sbyte)(mValue / (mValue < 0 ? 256 : 258))); break;
				case BmsArgumentType.Int16: writer.Write16((ushort)(mValue & 0xFFFF)); break;
				case BmsArgumentType.Int24: writer.Write24(mValue); break;
				case BmsArgumentType.Int32: writer.WriteS32(mValue); break;
			}
		}

		public static implicit operator int(BmsImmediate immediate) {
			return immediate.mValue;
		}

	}

	class BmsLabelReference : BmsArgument {

		bool mRelocation;
		string mSymbol;
		int mOffset;

		public override BmsArgumentType Type { get { return BmsArgumentType.LabelReference; } }
		public string Symbol { get { return mSymbol; } }
		public int Offset { get { return mOffset; } }

		public BmsLabelReference(string symbol)
			: this(symbol, 0) {
			mRelocation = true;
		}
		public BmsLabelReference(string symbol, int offset) {
			mSymbol = symbol;
			mOffset = offset;
		}

		public override void Write(BmsAssembler asm, aBinaryWriter writer, BmsArgumentType type) {
			if (type != BmsArgumentType.LabelReference) {
				return;
			}

			if (mRelocation) {
				asm.AddRelocation(mSymbol);
				writer.Write24(0); // dummy relocation info
			} else {
				writer.Write24(mOffset);
			}
		}

	}

	class BmsRegisterDereference : BmsArgument {

		bool mBitflag;
		int mRegisterIndex;

		public override BmsArgumentType Type {
			get { return BmsArgumentType.RegisterDereference; }
		}

		public bool Bitflag {
			get { return mBitflag; }
			set { mBitflag = value; }
		}
		public int RegisterIndex {
			get { return mRegisterIndex; }
			set { mRegisterIndex = value; }
		}

		public BmsRegisterDereference() : this(0) { }
		public BmsRegisterDereference(int index) : this(index, false) { }
		public BmsRegisterDereference(int index, bool bitflag) {
			mRegisterIndex = index;
			mBitflag = bitflag;
		}

		public override void Write(BmsAssembler asm, aBinaryWriter writer, BmsArgumentType type) {
			if (type != BmsArgumentType.RegisterDereference) {
				return;
			}

			var value = (byte)(mRegisterIndex & 0x7F);

			if (mBitflag) {
				value |= 0x80;
			}

			writer.Write8(value);
		}

		public static implicit operator int(BmsRegisterDereference dereference) {
			return dereference.mRegisterIndex;
		}

	}

}
