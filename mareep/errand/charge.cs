
using arookas.IO.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace arookas.charge {

	[Errand(Errand.Charge)]
	class ChargeErrand : IErrand {

		Action mAction;
		string mAafInPath, mAafOutPath;
		string mArcInPath, mArcOutPath;
		string mTarget;
		string mInput;
		string mOutput;

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);

			foreach (var parameter in cmdline) {
				switch (parameter.Name) {
					case "-extract-seq": mAction = DoExtractSeq; break;
					case "-replace-seq": mAction = DoReplaceSeq; break;
					case "-extract-ibnk": mAction = DoExtractIbnk; break;
					case "-replace-ibnk": mAction = DoReplaceIbnk; break;
					case "-extract-wsys": mAction = DoExtractWsys; break;
					case "-replace-wsys": mAction = DoReplaceWsys; break;
					case "-init-data-file": {
						if (parameter.Count == 0) {
							mareep.WriteError("CHARGE: bad -init-data-file parameter");
						}

						mAafInPath = parameter[0];

						if (parameter.Count > 1) {
							mAafOutPath = parameter[1];
						} else {
							mAafOutPath = Path.ChangeExtension(mAafInPath, ".new.aaf");
						}

						break;
					}
					case "-seq-data-file": {
						if (parameter.Count == 0) {
							mareep.WriteError("CHARGE: bad -seq-data-file parameter");
						}

						mArcInPath = parameter[0];

						if (parameter.Count > 1) {
							mArcOutPath = parameter[1];
						} else {
							mArcOutPath = Path.ChangeExtension(mArcInPath, ".new.arc");
						}

						break;
					}
					case "-target": mTarget = parameter[0]; break;
					case "-input": mInput = parameter[0]; break;
					case "-output": mOutput = parameter[0]; break;
				}
			}
		}

		public void ShowUsage() {
			mareep.WriteMessage("USAGE: charge <action> [...]\n");
			mareep.WriteMessage("\n");
			mareep.WriteMessage("ACTIONS:\n");
			mareep.WriteMessage("  -extract-seq    extract sequence data\n");
			mareep.WriteMessage("  -replace-seq    replace sequence data\n");
			mareep.WriteMessage("  -extract-ibnk   extract bank 'IBNK' data\n");
			mareep.WriteMessage("  -replace-ibnk   replace bank 'IBNK' data\n");
			mareep.WriteMessage("  -extract-wsys   extract wave bank 'WSYS' data\n");
			mareep.WriteMessage("  -replace-wsys   replace wave bank 'WSYS' data\n");
			mareep.WriteMessage("\n");
			mareep.WriteMessage("OPTIONS:\n");
			mareep.WriteMessage("  -init-data-file <file> [<output>]\n");
			mareep.WriteMessage("    Specifies the init data file (AAF). When writing,\n");
			mareep.WriteMessage("    <output> specifies the output file name.\n");
			mareep.WriteMessage("  -seq-data-file <file> [<output>]\n");
			mareep.WriteMessage("    Specifies the sequence data file (ARC). When writing,\n");
			mareep.WriteMessage("    <output> specifies the output file name.\n");
			mareep.WriteMessage("  -input <file>\n");
			mareep.WriteMessage("    Specifies the data with which to replace.\n");
			mareep.WriteMessage("  -output <file>\n");
			mareep.WriteMessage("    Specifies the file to which to extract data.\n");
			mareep.WriteMessage("  -target <value>\n");
			mareep.WriteMessage("    Specifies which sequence, bank, or wave bank to\n");
			mareep.WriteMessage("    extract or replace. For sequences, may be an index,\n");
			mareep.WriteMessage("    filename, or ASN name. For banks and wave banks, only\n");
			mareep.WriteMessage("    indices are supported.\n");
		}

		public void Perform() {
			mAction?.Invoke();
		}

		void DoExtractSeq() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mArcInPath == null) {
				mareep.WriteError("CHARGE: missing -seq-data-file parameter");
			}

			if (mOutput == null) {
				mareep.WriteError("CHARGE: missing -output parameter");
			}

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			int offset, size;

			using (Stream stream = mareep.OpenFile(mAafInPath)) {
				mareep.WriteMessage("Scanning AAF header...\n");
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);

				if (!ReadAafHeader(reader, 4, 0, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find sequence info data");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x42415243) { // 'BARC'
					mareep.WriteError("CHARGE: could not find 'BARC' header");
				}

				reader.Goto(offset + 12);
				int count = reader.ReadS32();
				reader.Goto(offset + 32);
				int index;

				mareep.WriteMessage("Found sequence info data (0x{0:X6}, 0x{1:X6}), {2} sequence(s)\n", offset, size, count);
				mareep.WriteMessage("Scanning sequence list...\n");

				if (!ReadBarcHeader(reader, mTarget, count, out index, out offset, out size)) {
					mareep.WriteError("CHARGE: could not find sequence {0}", mTarget);
				}

				mareep.WriteMessage("Found sequence {0} (0x{1:X6}, 0x{2:X6})\n", index, offset, size);
			}

			mareep.WriteMessage("Extracting sequence data...\n");

			using (Stream stream = mareep.OpenFile(mArcInPath)) {
				aBinaryReader reader = new aBinaryReader(stream);
				reader.Goto(offset);
				WriteFileData(mOutput, reader.Read8s(size));
			}
		}

		void DoReplaceSeq() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mArcInPath == null) {
				mareep.WriteError("CHARGE: missing -seq-data-file parameter");
			}

			byte[] arc_data = ReadFileData(mArcInPath);

			if (mInput == null) {
				mareep.WriteError("CHARGE: missing -input parameter");
			}

			byte[] seq_data = ReadFileData(mInput);

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			byte[] barc_data = null;

			using (Stream input = mareep.OpenFile(mAafInPath)) {
				mareep.WriteMessage("Scanning AAF header...\n");
				aBinaryReader reader = new aBinaryReader(input, Endianness.Big);
				int offset, size;

				if (!ReadAafHeader(reader, 4, 0, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find sequence info block");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x42415243) { // 'BARC'
					mareep.WriteError("CHARGE: could not find 'BARC' header");
				}

				reader.Goto(offset + 12);
				int count = reader.ReadS32();
				barc_data = new byte[32 + 32 * count];
				reader.Goto(offset + 32);
				int index, old_offset, old_size;

				mareep.WriteMessage("Found sequence info data (0x{0:X6}, 0x{1:X6}), {2} sequence(s)\n", offset, size, count);
				mareep.WriteMessage("Scanning sequence list...\n");

				if (!ReadBarcHeader(reader, mTarget, count, out index, out old_offset, out old_size)) {
					mareep.WriteError("CHARGE: could not find sequence {0}", mTarget);
				}

				mareep.WriteMessage("Found sequence {0} (0x{1:X6}, 0x{2:X6})\n", index, offset, size);

				int new_offset, new_size = ((seq_data.Length + 31) & ~31);
				int difference = (new_size - old_size);
				reader.Goto(offset + 16);

				using (Stream arc_stream = mareep.CreateFile(mArcOutPath))
				using (MemoryStream barc_stream = new MemoryStream(barc_data, true)) {
					mareep.WriteMessage("Writing new sequence data...\n");
					aBinaryWriter arc_writer = new aBinaryWriter(arc_stream);
					aBinaryWriter barc_writer = new aBinaryWriter(barc_stream, Endianness.Big);
					barc_writer.WriteS32(0x42415243); // 'BARC'
					barc_writer.WriteS32(0x2D2D2D2D); // '----'
					barc_writer.WriteS32(0);
					barc_writer.WriteS32(count);
					barc_writer.Write8s(reader.Read8s(16));

					for (int i = 0; i < count; ++i) {
						barc_writer.Write8s(reader.Read8s(14));
						barc_writer.WriteS16(reader.ReadS16());
						barc_writer.WriteS32(reader.ReadS32());
						barc_writer.WriteS32(reader.ReadS32());
						offset = reader.ReadS32();
						size = reader.ReadS32();
						new_offset = offset;

						if (offset > old_offset) {
							new_offset += difference;
						}

						arc_writer.Goto(new_offset);

						if (i == index) {
							arc_writer.Write8s(seq_data);
							arc_writer.WritePadding(32, 0);
							size = new_size;
						} else {
							arc_writer.Write8s(arc_data, offset, size);
						}

						barc_writer.WriteS32(new_offset);
						barc_writer.WriteS32(size);
					}
				}

				reader.Goto(0);

				using (Stream output = mareep.CreateFile(mAafOutPath)) {
					mareep.WriteMessage("Writing new AAF file...\n");
					aBinaryWriter writer = new aBinaryWriter(output, Endianness.Big);

					if (!WriteAafHeader(reader, writer, 4, 0, barc_data)) {
						mareep.WriteError("CHARGE: failed to write aaf file");
					}
				}
			}
		}

		void DoExtractIbnk() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mOutput == null) {
				mareep.WriteError("CHARGE: missing -output parameter");
			}

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			int index;

			if (!Int32.TryParse(mTarget, out index)) {
				mareep.WriteError("CHARGE: bad target {0}", mTarget);
			}

			using (Stream stream = mareep.OpenFile(mAafInPath)) {
				mareep.WriteMessage("Scanning AAF header...\n");
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);
				int offset, size;

				if (!ReadAafHeader(reader, 2, index, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find bank block\n");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x49424E4B) { // 'IBNK'
					mareep.WriteError("CHARGE: could not find 'IBNK' header");
				}

				mareep.WriteMessage("Found bank data {0} (0x{1:X6}, 0x{2:X6})\n", index, offset, size);
				mareep.WriteMessage("Extracting bank data...\n");
				reader.Goto(offset);
				WriteFileData(mOutput, reader.Read8s(size));
			}
		}

		void DoReplaceIbnk() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mInput == null) {
				mareep.WriteError("CHARGE: missing -input parameter");
			}

			mareep.WriteMessage("Loading input file data...\n");
			byte[] data = ReadFileData(mInput);

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			int index;

			if (!Int32.TryParse(mTarget, out index)) {
				mareep.WriteError("CHARGE: bad target {0}", mTarget);
			}

			using (Stream input = mareep.OpenFile(mAafInPath))
			using (Stream output = mareep.CreateFile(mAafOutPath)) {
				mareep.WriteMessage("Writing new AAF file...\n");
				aBinaryReader reader = new aBinaryReader(input, Endianness.Big);
				aBinaryWriter writer = new aBinaryWriter(output, Endianness.Big);

				if (!WriteAafHeader(reader, writer, 2, index, data)) {
					mareep.WriteError("CHARGE: failed to write aaf file");
				}
			}
		}

		void DoExtractWsys() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mOutput == null) {
				mareep.WriteError("CHARGE: missing -output parameter");
			}

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			int index;

			if (!Int32.TryParse(mTarget, out index)) {
				mareep.WriteError("CHARGE: bad target {0}", mTarget);
			}

			using (Stream stream = mareep.OpenFile(mAafInPath)) {
				mareep.WriteMessage("Scanning AAF header...\n");
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);
				int offset, size;

				if (!ReadAafHeader(reader, 3, index, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find wave bank data\n");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x57535953) { // 'WSYS'
					mareep.WriteError("CHARGE: could not find 'WSYS' header");
				}
				
				mareep.WriteMessage("Found wave bank data {0} (0x{1:X6}, 0x{2:X6})\n", index, offset, size);
				mareep.WriteMessage("Extracting wave bank data...\n");
				reader.Goto(offset);
				WriteFileData(mOutput, reader.Read8s(size));
			}
		}

		void DoReplaceWsys() {
			if (mAafInPath == null) {
				mareep.WriteError("CHARGE: missing -init-data-file parameter");
			}

			if (mInput == null) {
				mareep.WriteError("CHARGE: missing -input parameter");
			}

			mareep.WriteMessage("Loading input file data...\n");
			byte[] data = ReadFileData(mInput);

			if (mTarget == null) {
				mareep.WriteError("CHARGE: missing -target parameter");
			}

			int index;

			if (!Int32.TryParse(mTarget, out index)) {
				mareep.WriteError("CHARGE: bad target {0}", mTarget);
			}

			using (Stream input = mareep.OpenFile(mAafInPath))
			using (Stream output = mareep.CreateFile(mAafOutPath)) {
				mareep.WriteMessage("Writing new AAF file...\n");
				aBinaryReader reader = new aBinaryReader(input, Endianness.Big);
				aBinaryWriter writer = new aBinaryWriter(output, Endianness.Big);

				if (!WriteAafHeader(reader, writer, 3, index, data)) {
					mareep.WriteError("CHARGE: failed to write aaf file");
				}
			}
		}

		static string[] sAsnNames = {
			"MSD_SE_SEQ", "MSD_BGM_DOLPIC", "MSD_BGM_BIANCO",
			"MSD_BGM_MAMMA", "MSD_BGM_PINNAPACO_SEA", "MSD_BGM_PINNAPACO",
			"MSD_BGM_MARE_SEA", "MSD_BGM_MONTEVILLAGE", "MSD_BGM_SHILENA",
			"MSD_BGM_RICCO", "MSD_BGM_GET_SHINE", "MSD_BGM_CHUBOSS",
			"MSD_BGM_MISS", "MSD_BGM_BOSS", "MSD_BGM_MAP_SELECT",
			"MSD_BGM_BOSSPAKU_DEMO", "MSD_BGM_MAIN_TITLE", "MSD_BGM_CHUBOSS2",
			"MSD_BGM_EXTRA", "MSD_BGM_DELFINO", "MSD_BGM_MAREVILLAGE",
			"MSD_BGM_CORONA", "MSD_BGM_KAGEMARIO", "MSD_BGM_CAMERA",
			"MSD_BGM_MONTE_ONSEN", "MSD_BGM_MECHAKUPPA", "MSD_BGM_AIRPORT",
			"MSD_BGM_UNDERGROUND", "MSD_BGM_TITLEBACK", "MSD_BGM_MONTE_NIGHT",
			"MSD_BGM_CASINO", "MSD_BGM_EVENT", "MSD_BGM_TIME_IVENT",
			"MSD_BGM_SKY_AND_SEA", "MSD_BGM_MONTE_RESCUE", "MSD_BGM_MERRY_GO_ROUND",
			"MSD_BGM_SCENARIO_SELECT", "MSD_BGM_FANFARE_CASINO", "MSD_BGM_FANFARE_RACE",
			"MSD_BGM_CAMERA_KAGE", "MSD_BGM_GAMEOVER", "MSD_BGM_BOSSHANA_2ND3RD",
			"MSD_BGM_BOSSGESO_2DN3RD", "MSD_BGM_CHUBOSS_MANTA", "MSD_BGM_MONTE_LAST",
			"MSD_BGM_SHINE_APPEAR", "MSD_BGM_KUPPA", "MSD_BGM_MONTEMAN_RACE",
		};

		static int ConvertSeqNameToIndex(string name) {
			if (name.All(Char.IsDigit)) {
				return Int32.Parse(name);
			}

			for (int i = 0; i < sAsnNames.Length; ++i) {
				if (sAsnNames[i].Equals(name, StringComparison.InvariantCultureIgnoreCase)) {
					return i;
				}
			}

			return -1;
		}

		static bool ReadAafHeader(aBinaryReader reader, int id, int index, out int offset, out int size) {
			int section;
			offset = 0;
			size = 0;

			while ((section = reader.ReadS32()) != 0) {
				if (section == 2 || section == 3) {
					for (int i = 0; (offset = reader.ReadS32()) != 0; ++i) {
						size = reader.ReadS32();
						reader.Step(4);

						if (section == id && i == index) {
							return true;
						}
					}
				} else {
					offset = reader.ReadS32();
					size = reader.ReadS32();
					reader.Step(4);

					if (section == id && index == 0) {
						return true;
					}
				}

				if (section == id) {
					break;
				}
			}

			return false;
		}

		static bool WriteAafHeader(aBinaryReader reader, aBinaryWriter writer, int id, int index, byte[] data) {
			long reader_base = reader.Position;
			long writer_base = writer.Position;
			int old_offset, old_size;

			if (!ReadAafHeader(reader, id, index, out old_offset, out old_size)) {
				return false;
			}

			int difference = (data.Length - old_size);
			mareep.WriteMessage("Entry size difference: {0} byte(s)\n", difference);
			int danger_level = 0;

			reader.Goto(reader_base);
			int section;

			while ((section = reader.ReadS32()) != 0) {
				if (danger_level++ > 1000) {
					mareep.WriteError("CHARGE: malformed AAF file (endless loop detected)");
				}

				bool has_vnum = (section == 2 || section == 3);
				writer.WriteS32(section);
				int offset, size;
				int i = 0;

				while ((offset = reader.ReadS32()) != 0) {
					if (danger_level++ > 1000) {
						mareep.WriteError("CHARGE: malformed AAF file (endless loop detected)");
					}

					size = reader.ReadS32();
					int new_offset = offset;

					if (new_offset > old_offset) {
						new_offset += difference;
					}

					writer.Keep();
					writer.Goto(writer_base + new_offset);

					if (section == id && i == index) {
						writer.Write8s(data);
						size = data.Length;
					} else {
						reader.Keep();
						reader.Goto(reader_base + offset);
						writer.Write8s(reader.Read8s(size));
						reader.Back();
					}

					writer.Back();
					writer.WriteS32(new_offset);
					writer.WriteS32(size);

					if (has_vnum) {
						writer.WriteS32(reader.ReadS32());
					}

					++i;
				}

				writer.WriteS32(0);
			}

			writer.WriteS32(0);
			writer.Goto(writer.Length);
			writer.WritePadding(32, 0);
			return true;
		}

		static bool ReadBarcHeader(aBinaryReader reader, string seq, int count, out int index, out int offset, out int size) {
			index = ConvertSeqNameToIndex(seq);
			long start = reader.Position;
			offset = 0;
			size = 0;

			if (index > 0) {
				if (index >= count) {
					return false;
				}

				reader.Goto(start + 32 * index + 24);
				offset = reader.ReadS32();
				size = reader.ReadS32();
				return true;
			}

			for (int i = 0; i < count; ++i) {
				reader.Goto(start + 32 * i);
				string name = reader.ReadString<aZSTR>();

				if (name.Equals(seq, StringComparison.InvariantCultureIgnoreCase)) {
					reader.Goto(start + 32 * i + 24);
					offset = reader.ReadS32();
					size = reader.ReadS32();
					index = i;
					return true;
				}
			}

			return false;
		}

		static void MissingInitDataFile() {
			mareep.WriteError("CHARGE: missing -init-data-file parameter");
		}

		static void MissingSeqDataFile() {
			mareep.WriteError("CHARGE: missing -seq-data-file parameter");
		}

		static void MissingTarget() {
			mareep.WriteError("CHARGE: missing -target parameter");
		}

		static byte[] ReadFileData(string path) {
			try {
				return File.ReadAllBytes(path);
			} catch {
				mareep.WriteError("CHARGE: failed to read file {0}", path);
			}

			return null;
		}

		static void WriteFileData(string path, byte[] data) {
			try {
				File.WriteAllBytes(path, data);
			} catch {
				mareep.WriteError("CHARGE: failed to write file {0}", path);
			}
		}

	}

}
