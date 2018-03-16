
using arookas.IO.Binary;
using arookas.Xml;
using System;
using System.IO;
using System.Xml;

namespace arookas.whap {

	[Errand(Errand.Whap)]
	class WhapErrand : SimpleConverterErrand {

		WaveMixerMode mMixerMode;
		bool mExtractWav;
		string mWaveOutput;

		public override void LoadParams(string[] arguments) {
			base.LoadParams(arguments);

			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter parameter;

			parameter = mareep.GetLastCmdParam(cmdline, "-wave-dir");

			if (parameter == null) {
				mWaveOutput = "waves/";
			} else if (parameter.Count == 0) {
				mareep.WriteError("WHAP: missing argument for -wave-dir parameter.");
			} else {
				mWaveOutput = parameter[0];
			}

			parameter = mareep.GetLastCmdParam(cmdline, "-mix-mode");

			if (parameter == null) {
				mMixerMode = WaveMixerMode.Mix;
			} else if (parameter.Count == 0) {
				mareep.WriteError("WHAP: missing argument for -mix-mode parameter.");
			} else if (!Enum.TryParse(parameter[0], true, out mMixerMode)) {
				mareep.WriteError("WHAP: unknown mix mode '{0}'.", parameter[0]);
			}

			mExtractWav = (mareep.GetLastCmdParam(cmdline, "-extract-wav") != null);
		}

		public override void Perform() {
			var chain = new Transformer<WaveBank>();

			mareep.WriteMessage("Opening input file '{0}'...\n", Path.GetFileName(mInputFile));

			using (var instream = mareep.OpenFile(mInputFile)) {
				mareep.WriteMessage("Creating output file '{0}'...\n", Path.GetFileName(mOutputFile));

				using (var outstream = mareep.CreateFile(mOutputFile)) {
					mareep.WriteMessage("Linking deserializer...\n");

					switch (mInputFormat) {
						case IOFormat.Xml: chain.AppendLink(new XmlWaveBankDeserializer(CreateXmlInput(instream).Root)); break;
						case IOFormat.LittleBinary: chain.AppendLink(new BinaryWaveBankDeserializer(CreateLittleBinaryInput(instream))); break;
						case IOFormat.BigBinary: chain.AppendLink(new BinaryWaveBankDeserializer(CreateBigBinaryInput(instream))); break;
					}

					mareep.WriteMessage("Linking wave transferer...\n");

					var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(mInputFile));
					var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(mOutputFile));

					if (mInputFormat == IOFormat.Xml && IsFormatBinary(mOutputFormat)) {
						chain.AppendLink(new WaveArchivePacker(inputDirectory, outputDirectory, mMixerMode));
					} else if (IsFormatBinary(mInputFormat) && mOutputFormat == IOFormat.Xml) {
						chain.AppendLink(new WaveArchiveExtractor(inputDirectory, outputDirectory, mWaveOutput, mExtractWav));
					}

					mareep.WriteMessage("Linking serializer...\n");

					switch (mOutputFormat) {
						case IOFormat.Xml: chain.AppendLink(new XmlWaveBankSerializer(CreateXmlOutput(outstream))); break;
						case IOFormat.LittleBinary: chain.AppendLink(new BinaryWaveBankSerializer(CreateLittleBinaryOutput(outstream))); break;
						case IOFormat.BigBinary: chain.AppendLink(new BinaryWaveBankSerializer(CreateBigBinaryOutput(outstream))); break;
					}

					mareep.WriteMessage("Calling transform chain...\n");

					chain.Transform(null);
				}
			}
		}

	}

}

namespace arookas {

	abstract class BinaryWaveBankTransformer : Transformer<WaveBank> {

		protected const uint WSYS = 0x57535953U;
		protected const uint WINF = 0x57494E46U;
		protected const uint WBCT = 0x57424354U;
		protected const uint SCNE = 0x53434E45U;
		protected const uint C_DF = 0x432D4446U;
		protected const uint C_EX = 0x432D4558U;
		protected const uint C_ST = 0x432D5354U;

	}

	class BinaryWaveBankDeserializer : BinaryWaveBankTransformer {

		aBinaryReader mReader;

		public BinaryWaveBankDeserializer(aBinaryReader reader) {
			mReader = reader;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj != null) {
				return obj;
			}

			mReader.Keep();
			mReader.PushAnchor();

			if (mReader.Read32() != WSYS) {
				mareep.WriteError("WSYS: could not find header.");
			}

			var size = mReader.ReadS32();
			mReader.Step(8); // unused
			var winfOffset = mReader.ReadS32();
			var wbctOffset = mReader.ReadS32();

			mareep.WriteMessage("WSYS: header found, size {0:F1} KB\n", ((double)size / 1024.0d));

			var waveBank = new WaveBank();

			mReader.Goto(winfOffset);

			if (mReader.Read32() != WINF) {
				mareep.WriteError("WSYS: could not find WINF at 0x{0:X6}.", winfOffset);
			}

			var waveGroupCount = mReader.ReadS32();

			if (waveGroupCount < 0) {
				mareep.WriteError("WSYS: bad wave-group count '{0}' in WINF.", waveGroupCount);
			}

			mareep.WriteMessage("WSYS: WINF found, {0} wave group(s).\n", waveGroupCount);

			var waveGroupOffsets = mReader.ReadS32s(waveGroupCount);

			mReader.Goto(wbctOffset);

			if (mReader.Read32() != WBCT) {
				mareep.WriteError("WSYS: could not find WBCT at 0x{0:X6}.", wbctOffset);
			}

			mReader.Step(4); // unused

			var sceneCount = mReader.ReadS32();

			if (sceneCount != waveGroupCount) {
				mareep.WriteError("WSYS: WINF count ({0}) does not match WBCT count ({1}).", waveGroupCount, sceneCount);
			}

			var sceneOffsets = mReader.ReadS32s(sceneCount);

			for (var i = 0; i < waveGroupCount; ++i) {
				mReader.Goto(waveGroupOffsets[i]);

				var archiveName = mReader.ReadString<aCSTR>(112);
				var waveInfoCount = mReader.ReadS32();

				if (waveInfoCount < 0) {
					mareep.WriteError("WSYS: bad wave count '{0}' in wave group #{1}.", waveInfoCount, i);
				}

				var waveInfoOffsets = mReader.ReadS32s(waveInfoCount);

				mReader.Goto(sceneOffsets[i]);

				if (mReader.Read32() != SCNE) {
					mareep.WriteError("WSYS: could not find SCNE at 0x{0:X6}.", sceneOffsets[i]);
				}

				mReader.Step(8); // unused
				var cdfOffset = mReader.ReadS32();
				mReader.Goto(cdfOffset);

				if (mReader.Read32() != C_DF) {
					mareep.WriteError("WSYS: could not find C-DF at 0x{0:X6}.", cdfOffset);
				}

				var waveidCount = mReader.ReadS32();

				if (waveidCount != waveInfoCount) {
					mareep.WriteError("WSYS: C-DF count ({0}) does not match wave-info count ({1}).", waveidCount, waveInfoCount);
				}

				var waveidOffsets = mReader.ReadS32s(waveidCount);

				var waveGroup = new WaveGroup();
				waveGroup.ArchiveFileName = archiveName;

				for (var j = 0; j < waveInfoCount; ++j) {
					var wave = new Wave();

					mReader.Goto(waveidOffsets[j]);

					var waveid = (mReader.ReadS32() & 0xFFFF);
					wave.WaveId = waveid;

					mReader.Goto(waveInfoOffsets[j]);
					mReader.Step(1); // unknown

					var format = (WaveFormat)mReader.Read8();

					if (!format.IsDefined()) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad format '{2}'.", i, j, (byte)format);
					} else {
						wave.Format = format;
					}

					var key = mReader.Read8();

					if (key < 0 || key > 127) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad root key '{2}'.", i, j, key);
					} else {
						wave.RootKey = key;
					}

					mReader.Step(1); // alignment

					var sampleRate = mReader.ReadF32();

					if (sampleRate < 0.0f) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad sample rate '{2:F1}'.", i, j, sampleRate);
					} else {
						wave.SampleRate = sampleRate;
					}

					var waveStart = mReader.ReadS32();

					if (waveStart < 0) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad wave start '{2}'.", i, j, waveStart);
					} else {
						wave.WaveStart = waveStart;
					}

					var waveSize = mReader.ReadS32();

					if (waveSize < 0) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad wave size '{1}'.", i, j, waveSize);
					} else {
						wave.WaveSize = waveSize;
					}

					wave.Loop = (mReader.Read32() != 0);

					var loopStart = mReader.ReadS32();

					if (loopStart < 0) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad loop start '{2}'.", i, j, loopStart);
					} else {
						wave.LoopStart = loopStart;
					}

					var loopEnd = mReader.ReadS32();

					if (loopEnd < 0) {
						mareep.WriteError("WSYS: group #{0}: wave #{1}: bad loop end '{2}'.", i, j, loopEnd);
					} else {
						wave.LoopEnd = loopEnd;
					}

					var sampleCount = mReader.ReadS32();
					wave.SampleCount = mareep.CalculateSampleCount(format, waveSize);

					if (loopStart > loopEnd) {
						mareep.WriteWarning("WSYS: group #{0}: wave #{1}: loop start '{2}' is greater than loop end '{3}'.\n", i, j, loopStart, loopEnd);
					}

					if (loopStart > wave.SampleCount) {
						mareep.WriteWarning("WSYS: group #{0}: wave #{1}: loop start '{2}' is greater than sample count '{3}'.\n", i, j, loopStart, wave.SampleCount);
					}

					if (loopEnd > wave.SampleCount) {
						mareep.WriteWarning("WSYS: group #{0}: wave #{1}: loop end '{2}' is greater than sample count '{3}'.\n", i, j, loopEnd, wave.SampleCount);
					}

					wave.HistoryLast = mReader.ReadS16();
					wave.HistoryPenult = mReader.ReadS16();

					// rest of the fields are unknown or runtime

					waveGroup.Add(wave);
				}

				waveBank.Add(waveGroup);
			}

			mReader.PopAnchor();
			mReader.Back();

			return waveBank;
		}

	}

	class BinaryWaveBankSerializer : BinaryWaveBankTransformer {

		aBinaryWriter mWriter;
		WaveBank mWaveBank;

		public BinaryWaveBankSerializer(aBinaryWriter writer) {
			mWriter = writer;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj == null) {
				return null;
			}

			mWaveBank = obj;

			WriteWaveBank();

			return mWaveBank;
		}

		void WriteWaveBank() {
			var winfSize = CalculateControlSize(mWaveBank.Count);
			var wbctSize = CalculateControlGroupSize(mWaveBank.Count);
			var baseOffset = (32 + winfSize + wbctSize);

			mWriter.PushAnchor();

			mWriter.Write32(WSYS);
			mWriter.WriteS32(0); // TODO: size
			mWriter.WriteS32(0); // unused
			mWriter.WriteS32(0); // unused
			mWriter.WriteS32(32);
			mWriter.WriteS32(32 + winfSize);
			mWriter.WritePadding(32, 0);

			mWriter.Write32(WINF);
			mWriter.WriteS32(mWaveBank.Count);

			var winfOffset = baseOffset;

			foreach (var waveGroup in mWaveBank) {
				mWriter.WriteS32(winfOffset);
				winfOffset += CalculateWaveGroupSize(waveGroup);
			}

			mWriter.WritePadding(32, 0);

			mWriter.Write32(WBCT);
			mWriter.WriteS32(0); // unused
			mWriter.WriteS32(mWaveBank.Count);

			var wbctOffset = baseOffset;

			foreach (var waveGroup in mWaveBank) {
				mWriter.WriteS32(wbctOffset + CalculateArchiveInfoSize(waveGroup.Count) + CalculateWaveSize(waveGroup.Count) + CalculateControlSize(waveGroup.Count) + 64);
				wbctOffset += CalculateWaveGroupSize(waveGroup);
			}

			mWriter.WritePadding(32, 0);

			foreach (var waveGroup in mWaveBank) {
				WriteWaveGroup(waveGroup);
			}

			mWriter.PopAnchor();
		}

		void WriteWaveGroup(WaveGroup waveGroup) {
			var offset = ((int)mWriter.Position + CalculateArchiveInfoSize(waveGroup.Count));

			if (waveGroup.ArchiveFileName.Length > 111) {
				mareep.WriteWarning("WSYS: wave archive name '{0}' is too long.\n", waveGroup.ArchiveFileName);
			}

			mWriter.WriteString<aCSTR>(waveGroup.ArchiveFileName, 112);
			mWriter.WriteS32(waveGroup.Count);

			for (var i = 0; i < waveGroup.Count; ++i) {
				mWriter.WriteS32(offset + 48 * i + 4);
			}

			mWriter.WritePadding(32, 0);

			foreach (var wave in waveGroup) {
				WriteWave(wave);
			}

			mWriter.WritePadding(32, 0);

			var sceneOffset = (int)mWriter.Position;

			mWriter.Write32(C_DF);
			mWriter.WriteS32(waveGroup.Count);

			for (var i = 0; i < waveGroup.Count; ++i) {
				mWriter.WriteS32(offset + 48 * i);
			}

			mWriter.WritePadding(32, 0);

			// these two sections are unused
			mWriter.Write32(C_EX);
			mWriter.WritePadding(32, 0);

			mWriter.Write32(C_ST);
			mWriter.WritePadding(32, 0);

			mWriter.Write32(SCNE);
			mWriter.WriteS32(0); // unused
			mWriter.WriteS32(0); // unused
			mWriter.WriteS32(sceneOffset);
			mWriter.WriteS32(sceneOffset + 32);
			mWriter.WriteS32(sceneOffset + 64);
			mWriter.WritePadding(32, 0);
		}

		void WriteWave(Wave wave) {
			mWriter.WriteS32(wave.WaveId);
			mWriter.Write8(0xFF); // unknown
			mWriter.Write8((byte)wave.Format);
			mWriter.Write8((byte)wave.RootKey);
			mWriter.WritePadding(4, 0);
			mWriter.WriteF32(wave.SampleRate);

			mWriter.WriteS32(wave.WaveStart);
			mWriter.WriteS32(wave.WaveSize);

			if (wave.Loop) {
				mWriter.WriteS32(-1);
				mWriter.WriteS32(wave.LoopStart);
				mWriter.WriteS32(wave.LoopEnd);
			} else {
				mWriter.WriteS32(0);
				mWriter.WriteS32(0);
				mWriter.WriteS32(0);
			}

			mWriter.WriteS32(wave.SampleCount);

			if (wave.Loop) {
				mWriter.WriteS16((short)wave.HistoryLast);
				mWriter.WriteS16((short)wave.HistoryPenult);
			} else {
				mWriter.WriteS16(0);
				mWriter.WriteS16(0);
			}

			mWriter.Write32(0); // runtime (load-flag pointer)
			mWriter.Write32(0x1D8); // unknown
		}
		
		int CalculateControlSize(int count) {
			return mareep.RoundUp32B(8 + 4 * count);
		}

		int CalculateControlGroupSize(int count) {
			return mareep.RoundUp32B(12 + 4 * count);
		}

		int CalculateWaveSize(int count) {
			return mareep.RoundUp32B(48 * count);
		}

		int CalculateArchiveInfoSize(int count) {
			return mareep.RoundUp32B(116 + 4 * count);
		}

		int CalculateWaveGroupSize(WaveGroup waveGroup) {
			return (
				CalculateArchiveInfoSize(waveGroup.Count) +
				CalculateWaveSize(waveGroup.Count) +
				CalculateControlSize(waveGroup.Count) +
				96
			);
		}

	}

	abstract class XmlWaveBankTransformer : Transformer<WaveBank> {

		protected const string cWaveBank = "wave-bank";

		protected const string cWaveGroup = "wave-group";
		protected const string cWaveArchive = "archive";

		protected const string cWave = "wave";
		protected const string cWaveId = "id";
		protected const string cWaveFile = "file";
		protected const string cWaveFormat = "format";
		protected const string cWaveRate = "rate";
		protected const string cWaveKey = "key";
		protected const string cWaveLoopStart = "loop-start";
		protected const string cWaveLoopEnd = "loop-end";

	}

	class XmlWaveBankDeserializer : XmlWaveBankTransformer {

		xElement mRootElement;

		public XmlWaveBankDeserializer(xElement element) {
			mRootElement = element;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj != null) {
				return obj;
			}

			return LoadWaveBank(mRootElement);
		}

		WaveBank LoadWaveBank(xElement xwavebank) {
			var waveBank = new WaveBank();

			foreach (var xwavegroup in xwavebank.Elements(cWaveGroup)) {
				var waveGroup = LoadWaveGroup(xwavegroup);

				if (waveGroup != null) {
					waveBank.Add(waveGroup);
				}
			}

			return waveBank;
		}

		WaveGroup LoadWaveGroup(xElement xwavegroup) {
			var waveGroup = new WaveGroup();

			var xarchive = xwavegroup.Attribute(cWaveArchive);

			if (xarchive == null) {
				mareep.WriteWarning("XML: line #{0}: missing archive attribute\n", xwavegroup.LineNumber);
				return null;
			}

			waveGroup.ArchiveFileName = xarchive.Value;

			foreach (var xwave in xwavegroup.Elements(cWave)) {
				var wave = LoadWave(xwave);

				if (wave != null) {
					waveGroup.Add(wave);
				}
			}

			return waveGroup;
		}

		Wave LoadWave(xElement xwave) {
			xAttribute attribute;

			var wave = new Wave();

			attribute = xwave.Attribute(cWaveId);

			if (attribute == null) {
				mareep.WriteWarning("XML: line #{0}: missing wave id\n", xwave.LineNumber);
				return null;
			}

			var waveid = (attribute | -1);

			if (waveid < 0) {
				mareep.WriteWarning("XML: line #{0}: bad wave id '{1}'\n", attribute.LineNumber, attribute.Value);
				return null;
			}

			wave.WaveId = waveid;

			attribute = xwave.Attribute(cWaveFile);

			if (attribute == null) {
				mareep.WriteWarning("XML: line #{0}: missing file\n", xwave.LineNumber);
				return null;
			}

			wave.FileName = attribute.Value;

			attribute = xwave.Attribute(cWaveFormat);

			if (attribute == null) {
				mareep.WriteWarning("XML: line #{0}: missing format\n", xwave.LineNumber);
				return null;
			}

			var format = attribute.AsEnum((WaveFormat)(-1));

			if (!format.IsDefined()) {
				mareep.WriteWarning("XML: line #{0}: bad wave format '{1}'\n", attribute.LineNumber, attribute.Value);
				return null;
			}

			wave.Format = format;

			attribute = xwave.Attribute(cWaveRate);

			if (attribute != null) {
				var samplerate = (attribute | -1.0f);

				if (samplerate < 0.0f) {
					mareep.WriteWarning("XML: line #{0}: bad sample rate '{1:F1}'.\n", attribute.LineNumber, attribute.Value);
					return null;
				}

				wave.SampleRate = samplerate;
			}

			attribute = xwave.Attribute(cWaveKey);

			var keynumber = 60;

			if (attribute != null) {
				keynumber = attribute.AsKeyNumber();

				if (keynumber < 0 || keynumber > 127) {
					mareep.WriteWarning("XML: line #{0}: bad root key '{1}'\n", attribute.LineNumber, attribute.Value);
					return null;
				}
			}

			wave.RootKey = keynumber;

			var xloopstart = xwave.Attribute(cWaveLoopStart);
			var xloopend = xwave.Attribute(cWaveLoopEnd);

			if (xloopstart != null && xloopend != null) {
				var loopstart = (xloopstart | -1);
				var loopend = (xloopend | -1);

				if (loopstart < 0) {
					mareep.WriteWarning("XML: line #{0}: bad loop start '{0}'\n", xloopstart.LineNumber, xloopstart.Value);
					return null;
				} else if (loopend < 0) {
					mareep.WriteWarning("XML: line #{0}: bad loop end '{0}'\n", xloopend.LineNumber, xloopend.Value);
					return null;
				}

				wave.Loop = true;
				wave.LoopStart = loopstart;
				wave.LoopEnd = loopend;
			} else if ((xloopstart == null) != (xloopend == null)) {
				mareep.WriteWarning("XML: line #{0}: only one loop specified.\n", (xloopstart ?? xloopend).LineNumber);
				return null;
			}

			return wave;
		}

	}

	class XmlWaveBankSerializer : XmlWaveBankTransformer {

		XmlWriter mWriter;
		WaveBank mWaveBank;

		public XmlWaveBankSerializer(XmlWriter writer) {
			mWriter = writer;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj == null) {
				return null;
			}

			mWaveBank = obj;

			WriteWaveBank();

			return mWaveBank;
		}

		void WriteWaveBank() {
			mWriter.WriteStartElement(cWaveBank);

			foreach (var waveGroup in mWaveBank) {
				WriteWaveGroup(waveGroup);
			}

			mWriter.WriteEndElement();
			mWriter.Flush();
		}

		void WriteWaveGroup(WaveGroup waveGroup) {
			mWriter.WriteStartElement(cWaveGroup);
			mWriter.WriteAttributeString(cWaveArchive, waveGroup.ArchiveFileName);

			foreach (var wave in waveGroup) {
				WriteWave(wave);
			}

			mWriter.WriteEndElement();
		}

		void WriteWave(Wave wave) {
			mWriter.WriteStartElement(cWave);
			mWriter.WriteAttributeString(cWaveId, wave.WaveId);
			mWriter.WriteAttributeString(cWaveFile, wave.FileName);
			mWriter.WriteAttributeString(cWaveFormat, wave.Format.ToLowerString());
			mWriter.WriteAttributeString(cWaveRate, wave.SampleRate);

			if (wave.RootKey != 60) {
				mWriter.WriteAttributeString(cWaveKey, mareep.ConvertKey(wave.RootKey));
			}

			if (wave.Loop) {
				mWriter.WriteAttributeString(cWaveLoopStart, wave.LoopStart);
				mWriter.WriteAttributeString(cWaveLoopEnd, wave.LoopEnd);
			}

			mWriter.WriteEndElement();
		}

	}

	class WaveArchivePacker : Transformer<WaveBank> {

		string mArchiveDirectory, mWaveDirectory;
		WaveMixerMode mMixerMode;

		public WaveArchivePacker(string archiveDirectory, string waveDirectory, WaveMixerMode mixermode) {
			mArchiveDirectory = archiveDirectory;
			mWaveDirectory = waveDirectory;
			mMixerMode = mixermode;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj == null) {
				return null;
			}
			
			var badwaves = 0;

			if (!Directory.Exists(mArchiveDirectory)) {
				mareep.WriteMessage("Creating directory '{0}'...\n", mArchiveDirectory);
				Directory.CreateDirectory(mArchiveDirectory);
			}

			mareep.WriteMessage("Transferring waves...\n");

			foreach (var waveGroup in obj) {
				var archiveFileName = Path.Combine(mArchiveDirectory, waveGroup.ArchiveFileName);

				mareep.WriteSeparator('-');
				mareep.WriteMessage("{0}\n", waveGroup.ArchiveFileName);

				using (var outstream = mareep.CreateFile(archiveFileName)) {
					var writer = new aBinaryWriter(outstream, Endianness.Big);

					foreach (var wave in waveGroup) {
						var waveFileName = Path.Combine(mWaveDirectory, wave.FileName);
						var extension = Path.GetExtension(waveFileName).ToLowerInvariant();

						if (!File.Exists(waveFileName)) {
							mareep.WriteWarning("XFER: cannot find file '{0}'\n", wave.FileName);
							++badwaves;
							continue;
						}

						using (var instream = mareep.OpenFile(waveFileName)) {
							wave.WaveStart = (int)writer.Position;
							WaveMixer mixer = null;

							switch (extension) {
								case ".wav": mixer = new MicrosoftWaveMixer(instream); break;
								case ".raw": mixer = new RawWaveMixer(instream, wave.Format); break;
							}

							if (mixer == null) {
								mareep.WriteWarning("XFER: could not create wave mixer (unsupported file extension '{0}')\n", extension);
								++badwaves;
								continue;
							}

							mixer.MixerMode = mMixerMode;
							mixer.CopyWaveInfo(wave);

							if (wave.Loop && wave.LoopStart > 0) {
								int last, penult;

								mixer.CalculateHistory(wave.LoopStart, wave.Format, out last, out penult);

								wave.HistoryLast = last;
								wave.HistoryPenult = penult;
							}

							mixer.Write(wave.Format, writer);
							wave.WaveSize = ((int)writer.Position - wave.WaveStart);
							writer.WritePadding(32, 0);

							mareep.WriteMessage(" #{0:X4} '{1,-35}' (0x{2:X6} 0x{3:X6})\n", wave.WaveId, Path.GetFileName(wave.FileName), wave.WaveStart, wave.WaveSize);
						}
					}
				}
			}

			if (badwaves > 0) {
				mareep.WriteError("Failed to transfer {0} wave(s).", badwaves);
			}

			return obj;
		}

	}

	class WaveArchiveExtractor : Transformer<WaveBank> {

		string mInputDirectory, mOutputDirectory, mWaveDirectory;
		bool mExtractWav;

		public WaveArchiveExtractor(string inputDirectory, string outputDirectory, string waveDirectory, bool extractwav) {
			mInputDirectory = inputDirectory;
			mOutputDirectory = outputDirectory;
			mWaveDirectory = waveDirectory;
			mExtractWav = extractwav;
		}

		protected override WaveBank DoTransform(WaveBank obj) {
			if (obj == null) {
				return null;
			}

			var waveDirectory = Path.Combine(mOutputDirectory, mWaveDirectory);

			if (!Directory.Exists(waveDirectory)) {
				mareep.WriteMessage("Creating directory '{0}'...\n", waveDirectory);
				Directory.CreateDirectory(waveDirectory);
			}

			mareep.WriteMessage("Transferring waves...\n");

			var extension = (mExtractWav ? "wav" : "raw");

			foreach (var waveGroup in obj) {
				var archiveFileName = Path.Combine(mInputDirectory, waveGroup.ArchiveFileName);
				var archiveNoExtension = Path.GetFileNameWithoutExtension(waveGroup.ArchiveFileName);

				using (var instream = mareep.OpenFile(archiveFileName)) {
					var reader = new aBinaryReader(instream, Endianness.Big);

					mareep.WriteSeparator('-');
					mareep.WriteMessage("{0} ({1} wave(s))\n", waveGroup.ArchiveFileName, waveGroup.Count);

					foreach (var wave in waveGroup) {
						var waveFileName = String.Format("{0}_{1:D5}.{2}.{3}", archiveNoExtension, wave.WaveId, wave.Format.ToLowerString(), extension);
						wave.FileName =  Path.Combine(mWaveDirectory, waveFileName);

						using (var outstream = mareep.CreateFile(Path.Combine(mOutputDirectory, wave.FileName))) {
							if (mExtractWav) {
								ExtractWav(wave, reader, outstream);
							} else {
								ExtractRaw(wave, reader, outstream);
							}
						}

						mareep.WriteMessage(" #{0:X4} {1} {2} {3}Hz {4} samples\n", wave.WaveId, mareep.ConvertKey(wave.RootKey), wave.Format.ToLowerString(), (int)wave.SampleRate, wave.SampleCount);
					}
				}
			}

			return obj;
		}

		void ExtractWav(Wave wave, aBinaryReader reader, Stream outstream) {
			reader.Goto(wave.WaveStart);
			var data = reader.Read8s(wave.WaveSize);
			var writer = new aBinaryWriter(outstream, Endianness.Little);
			var mixer = new RawWaveMixer(new MemoryStream(data), wave.Format);
			var dataSize = (wave.SampleCount * 2);
			var sampleRate = (int)wave.SampleRate;
			
			writer.WriteString("RIFF");
			writer.WriteS32(36 + dataSize);
			writer.WriteString("WAVE");
			writer.WriteString("fmt ");
			writer.WriteS32(16);
			writer.WriteS16(1); // format
			writer.Write16(1); // channel count
			writer.WriteS32(sampleRate);
			writer.WriteS32(sampleRate * 2); // byte rate
			writer.Write16(2); // block align
			writer.Write16(16); // bit depth
			writer.WriteString("data");
			writer.WriteS32(dataSize);
			mixer.Write(WaveFormat.Pcm16, writer);
		}

		void ExtractRaw(Wave wave, aBinaryReader reader, Stream outstream) {
			reader.Goto(wave.WaveStart);
			var data = reader.Read8s(wave.WaveSize);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			writer.Write8s(data);
		}

	}

}
