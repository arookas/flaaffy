
using arookas.IO.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace arookas.charge {

	[Errand(Errand.Charge)]
	class ChargeErrand : IErrand {

		Action mAction;

		public ChargeErrand() {
			mAction = null;
		}

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);

			foreach (var parameter in cmdline) {
				Entrypoint entrypoint = null;

				switch (parameter.Name) {
					case "-extract-seq":  entrypoint = DoExtractSeq;  break;
					case "-extract-ibnk": entrypoint = DoExtractIbnk; break;
					case "-replace-ibnk": entrypoint = DoReplaceIbnk; break;
					case "-extract-wsys": entrypoint = DoExtractWsys; break;
					case "-replace-wsys": entrypoint = DoReplaceWsys; break;
				}

				if (entrypoint != null) {
					mAction = new Action(entrypoint, parameter);
				}
			}
		}

		public void Perform() {
			if (mAction != null) {
				mAction.Call();
			}
		}

		void DoExtractSeq(string[] arguments) {
			if (arguments.Length != 4) {
				mareep.WriteError("CHARGE: bad argument count");
			}

			string aaf_path = arguments[0];
			string arc_path = arguments[1];
			string seq_path = arguments[2];
			string seq_name = arguments[3];

			int seq_index = ConvertSeqNameToIndex(seq_name);

			if (seq_index < 0) {
				mareep.WriteError("CHARGE: bad sequence {0}", seq_index);
			}

			int offset, size;

			using (Stream stream = OpenStreamRead(aaf_path)) {
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);

				if (!ReadAafHeader(reader, 4, 0, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find sequence info block");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x42415243) { // 'BARC'
					mareep.WriteError("CHARGE: could not find 'BARC' header");
				}

				reader.Goto(offset + 12);

				if (seq_index >= reader.ReadS32()) {
					mareep.WriteError("CHARGE: bad sequence {0}", seq_index);
				}

				reader.Goto(offset + 32 + (32 * seq_index) + 24);
				offset = reader.ReadS32();
				size = reader.ReadS32();
			}

			byte[] seq_data = null;

			using (Stream stream = OpenStreamRead(arc_path)) {
				aBinaryReader reader = new aBinaryReader(stream);
				reader.Goto(offset);
				seq_data = reader.Read8s(size);
			}

			try {
				File.WriteAllBytes(seq_path, seq_data);
			} catch {
				mareep.WriteError("CHARGE: failed to write output file");
			}
		}

		void DoExtractIbnk(string[] arguments) {
			if (arguments.Length != 3) {
				mareep.WriteError("CHARGE: bad argument count");
			}

			string aaf_path = arguments[0];
			string ibnk_path = arguments[1];
			int ibnk_index;

			if (!Int32.TryParse(arguments[2], out ibnk_index)) {
				mareep.WriteError("CHARGE: bad ibnk index {0}", arguments[2]);
			}
			
			byte[] wsys_data;

			using (Stream stream = OpenStreamRead(aaf_path)) {
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);
				int offset, size;

				if (!ReadAafHeader(reader, 2, ibnk_index, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find bank block\n");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x49424E4B) { // 'IBNK'
					mareep.WriteError("CHARGE: could not find 'IBNK' header");
				}

				reader.Goto(offset);
				wsys_data = reader.Read8s(size);
			}

			try {
				File.WriteAllBytes(ibnk_path, wsys_data);
			} catch {
				mareep.WriteError("CHARGE: failed to write output file");
			}
		}

		void DoReplaceIbnk(string[] arguments) {
			if (arguments.Length != 3) {
				mareep.WriteError("CHARGE: bad argument count");
			}

			string aaf_path = arguments[0];
			string ibnk_path = arguments[1];
			int ibnk_index;

			if (!Int32.TryParse(arguments[2], out ibnk_index)) {
				mareep.WriteError("CHARGE: bad ibnk index {0}", arguments[2]);
			}

			byte[] ibnk_data = null;

			try {
				ibnk_data = File.ReadAllBytes(ibnk_path);
			} catch {
				mareep.WriteError("CHARGE: failed to read ibnk file");
			}

			byte[] aaf_data = null;

			try {
				aaf_data = File.ReadAllBytes(aaf_path);
			} catch {
				mareep.WriteError("CHARGE: failed to read aaf file");
			}

			Stream out_stream = null;

			try {
				out_stream = OpenStreamWrite(aaf_path);
			} catch {
				mareep.WriteError("CHARGE: failed to create aaf file");
			}

			using (out_stream)
			using (MemoryStream in_stream = new MemoryStream(aaf_data)) {
				aBinaryReader reader = new aBinaryReader(in_stream, Endianness.Big);
				aBinaryWriter writer = new aBinaryWriter(out_stream, Endianness.Big);

				if (!WriteAafHeader(reader, writer, 2, ibnk_index, ibnk_data)) {
					mareep.WriteError("CHARGE: failed to write aaf file");
				}
			}
		}

		void DoExtractWsys(string[] arguments) {
			if (arguments.Length != 3) {
				mareep.WriteError("CHARGE: bad argument count");
			}

			string aaf_path = arguments[0];
			string wsys_path = arguments[1];
			int wsys_index;

			if (!Int32.TryParse(arguments[2], out wsys_index)) {
				mareep.WriteError("CHARGE: bad wsys index {0}", arguments[2]);
			}
			
			byte[] wsys_data;

			using (Stream stream = OpenStreamRead(aaf_path)) {
				aBinaryReader reader = new aBinaryReader(stream, Endianness.Big);
				int offset, size;

				if (!ReadAafHeader(reader, 3, wsys_index, out offset, out size)) {
					mareep.WriteError("CHARGE: failed to find wave bank block\n");
				}

				reader.Goto(offset);

				if (reader.ReadS32() != 0x57535953) { // 'WSYS'
					mareep.WriteError("CHARGE: could not find 'WSYS' header");
				}

				reader.Goto(offset);
				wsys_data = reader.Read8s(size);
			}

			try {
				File.WriteAllBytes(wsys_path, wsys_data);
			} catch {
				mareep.WriteError("CHARGE: failed to write output file");
			}
		}

		void DoReplaceWsys(string[] arguments) {
			if (arguments.Length != 3) {
				mareep.WriteError("CHARGE: bad argument count");
			}

			string aaf_path = arguments[0];
			string wsys_path = arguments[1];
			int wsys_index;

			if (!Int32.TryParse(arguments[2], out wsys_index)) {
				mareep.WriteError("CHARGE: bad wsys index {0}", arguments[2]);
			}

			byte[] wsys_data = null;

			try {
				wsys_data = File.ReadAllBytes(wsys_path);
			} catch {
				mareep.WriteError("CHARGE: failed to read wsys file");
			}

			byte[] aaf_data = null;

			try {
				aaf_data = File.ReadAllBytes(aaf_path);
			} catch {
				mareep.WriteError("CHARGE: failed to read aaf file");
			}

			Stream out_stream = null;

			try {
				out_stream = OpenStreamWrite(aaf_path);
			} catch {
				mareep.WriteError("CHARGE: failed to create aaf file");
			}

			using (out_stream)
			using (MemoryStream in_stream = new MemoryStream(aaf_data)) {
				aBinaryReader reader = new aBinaryReader(in_stream, Endianness.Big);
				aBinaryWriter writer = new aBinaryWriter(out_stream, Endianness.Big);

				if (!WriteAafHeader(reader, writer, 3, wsys_index, wsys_data)) {
					mareep.WriteError("CHARGE: failed to write aaf file");
				}
			}
		}

		static string[] sSeqNames = {
			"se.scom", "k_dolpic.com", "k_bianco.com",
			"k_manma.com", "t_pinnapaco_s", "t_pinnapaco.c",
			"t_mare_sea.co", "t_montevillag", "t_shilena.com",
			"k_rico.com", "k_clear.com", "t_chuboss.com",
			"k_miss.com", "t_boss.com", "t_select.com",
			"t_bosspakkun_", "k_title.com", "t_chuboss2.co",
			"k_ex.com", "t_delfino.com", "t_marevillage",
			"t_corona.com", "k_kagemario.c", "k_camera.com",
			"t_montevillag", "t_mechakuppa_", "k_airport.com",
			"k_chika.com", "k_titleback.c", "t_montevillag",
			"t_delfino_kaj", "t_event.com", "t_timelimit.c",
			"t_extra_skyan", "t_montevillag", "t_pinnapaco_m",
			"k_select.com", "t_casino_fanf", "t_race_fanfar",
			"k_camerakage.", "k_gameover.co", "t_boss_hanach",
			"t_boss_geso_i", "t_chuboss_man", "t_montevillage",
			"t_shine_appea", "k_kuppa.com", "t_monteman_ra",
		};

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

			for (int i = 0; i < sSeqNames.Length; ++i) {
				if (sSeqNames[i].Equals(name, StringComparison.InvariantCultureIgnoreCase)) {
					return i;
				}
			}

			return -1;
		}

		static bool ReadAafHeader(aBinaryReader reader, int id, int index, out int offset, out int size) {
			offset = 0;
			size = 0;

			int section;

			while ((section = reader.ReadS32()) != 0) {
				bool has_vnum = (section == 2 || section == 3);
				int i = 0;

				while ((offset = reader.ReadS32()) != 0) {
					size = reader.ReadS32();

					if (has_vnum) {
						reader.Step(4);
					}

					if (section == id && i == index) {
						return true;
					}

					++i;
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

			reader.Goto(reader_base);
			int section;

			while ((section = reader.ReadS32()) != 0) {
				bool has_vnum = (section == 2 || section == 3);
				writer.WriteS32(section);
				int offset, size;
				int i = 0;

				while ((offset = reader.ReadS32()) != 0) {
					size = reader.ReadS32();

					if (offset > old_offset) {
						offset += difference;
					}

					writer.Keep();
					writer.Goto(writer_base + offset);

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
					writer.WriteS32(offset);
					writer.WriteS32(size);

					if (has_vnum) {
						writer.WriteS32(reader.ReadS32());
					}

					++i;
				}

				writer.WriteS32(0);
			}

			writer.WriteS32(0);

			return true;
		}

		static byte[] WriteBarcHeader(aBinaryReader aaf,  aBinaryWriter barc, int seq_index) {
			return null;
		}

		static Stream OpenStreamRead(string path) {
			Stream stream = null;

			try {
				stream = File.OpenRead(path);
			} catch {
				mareep.WriteError("CHARGE: failed to open file {0}", path);
			}

			return stream;
		}

		static Stream OpenStreamWrite(string path) {
			Stream stream = null;

			try {
				stream = File.Create(path);
			} catch {
				mareep.WriteError("CHARGE: failed to create file {0}", path);
			}

			return stream;
		}

		delegate void Entrypoint(string[] arguments);

		class Action {

			Entrypoint Entrypoint { get; set; }
			string[] Arguments { get; set; }

			public Action(Entrypoint entrypoint, aCommandLineParameter parameter) {
				Entrypoint = entrypoint;
				Arguments = parameter.ToArray();
			}

			public void Call() {
				Entrypoint(Arguments);
			}

		}

	}

}
