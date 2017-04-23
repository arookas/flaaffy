
using arookas.IO.Binary;
using arookas.Xml;
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace arookas {

	[Performer(Action.Whap)]
	class WhapPerformer : InputOutputPerformer {

		string mWaveOutput;

		public override void LoadParams(string[] arguments) {
			base.LoadParams(arguments);

			aCommandLineParameter param;
			var cmdline = new aCommandLine(arguments);

			param = cmdline.LastOrDefault(p => p.Name.Equals("-wave-dir", StringComparison.InvariantCultureIgnoreCase));

			if (param == null) {
				mWaveOutput = "waves/";
			} else if (param.Count != 1) {
				mareep.WriteError("Bad -wave-dir parameter.");
			} else {
				mWaveOutput = param[0];
			}
		}

		public override void Perform() {
			WaveBank waveBank = LoadWaveBank();

			if (waveBank == null) {
				mareep.WriteError("Failed to load wave bank.\n");
			}

			TransferWaves(waveBank);
			SaveWaveBank(waveBank);
		}

		WaveBank LoadWaveBank() {
			mareep.WriteMessage("Opening input file \"{0}\"...\n", mInputFile);

			using (var stream = mareep.OpenFile(mInputFile)) {
				mareep.WriteMessage("Loading wave bank...\n");

				switch (mInputFormat) {
					case IOFormat.Xml: {
						var xml = CreateXmlInput(stream);
						return Xml.LoadWaveBank(xml.Root);
					}
					case IOFormat.LittleBinary: {
						var reader = CreateLittleBinaryInput(stream);
						return Binary.LoadWaveBank(reader);
					}
					case IOFormat.BigBinary: {
						var reader = CreateBigBinaryInput(stream);
						return Binary.LoadWaveBank(reader);
					}
					default: mareep.WriteError("Unimplemented input format \"{0}\".\n", mInputFormat); break;
				}
			}

			return null;
		}
		void SaveWaveBank(WaveBank waveBank) {
			mareep.WriteMessage("Creating output file \"{0}\"...\n", mOutputFile);

			using (var stream = mareep.CreateFile(mOutputFile)) {
				mareep.WriteMessage("Saving wave bank...\n");

				switch (mOutputFormat) {
					case IOFormat.Xml: {
						using (var writer = CreateXmlOutput(stream)) {
							writer.WriteStartDocument();
							Xml.SaveWaveBank(waveBank, writer);
							writer.WriteEndDocument();
						}
						break;
					}
					case IOFormat.LittleBinary: {
						var writer = CreateLittleBinaryOutput(stream);
						Binary.SaveWaveBank(waveBank, writer);
						break;
					}
					case IOFormat.BigBinary: {
						var writer = CreateBigBinaryOutput(stream);
						Binary.SaveWaveBank(waveBank, writer);
						break;
					}
					default: mareep.WriteError("Unimplemented output format \"{0}\".\n", mOutputFormat); break;
				}
			}
		}

		void TransferWaves(WaveBank waveBank) {
			mareep.WriteMessage("Transferring waves...\n");

			if (mInputFormat == IOFormat.Xml && IsFormatBinary(mOutputFormat)) {
				TransferWavesXmlToBin(waveBank);
			} else if (IsFormatBinary(mInputFormat) && mOutputFormat == IOFormat.Xml) {
				TransferWavesBinToXml(waveBank);
			} else {
				mareep.WriteWarning("Could not transfer waves.\n");
			}
		}
		void TransferWavesXmlToBin(WaveBank waveBank) {
			var badwaves = 0;

			foreach (var waveGroup in waveBank) {
				var archiveFileName = Path.Combine(Path.GetDirectoryName(mInputFile), waveGroup.ArchiveFileName);

				mareep.WriteMessage("{0}\n", waveGroup.ArchiveFileName);

				using (var outstream = mareep.CreateFile(archiveFileName)) {
					var writer = CreateBigBinaryOutput(outstream);

					foreach (var wave in waveGroup) {
						var waveFileName = Path.Combine(Path.GetDirectoryName(mOutputFile), wave.FileName);
						var extension = Path.GetExtension(waveFileName).ToLowerInvariant();

						if (!File.Exists(waveFileName)) {
							mareep.WriteWarning("Cannot find file \"{0}\" for wave id #{1} in group \"{2}\".\n", wave.FileName, wave.WaveId, waveGroup.ArchiveFileName);
							++badwaves;
							continue;
						}

						using (var instream = mareep.OpenFile(waveFileName)) {
							wave.WaveStart = (int)writer.Position;
							WaveMixer mixer = null;

							switch (extension) {
								case ".wav": mixer = new MicrosoftWaveMixer(instream, wave); break;
								case ".raw": mixer = new RawWaveMixer(instream, wave, wave.Format, wave.SampleCount); break;
							}

							if (mixer == null) {
								mareep.WriteError("Could not create wave mixer (unsupported file extension '{0}').", extension);
							}

							switch (wave.Format) {
								case WaveFormat.Pcm8: mixer.WritePcm8(writer); break;
								case WaveFormat.Pcm16: mixer.WritePcm16(writer); break;
								case WaveFormat.Adpcm2: mixer.WriteAdpcm2(writer); break;
								case WaveFormat.Adpcm4: mixer.WriteAdpcm4(writer); break;
							}

							wave.WaveSize = ((int)writer.Position - wave.WaveStart);
							mareep.WriteMessage("  {0} 0x{1:X8} 0x{2:X8}\n", wave.WaveId, wave.WaveStart, wave.WaveSize);
							writer.WritePadding(32, 0);
						}
					}
				}
			}

			if (badwaves > 0) {
				mareep.WriteError("Failed to transfer {0} wave(s).\n", badwaves);
			}
		}
		void TransferWavesBinToXml(WaveBank waveBank) {
			foreach (var waveGroup in waveBank) {
				var archiveFileName = Path.Combine(Path.GetDirectoryName(mInputFile), waveGroup.ArchiveFileName);
				var outputDirectory = Path.Combine(Path.GetDirectoryName(mOutputFile), mWaveOutput);
				var waveid = 0;

				mareep.WriteMessage("{0}\n", waveGroup.ArchiveFileName);

				using (var instream = mareep.OpenFile(archiveFileName)) {
					var reader = CreateBigBinaryInput(instream);

					foreach (var wave in waveGroup) {
						var waveFileName = String.Format("group_{0}_wave_{1:D6}.raw", waveGroup.ArchiveFileName, waveid++);
						wave.FileName = Path.Combine(mWaveOutput, waveFileName);

						// make sure directory exists
						Directory.CreateDirectory(outputDirectory);

						using (var outstream = mareep.CreateFile(Path.Combine(outputDirectory, waveFileName))) {
							var writer = CreateBigBinaryOutput(outstream);
							reader.Goto(wave.WaveStart);
							writer.Write8s(reader.Read8s(wave.WaveSize));
						}
					}
				}
			}
		}

		public static class Xml {

			public static WaveBank LoadWaveBank(xElement root) {
				var waveBank = new WaveBank();

				foreach (var waveGroupElem in root.Elements("wave-group")) {
					var waveGroup = new WaveGroup();

					waveGroup.ArchiveFileName = (waveGroupElem.Attribute("archive") | "");

					var index = 0;

					foreach (var waveElem in waveGroupElem.Elements("wave")) {
						var wave = new Wave();

						var waveidAttr = waveElem.Attribute("id");
						var waveid = (waveidAttr != null ? (waveidAttr | index) : index);

						if (waveid < 0) {
							mareep.WriteError("Bad wave id '{0}' in wave of wave group \"{1}\".\n", waveid, waveGroup.ArchiveFileName);
						} else {
							wave.WaveId = waveid;
						}

						wave.FileName = waveElem.Attribute("file");

						var format = waveElem.Attribute("format").AsEnum((WaveFormat)(-1));

						if (!format.IsDefined()) {
							mareep.WriteError("Bad or missing format in wave '{0}' of wave group \"{1}\".\n", waveid, waveGroup.ArchiveFileName);
						} else {
							wave.Format = format;
						}

						var sampleRate = (waveElem.Attribute("rate") | 0.0f);

						if (sampleRate < 0.0f) {
							mareep.WriteError("Bad sample rate '{0:F1} Hz' in wave '{1}' of wave group \"{2}\".\n", sampleRate, waveid, waveGroup.ArchiveFileName);
						} else {
							wave.SampleRate = sampleRate;
						}
						
						var keynumber = 60;
						var keyAttr = waveElem.Attribute("key");

						if (keyAttr != null) {
							keynumber = mareep.ConvertKey(keyAttr.Value);

							if (keynumber < 0) {
								keynumber = (int)keyAttr;
							}

							if (keynumber < 0 || keynumber > 127) {
								mareep.WriteError("Bad root key '{0}' in wave '{1}' of wave group \"{2}\".\n", keyAttr.Value, waveid, waveGroup.ArchiveFileName);
							}
						}

						wave.RootKey = keynumber;

						var loopStartElem = waveElem.Attribute("loop-start");
						var loopEndElem = waveElem.Attribute("loop-end");

						if (loopStartElem != null && loopEndElem != null) {
							var loopStart = (loopStartElem | -1);
							var loopEnd = (loopEndElem | -1);

							if (loopStart < 0) {
								mareep.WriteError("Bad loop start '{0}' in wave '{1}' of wave group \"{2}\".\n", loopStart, waveid, waveGroup.ArchiveFileName);
							} else if (loopEnd < 0) {
								mareep.WriteError("Bad loop start '{0}' in wave '{1}' of wave group \"{2}\".\n", loopStart, waveid, waveGroup.ArchiveFileName);
							}

							wave.Loop = true;
							wave.LoopStart = loopStart;
							wave.LoopEnd = loopEnd;
						} else if ((loopStartElem == null) != (loopEndElem == null)) {
							mareep.WriteWarning("Only one loop point specified in wave '{0}' of wave group \"{1}\".\n", waveid, waveGroup.ArchiveFileName);
						}

						var sampleCount = (waveElem.Attribute("samples") | 0);

						if (sampleCount < 0) {
							mareep.WriteError("Bad sample count '{0}' in wave '{1}' of wave group \"{2}\".\n", sampleCount, waveid, waveGroup.ArchiveFileName);
						}

						wave.SampleCount = sampleCount;

						waveGroup.Add(wave);
						++index;
					}

					waveBank.Add(waveGroup);
				}

				return waveBank;
			}
			public static void SaveWaveBank(WaveBank waveBank, XmlWriter writer) {
				writer.WriteStartElement("wave-bank");

				foreach (var waveGroup in waveBank) {
					writer.WriteStartElement("wave-group");
					writer.WriteAttributeString("archive", waveGroup.ArchiveFileName);

					var index = 0;

					foreach (var wave in waveGroup) {
						writer.WriteStartElement("wave");

						if (wave.WaveId != index) {
							writer.WriteAttributeString("id", wave.WaveId.ToString());
						}

						writer.WriteAttributeString("file", wave.FileName);
						writer.WriteAttributeString("format", wave.Format.ToString().ToLowerInvariant());
						writer.WriteAttributeString("rate", wave.SampleRate.ToString("R"));

						if (wave.RootKey != 60) {
							writer.WriteAttributeString("key", mareep.ConvertKey(wave.RootKey));
						}

						if (wave.Loop) {
							writer.WriteAttributeString("loop-start", wave.LoopStart.ToString());
							writer.WriteAttributeString("loop-end", wave.LoopEnd.ToString());
						}

						if (wave.SampleCount > 0) {
							writer.WriteAttributeString("samples", wave.SampleCount.ToString());
						} 

						writer.WriteEndElement();
						++index;
					}

					writer.WriteEndElement();
				}

				writer.WriteEndElement();
			}

		}

		public static class Binary {

			public static WaveBank LoadWaveBank(aBinaryReader reader) {
				reader.Keep();
				reader.PushAnchor();

				if (reader.Read32() != WSYS) {
					mareep.WriteError("Could not find WSYS.\n");
				}

				var size = reader.ReadS32();
				reader.Step(8); // unused
				var winfOffset = reader.ReadS32();
				var wbctOffset = reader.ReadS32();

				mareep.WriteMessage("WSYS found, size {0:F1} KB\n", ((double)size / 1024.0d));
				
				var waveBank = new WaveBank();

				reader.Goto(winfOffset);

				if (reader.Read32() != WINF) {
					mareep.WriteError("Could not find WINF at 0x{0:X}.\n", winfOffset);
				}

				var waveGroupCount = reader.ReadS32();

				if (waveGroupCount < 0) {
					mareep.WriteError("Bad wave-group count '{0}' in WINF.\n", waveGroupCount);
				}

				mareep.WriteMessage("WINF found, {0} wave group(s).\n", waveGroupCount);

				var waveGroupOffsets = reader.ReadS32s(waveGroupCount);

				reader.Goto(wbctOffset);

				if (reader.Read32() != WBCT) {
					mareep.WriteError("Could not find WBCT at 0x{0:X}.\n", wbctOffset);
				}

				reader.Step(4); // unused

				var sceneCount = reader.ReadS32();

				if (sceneCount != waveGroupCount) {
					mareep.WriteError("WINF count ({0}) does not match WBCT count ({1}).\n", waveGroupCount, sceneCount);
				}

				var sceneOffsets = reader.ReadS32s(sceneCount);

				for (var i = 0; i < waveGroupCount; ++i) {
					reader.Goto(waveGroupOffsets[i]);

					var archiveName = reader.ReadString<aCSTR>(112);
					var waveInfoCount = reader.ReadS32();

					if (waveInfoCount < 0) {
						mareep.WriteError("Bad wave count '{0}' in wave group #{1}.\n", waveInfoCount, i);
					}

					var waveInfoOffsets = reader.ReadS32s(waveInfoCount);

					reader.Goto(sceneOffsets[i]);

					if (reader.Read32() != SCNE) {
						mareep.WriteError("Could not find SCNE at 0x{0:X}.\n", sceneOffsets[i]);
					}

					reader.Step(8); // unused
					var cdfOffset = reader.ReadS32();
					reader.Goto(cdfOffset);

					if (reader.Read32() != C_DF) {
						mareep.WriteError("Could not find C-DF at 0x{0:X}.\n", cdfOffset);
					}

					var waveidCount = reader.ReadS32();

					if (waveidCount != waveInfoCount) {
						mareep.WriteError("C-DF count ({0}) does not match wave-info count ({1}).\n", waveidCount, waveInfoCount);
					}

					var waveidOffsets = reader.ReadS32s(waveidCount);

					var waveGroup = new WaveGroup();
					waveGroup.ArchiveFileName = archiveName;

					for (var j = 0; j < waveInfoCount; ++j) {
						var wave = new Wave();

						reader.Goto(waveidOffsets[j]);
						
						var waveid = (reader.ReadS32() & 0xFFFF);
						wave.WaveId = waveid;

						reader.Goto(waveInfoOffsets[j]);
						reader.Step(1); // unknown

						var format = (WaveFormat)reader.Read8();

						if (!format.IsDefined()) {
							mareep.WriteError("Wave group #{0}: bad format '{1}' in wave #{2}.\n", i, (byte)format, j);
						} else {
							wave.Format = format;
						}

						var key = reader.Read8();

						if (key < 0 || key > 127) {
							mareep.WriteError("Wave group #{0}: bad root key '{1}' in wave #{2}.\n", i, key, j);
						} else {
							wave.RootKey = key;
						}

						reader.Step(1); // alignment

						var sampleRate = reader.ReadF32();

						if (sampleRate < 0.0f) {
							mareep.WriteError("Wave group #{0}: bad sample rate '{1:F1}' in wave #{2}.\n", i, sampleRate, j);
						} else {
							wave.SampleRate = sampleRate;
						}

						var waveStart = reader.ReadS32();

						if (waveStart < 0) {
							mareep.WriteError("Wave group #{0}: bad wave start '{1}' in wave #{2}.\n", i, waveStart, j);
						} else {
							wave.WaveStart = waveStart;
						}

						var waveSize = reader.ReadS32();

						if (waveSize < 0) {
							mareep.WriteError("Wave group #{0}: bad wave size '{1}' in wave #{2}.\n", i, waveSize, j);
						} else {
							wave.WaveSize = waveSize;
						}

						wave.Loop = (reader.Read32() != 0);

						var loopStart = reader.ReadS32();

						if (loopStart < 0) {
							mareep.WriteError("Wave group #{0}: bad loop start '{1}' in wave #{2}.\n", i, loopStart, j);
						} else {
							wave.LoopStart = loopStart;
						}

						var loopEnd = reader.ReadS32();

						if (loopEnd < 0) {
							mareep.WriteError("Wave group #{0}: bad loop end '{1}' in wave #{2}.\n", i, loopEnd, j);
						} else {
							wave.LoopEnd = loopEnd;
						}

						var sampleCount = reader.ReadS32();

						if (sampleCount < 0) {
							mareep.WriteError("Wave group #{0}: bad sample count '{1}' in wave #{2}.\n", i, sampleCount, j);
						} else {
							wave.SampleCount = sampleCount;
						}

						if (loopStart > loopEnd) {
							mareep.WriteWarning("Wave group #{0}: loop start '{1}' is greater than loop end '{2}' in wave #{2}.\n", i, loopStart, loopEnd, j);
						}

						if (loopStart > sampleCount) {
							mareep.WriteWarning("Wave group #{0}: loop start '{1}' is greater than sample count '{2}' in wave #{2}.\n", i, loopStart, sampleCount, j);
						}

						if (loopEnd > sampleCount) {
							mareep.WriteWarning("Wave group #{0}: loop end '{1}' is greater than sample count '{2}' in wave #{2}'.\n", i, loopEnd, sampleCount, j);
						}
						
						// rest of the fields are unknown or runtime

						waveGroup.Add(wave);
					}

					waveBank.Add(waveGroup);
				}

				reader.PopAnchor();
				reader.Back();

				return waveBank;
			}

			static int RoundUp16B(int value) {
				return ((value + 15) & ~15);
			}
			static int RoundUp32B(int value) {
				return ((value + 31) & ~31);
			}

			static int CalculateControlSize(int count) {
				return RoundUp32B(8 + 4 * count);
			}
			static int CalculateControlGroupSize(int count) {
				return RoundUp32B(12 + 4 * count);
			}
			static int CalculateWaveSize(int count) {
				return RoundUp32B(48 * count);
			}
			static int CalculateArchiveInfoSize(int count) {
				return RoundUp32B(116 + 4 * count);
			}
			static int CalculateWaveGroupSize(WaveGroup waveGroup) {
				return (
					CalculateArchiveInfoSize(waveGroup.Count) +
					CalculateWaveSize(waveGroup.Count) +
					CalculateControlSize(waveGroup.Count) +
					96
				);
			}

			static void SaveWave(Wave wave, aBinaryWriter writer) {
				writer.WriteS32(wave.WaveId);
				writer.Write8(0xFF); // unknown
				writer.Write8((byte)wave.Format);
				writer.Write8((byte)wave.RootKey);
				writer.WritePadding(4, 0);
				writer.WriteF32(wave.SampleRate);
				writer.WriteS32(wave.WaveStart);
				writer.WriteS32(wave.WaveSize);
				writer.WriteS32(wave.Loop ? -1 : 0);
				writer.WriteS32(wave.Loop ? wave.LoopStart : 0);
				writer.WriteS32(wave.Loop ? wave.LoopEnd : 0);
				writer.WriteS32(wave.SampleCount);
				writer.WriteS16(0); // unknown
				writer.WriteS16(0); // unknown
				writer.Write32(0); // runtime (load-flag pointer)
				writer.Write32(0x1D8); // unknown
			}
			static void SaveWaveGroup(WaveGroup waveGroup, aBinaryWriter writer) {
				var offset = ((int)writer.Position + CalculateArchiveInfoSize(waveGroup.Count));

				if (waveGroup.ArchiveFileName.Length > 111) {
					mareep.WriteWarning("Wave group archive \"{0}\" is too long!\n", waveGroup.ArchiveFileName);
				}

				writer.WriteString<aCSTR>(waveGroup.ArchiveFileName, 112);
				writer.WriteS32(waveGroup.Count);

				for (var i = 0; i < waveGroup.Count; ++i) {
					writer.WriteS32(offset + 48 * i + 4);
				}

				writer.WritePadding(32, 0);

				foreach (var wave in waveGroup) {
					SaveWave(wave, writer);
				}

				writer.WritePadding(32, 0);

				var sceneOffset = (int)writer.Position;

				writer.Write32(C_DF);
				writer.WriteS32(waveGroup.Count);

				for (var i = 0; i < waveGroup.Count; ++i) {
					writer.WriteS32(offset + 48 * i);
				}

				writer.WritePadding(32, 0);

				// these two sections are unused
				writer.Write32(C_EX);
				writer.WritePadding(32, 0);

				writer.Write32(C_ST);
				writer.WritePadding(32, 0);

				writer.Write32(SCNE);
				writer.WriteS32(0); // unused
				writer.WriteS32(0); // unused
				writer.WriteS32(sceneOffset);
				writer.WriteS32(sceneOffset + 32);
				writer.WriteS32(sceneOffset + 48);
				writer.WritePadding(32, 0);
			}
			public static void SaveWaveBank(WaveBank waveBank, aBinaryWriter writer) {
				var winfSize = CalculateControlSize(waveBank.Count);
				var wbctSize = CalculateControlGroupSize(waveBank.Count);
				var baseOffset = (32 + winfSize + wbctSize);

				writer.PushAnchor();

				writer.Write32(WSYS);
				writer.WriteS32(0); // TODO: size
				writer.WriteS32(0); // unused
				writer.WriteS32(0); // unused
				writer.WriteS32(32);
				writer.WriteS32(32 + winfSize);
				writer.WritePadding(32, 0);

				writer.Write32(WINF);
				writer.WriteS32(waveBank.Count);

				var winfOffset = baseOffset;

				foreach (var waveGroup in waveBank) {
					writer.WriteS32(winfOffset);
					winfOffset += CalculateWaveGroupSize(waveGroup);
				}

				writer.WritePadding(32, 0);

				writer.Write32(WBCT);
				writer.WriteS32(0); // unused
				writer.WriteS32(waveBank.Count);

				var wbctOffset = baseOffset;

				foreach (var waveGroup in waveBank) {
					writer.WriteS32(wbctOffset + CalculateArchiveInfoSize(waveGroup.Count) + CalculateWaveSize(waveGroup.Count) + CalculateControlSize(waveGroup.Count) + 64);
					wbctOffset += CalculateWaveGroupSize(waveGroup);
				}

				writer.WritePadding(32, 0);

				foreach (var waveGroup in waveBank) {
					SaveWaveGroup(waveGroup, writer);
				}

				writer.PopAnchor();
			}

			const uint WSYS = 0x57535953u;
			const uint WINF = 0x57494E46u;
			const uint WBCT = 0x57424354u;
			const uint SCNE = 0x53434E45u;
			const uint C_DF = 0x432D4446u;
			const uint C_EX = 0x432D4558u;
			const uint C_ST = 0x432D5354u;

		}

	}

}
