
using arookas.IO.Binary;
using arookas.Xml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace arookas.shock {

	[Errand(Errand.Shock)]
	class ShockErrand : SimpleConverterErrand {

		public override void Perform() {
			Transformer<InstrumentBank> chain = null;
			mareep.WriteMessage("Opening input file '{0}'...\n", Path.GetFileName(mInputFile));

			using (var instream = mareep.OpenFile(mInputFile)) {
				mareep.WriteMessage("Creating output file '{0}'...\n", Path.GetFileName(mOutputFile));

				using (var outstream = mareep.CreateFile(mOutputFile)) {
					switch (mInputFormat) {
						case IOFormat.Xml: chain = new XmlBankDeserializer(CreateXmlInput(instream).Root); break;
						case IOFormat.LittleBinary: chain = new BinaryBankDeserializer(CreateLittleBinaryInput(instream)); break;
						case IOFormat.BigBinary: chain = new BinaryBankDeserializer(CreateBigBinaryInput(instream)); break;
						default: mareep.WriteError("SHOCK: unimplemented input format '{0}'.", mInputFormat); break;
					}

					switch (mOutputFormat) {
						case IOFormat.Xml: chain.AppendLink(new XmlBankSerializer(CreateXmlOutput(outstream))); break;
						case IOFormat.LittleBinary: chain.AppendLink(new BinaryBankSerializer(CreateLittleBinaryOutput(outstream))); break;
						case IOFormat.BigBinary: chain.AppendLink(new BinaryBankSerializer(CreateBigBinaryOutput(outstream))); break;
						default: mareep.WriteError("SHOCK: unimplemented output format '{0}'.", mOutputFormat); break;
					}

					chain.Transform(null);
				}
			}
		}

	}

}

namespace arookas {

	abstract class BinaryBankTransformer : Transformer<InstrumentBank> {

		protected const uint IBNK = 0x49424E4Bu;
		protected const uint BANK = 0x42414E4Bu;
		protected const uint INST = 0x494E5354u;
		protected const uint PERC = 0x50455243u;
		protected const uint PER2 = 0x50455232u;

		protected const int cBankCount = 240;
		protected const int cOscillatorCount = 2;
		protected const int cRandomEffectCount = 2;
		protected const int cSenseEffectCount = 2;

		protected bool CheckBank(InstrumentBank bank) {
			var valid = true;

			for (var i = 0; i < cBankCount; ++i) {
				if (bank[i] == null) {
					continue;
				}

				var type = bank[i].Type;

				if (type == InstrumentType.Melodic) {
					var instrument = (bank[i] as MelodicInstrument);

					if (instrument == null) {
						mareep.WriteWarning("IBNK: #{0} bad instrument type\n", i);
						valid = false;
						continue;
					}

					if (instrument.OscillatorCount > 2) {
						mareep.WriteWarning("IBNK: #{0} instrument has more than two oscillators\n", i);
						valid = false;
					}

					if (instrument.Effects.Count(effect => effect is RandomInstrumentEffect) > 2) {
						mareep.WriteWarning("IBNK: #{0} instrument has more than two random effects\n", i);
						valid = false;
					}

					if (instrument.Effects.Count(effect => effect is SenseInstrumentEffect) > 2) {
						mareep.WriteWarning("IBNK: #{0} instrument has more than two sense effects\n", i);
						valid = false;
					}
				} else if (type == InstrumentType.DrumSet) {
					var drumset = (bank[i] as DrumSet);

					if (drumset == null) {
						mareep.WriteWarning("IBNK: #{0} bad instrument type\n", i);
						valid = false;
						continue;
					}

					for (var key = 0; key < drumset.Capacity; ++key) {
						var percussion = drumset[key];

						if (percussion == null) {
							continue;
						}

						if (percussion.Effects.Count(effect => effect is RandomInstrumentEffect) > 2) {
							mareep.WriteWarning("IBNK: #{0} {1} percussion has more than two random effects\n", i, mareep.ConvertKey(key));
							valid = false;
						}

						if (percussion.Effects.Count(effect => effect is SenseInstrumentEffect) > 0) {
							mareep.WriteWarning("IBNK: #{0} {1} percussion has sense effects\n", i, mareep.ConvertKey(key));
							valid = false;
						}
					}
				} else {
					mareep.WriteWarning("IBNK: #{0} bad instrument type\n", i);
					valid = false;
				}
			}

			return valid;
		}

	}

	class BinaryBankDeserializer : BinaryBankTransformer {

		aBinaryReader mReader;

		public BinaryBankDeserializer(aBinaryReader reader) {
			mReader = reader;
		}

		protected override InstrumentBank DoTransform(InstrumentBank obj) {
			if (obj != null) {
				return obj;
			}

			mReader.Keep();
			mReader.PushAnchor();

			if (mReader.Read32() != IBNK) {
				mareep.WriteError("IBNK: could not find header.");
			}

			var size = mReader.ReadS32();
			var virtualNumber = mReader.ReadS32();

			mareep.WriteMessage("IBNK: header found, size {0:F1} KB, virtual number {1}\n", ((double)size / 1024.0d), virtualNumber);

			var bank = new InstrumentBank(virtualNumber, 256);

			mReader.Goto(32);

			if (mReader.Read32() != BANK) {
				mareep.WriteError("IBNK: could not find bank.\n");
			}

			var instrumentOffsets = mReader.ReadS32s(cBankCount);

			mareep.WriteMessage("IBNK: bank found, {0} instrument(s)\n", instrumentOffsets.Count(offset => offset != 0));

			for (var i = 0; i < cBankCount; ++i) {
				if (instrumentOffsets[i] == 0) {
					continue;
				}

				mReader.Goto(instrumentOffsets[i]);

				var instrumentType = mReader.Read32();
				IInstrument instrument = null;

				mareep.WriteMessage("IBNK: #{0,-3} ", i);

				switch (instrumentType) {
					case INST: instrument = LoadMelodic(); break;
					case PERC: instrument = LoadDrumSet(1); break;
					case PER2: instrument = LoadDrumSet(2); break;
				}

				if (instrument == null) {
					mareep.WriteMessage("(null)\n");
					continue;
				}

				bank.Add(i, instrument);
			}

			mReader.PopAnchor();
			mReader.Back();

			return bank;
		}

		MelodicInstrument LoadMelodic() {
			var instrument = new MelodicInstrument();

			mReader.Step(4); // unused

			instrument.Volume = mReader.ReadF32();
			instrument.Pitch = mReader.ReadF32();

			var oscillatorOffsets = mReader.ReadS32s(cOscillatorCount);
			var randomEffectOffsets = mReader.ReadS32s(cRandomEffectCount);
			var senseEffectOffsets = mReader.ReadS32s(cSenseEffectCount);
			var keyRegionOffsets = mReader.ReadS32s(mReader.ReadS32());

			mareep.WriteMessage(
				"INST: volume {0:F1} pitch {1:F1} oscillators {2} effects {3} key regions {4}\n",
				instrument.Volume,
				instrument.Pitch,
				oscillatorOffsets.Count(offset => offset != 0),
				(randomEffectOffsets.Count(offset => offset != 0) + senseEffectOffsets.Count(offset => offset != 0)),
				keyRegionOffsets.Length
			);

			foreach (var offset in oscillatorOffsets) {
				if (offset == 0) {
					continue;
				}

				mReader.Goto(offset);
				var osc = LoadOscillator();
				if (osc != null) {
					instrument.AddOscillator(osc);
				}
			}

			foreach (var offset in randomEffectOffsets) {
				if (offset == 0) {
					continue;
				}
				mReader.Goto(offset);
				var effect = LoadRandomEffect();
				if (effect != null) {
					instrument.AddEffect(effect);
				}
			}

			foreach (var offset in senseEffectOffsets) {
				if (offset == 0) {
					continue;
				}

				mReader.Goto(offset);
				var effect = LoadSenseEffect();
				if (effect != null) {
					instrument.AddEffect(effect);
				}
			}

			foreach (var keyRegionOffset in keyRegionOffsets) {
				mReader.Goto(keyRegionOffset);

				var key = mReader.Read8();

				if (key > 127) {
					mareep.WriteWarning("IBNK: bad key region key number '{0}' at 0x{1:X6}\n", key, (mReader.Position - 1));
					continue;
				}

				mReader.Step(3); // alignment

				var velRegionOffsets = mReader.ReadS32s(mReader.ReadS32());
				var keyRegion = instrument.AddRegion(key);

				foreach (var velRegionOffset in velRegionOffsets) {
					mReader.Goto(velRegionOffset);

					var velocity = mReader.Read8();

					if (velocity > 127) {
						mareep.WriteWarning("IBNK: bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (mReader.Position - 1));
						continue;
					}

					mReader.Step(3); // alignment

					var waveid = mReader.Read32();
					var volume = mReader.ReadF32();
					var pitch = mReader.ReadF32();

					keyRegion.AddRegion(velocity, (int)(waveid & 0xFFFF), volume, pitch);
				}
			}

			return instrument;
		}
		DrumSet LoadDrumSet(int version) {
			var drumset = new DrumSet();

			mReader.Step(4); // unused
			mReader.Step(128); // unused 128-item byte array

			var percussionOffsets = mReader.ReadS32s(128);
			sbyte[] panTable = null;
			ushort[] releaseTable = null;

			if (version == 2) {
				panTable = mReader.ReadS8s(128);
				releaseTable = mReader.Read16s(128);
			}

			mareep.WriteMessage(
				"PER{0}: {1} percussions\n",
				(version == 2 ? '2' : 'C'),
				percussionOffsets.Count(offset => offset != 0)
			);

			for (var i = 0; i < 128; ++i) {
				if (percussionOffsets[i] == 0) {
					continue;
				}

				mReader.Goto(percussionOffsets[i]);

				var percussion = drumset.AddPercussion(i);

				percussion.Volume = mReader.ReadF32();
				percussion.Pitch = mReader.ReadF32();
				var randomEffectOffsets = mReader.ReadS32s(2);
				var velRegionOffsets = mReader.ReadS32s(mReader.ReadS32());

				if (version == 2) {
					percussion.Pan = ((float)panTable[i] / 127.0f);
					percussion.Release = releaseTable[i];
				}

				foreach (var offset in randomEffectOffsets) {
					if (offset == 0) {
						continue;
					}

					mReader.Goto(offset);
					var effect = LoadRandomEffect();
					if (effect != null) {
						percussion.AddEffect(effect);
					}
				}

				foreach (var velRegionOffset in velRegionOffsets) {
					mReader.Goto(velRegionOffset);

					var velocity = mReader.Read8();

					if (velocity > 127) {
						mareep.WriteWarning("Bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (mReader.Position - 1));
						return null;
					}

					mReader.Step(3); // alignment

					var waveid = mReader.Read32();
					var volume = mReader.ReadF32();
					var pitch = mReader.ReadF32();

					var velRegion = percussion.AddRegion(velocity, (int)(waveid & 0xFFFF));
					velRegion.Volume = volume;
					velRegion.Pitch = pitch;
				}
			}

			return drumset;
		}
		InstrumentOscillatorInfo LoadOscillator() {
			var oscillator = new InstrumentOscillatorInfo();

			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				mareep.WriteWarning("IBNK: bad oscillator target '{0}' at 0x{1:X6}\n", (int)target, (mReader.Position - 1));
				return null;
			}

			oscillator.Target = target;
			mReader.Step(3); // alignment
			oscillator.Rate = mReader.ReadF32();
			var startTableOffset = mReader.ReadS32();
			var releaseTableOffset = mReader.ReadS32();
			oscillator.Width = mReader.ReadF32();
			oscillator.Base = mReader.ReadF32();

			if (startTableOffset != 0) {
				mReader.Goto(startTableOffset);
				InstrumentOscillatorTableMode mode;

				do {
					mode = (InstrumentOscillatorTableMode)mReader.ReadS16();

					if (!mode.IsDefined()) {
						mareep.WriteWarning("IBNK: bad oscillator table mode '{0}' at 0x{1:X6}\n", (int)mode, (mReader.Position - 2));
						break;
					}

					var time = mReader.ReadS16();
					var amount = mReader.ReadS16();

					oscillator.AddStartTable(mode, time, amount);
				} while ((int)mode <= 10);
			}

			if (releaseTableOffset != 0) {
				mReader.Goto(releaseTableOffset);
				InstrumentOscillatorTableMode mode;

				for (; ; ) {
					mode = (InstrumentOscillatorTableMode)mReader.ReadS16();

					if (!mode.IsDefined()) {
						mareep.WriteWarning("IBNK: bad oscillator table mode '{0}' at 0x{1:X6}\n", (int)mode, (mReader.Position - 2));
						break;
					}

					var time = mReader.ReadS16();
					var amount = mReader.ReadS16();

					oscillator.AddReleaseTable(mode, time, amount);
				} while ((int)mode <= 10) ;
			}

			return oscillator;
		}
		RandomInstrumentEffect LoadRandomEffect() {
			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				mareep.WriteWarning("IBNK: bad random effect target '{0}' at 0x{1:X6}\n", (byte)target, (mReader.Position - 1));
				return null;
			}

			mReader.Step(3); // alignment

			var randomBase = mReader.ReadF32();
			var randomDistance = mReader.ReadF32();

			return new RandomInstrumentEffect(target, randomBase, randomDistance);
		}
		SenseInstrumentEffect LoadSenseEffect() {
			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				mareep.WriteWarning("IBNK: bad sense effect target '{0}' at 0x{1:X6}\n", (byte)target, (mReader.Position - 1));
				return null;
			}

			var source = (SenseInstrumentEffectTrigger)mReader.Read8();

			if (!source.IsDefined()) {
				source = SenseInstrumentEffectTrigger.None;
			}

			var centerKey = mReader.Read8();

			if (centerKey > 127) {
				mareep.WriteWarning("IBNK: bad sense effect center key '{0}' at 0x{1:X6}\n", centerKey, (mReader.Position - 1));
				return null;
			}

			mReader.Step(1); // alignment

			var rangeLo = mReader.ReadF32();
			var rangeHi = mReader.ReadF32();

			return new SenseInstrumentEffect(target, source, centerKey, rangeLo, rangeHi);
		}

	}

	class BinaryBankSerializer : BinaryBankTransformer {

		aBinaryWriter mWriter;
		InstrumentBank mBank;
		IEnumerable<InstrumentOscillatorInfo> mOscTable;
		int mBankSize, mOscTableSize;

		public BinaryBankSerializer(aBinaryWriter writer) {
			mWriter = writer;
		}

		protected override InstrumentBank DoTransform(InstrumentBank obj) {
			if (obj == null) {
				return null;
			}

			mWriter.PushAnchor();

			WriteInit(obj);
			WriteHeader();
			WriteBank();
			WriteOscTable();
			WriteInstruments();

			mWriter.PopAnchor();

			return obj;
		}

		void WriteInit(InstrumentBank bank) {
			if (!CheckBank(bank)) {
				mareep.WriteError("IBNK: instrument bank is incompatible with BNK format.");
			}

			mBank = bank;
			mOscTable = mBank.GenerateOscillatorTable();
			mOscTableSize = mOscTable.Sum(osc => CalculateOscillatorSize(osc));
			mBankSize = (cDataStart + mOscTableSize + mBank.Sum(instrument => CalculateInstrumentSize(instrument)));
		}
		void WriteHeader() {
			mWriter.Write32(IBNK);
			mWriter.Keep();
			mWriter.WriteS32(mBankSize);
			mWriter.WriteS32(mBank.VirtualNumber);
			mWriter.WritePadding(32, 0);
		}
		void WriteBank() {
			var offset = (cDataStart + mOscTableSize);

			mWriter.Write32(BANK);

			for (var i = 0; i < cBankCount; ++i) {
				if (mBank[i] != null) {
					mWriter.WriteS32(offset);
					offset += CalculateInstrumentSize(mBank[i]);
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WritePadding(32, 0);
		}
		void WriteOscTable() {
			foreach (var oscillator in mOscTable) {
				var offset = ((int)mWriter.Position + 32);

				mWriter.Write8((byte)oscillator.Target);
				mWriter.WritePadding(4, 0);
				mWriter.WriteF32(oscillator.Rate);

				if (oscillator.StartTableCount > 0) {
					mWriter.WriteS32(offset);
					offset += mareep.RoundUp32B(oscillator.StartTableCount * 6);
				} else {
					mWriter.WriteS32(0);
				}

				if (oscillator.ReleaseTableCount > 0) {
					mWriter.WriteS32(offset);
					offset += mareep.RoundUp32B(oscillator.ReleaseTableCount * 6);
				} else {
					mWriter.WriteS32(0);
				}

				mWriter.WriteF32(oscillator.Width);
				mWriter.WriteF32(oscillator.Base);
				mWriter.WritePadding(32, 0);

				for (var i = 0; i < oscillator.StartTableCount; ++i) {
					var table = oscillator.GetStartTable(i);
					mWriter.WriteS16((short)table.mode);
					mWriter.WriteS16((short)table.time);
					mWriter.WriteS16((short)table.amount);
				}

				mWriter.WritePadding(32, 0);

				for (var i = 0; i < oscillator.ReleaseTableCount; ++i) {
					var table = oscillator.GetReleaseTable(i);
					mWriter.WriteS16((short)table.mode);
					mWriter.WriteS16((short)table.time);
					mWriter.WriteS16((short)table.amount);
				}

				mWriter.WritePadding(32, 0);
			}

			mWriter.WritePadding(32, 0);
		}
		void WriteInstruments() {
			for (var i = 0; i < cBankCount; ++i) {
				if (mBank[i] == null) {
					continue;
				}

				var type = mBank[i].Type;

				if (type == InstrumentType.Melodic) {
					var instrument = (mBank[i] as MelodicInstrument);

					if (instrument == null) {
						continue;
					}

					WriteMelodic(instrument);
				} else if (type == InstrumentType.DrumSet) {
					var drumset = (mBank[i] as DrumSet);

					if (drumset == null) {
						continue;
					}

					WriteDrumSet(drumset);
				}
			}
		}
		void WriteMelodic(MelodicInstrument instrument) {
			var offset = ((int)mWriter.Position + mareep.RoundUp16B(44 + 4 * instrument.Count));

			mWriter.Write32(INST);
			mWriter.WriteS32(0); // unused
			mWriter.WriteF32(instrument.Volume);
			mWriter.WriteF32(instrument.Pitch);

			for (var i = 0; i < cOscillatorCount; ++i) {
				if (i < instrument.OscillatorCount) {
					mWriter.WriteS32(CalculateOscillatorOffset(instrument.GetOscillatorAt(i)));
				} else {
					mWriter.WriteS32(0);
				}
			}

			var randomEffects = instrument.Effects.OfType<RandomInstrumentEffect>().ToArray();

			for (var i = 0; i < cRandomEffectCount; ++i) {
				if (i < randomEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			var senseEffects = instrument.Effects.OfType<SenseInstrumentEffect>().ToArray();

			for (var i = 0; i < cSenseEffectCount; ++i) {
				if (i < senseEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WriteS32(instrument.Count);

			foreach (var keyregion in instrument) {
				mWriter.WriteS32(offset);
				offset += CalculateKeyRegionSize(keyregion);
			}

			mWriter.WritePadding(16, 0);

			for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
				WriteRandomEffect(randomEffects[i]);
			}

			for (var i = 0; i < 2 && i < senseEffects.Length; ++i) {
				WriteSenseEffect(senseEffects[i]);
			}

			foreach (var keyregion in instrument) {
				WriteKeyRegion(keyregion);
			}

			mWriter.WritePadding(32, 0);
		}
		void WriteDrumSet(DrumSet drumset) {
			var offset = ((int)mWriter.Position + 1056);

			mWriter.Write32(PER2);
			mWriter.WriteS32(0); // unused
			mWriter.Write8s(new byte[128]); // unused

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.WriteS32(offset);
					offset += CalculatePercussionSize(percussion);
				} else {
					mWriter.WriteS32(0);
				}
			}

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.WriteS8((sbyte)(percussion.Pan * 127.0f));
				} else {
					mWriter.WriteS8(0);
				}
			}

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.Write16((ushort)percussion.Release);
				} else {
					mWriter.Write16(0);
				}
			}

			mWriter.WritePadding(32, 0);

			foreach (var percussion in drumset) {
				if (percussion == null) {
					continue;
				}

				WritePercussion(percussion);
			}

			mWriter.WritePadding(32, 0);
		}
		void WriteKeyRegion(MelodicKeyRegion keyregion) {
			var offset = ((int)mWriter.Position + mareep.RoundUp16B(8 + 4 * keyregion.Count));

			mWriter.Write8((byte)keyregion.Key);
			mWriter.WritePadding(4, 0);
			mWriter.WriteS32(keyregion.Count);

			foreach (var velregion in keyregion) {
				mWriter.WriteS32(offset);
				offset += 16;
			}

			mWriter.WritePadding(16, 0);

			foreach (var velregion in keyregion) {
				WriteVelocityRegion(velregion);
			}
		}
		void WritePercussion(Percussion percussion) {
			var offset = ((int)mWriter.Position + mareep.RoundUp16B(20 + 4 * percussion.Count));

			mWriter.WriteF32(percussion.Volume);
			mWriter.WriteF32(percussion.Pitch);

			var randomEffects = percussion.Effects.OfType<RandomInstrumentEffect>().ToArray();

			for (var i = 0; i < cRandomEffectCount; ++i) {
				if (i < randomEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WriteS32(percussion.Count);

			for (var i = 0; i < percussion.Count; ++i) {
				mWriter.WriteS32(offset);
				offset += 16;
			}

			mWriter.WritePadding(16, 0);

			for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
				WriteRandomEffect(randomEffects[i]);
			}

			foreach (var velregion in percussion) {
				WriteVelocityRegion(velregion);
			}
		}
		void WriteVelocityRegion(InstrumentVelocityRegion velregion) {
			mWriter.Write8((byte)velregion.Velocity);
			mWriter.WritePadding(4, 0);
			mWriter.Write32((uint)(velregion.WaveId & 0xFFFF));
			mWriter.WriteF32(velregion.Volume);
			mWriter.WriteF32(velregion.Pitch);
		}
		void WriteRandomEffect(RandomInstrumentEffect effect) {
			mWriter.Write8((byte)effect.Target);
			mWriter.WritePadding(4, 0);
			mWriter.WriteF32(effect.RandomBase);
			mWriter.WriteF32(effect.RandomDistance);
			mWriter.WritePadding(16, 0);
		}
		void WriteSenseEffect(SenseInstrumentEffect effect) {
			mWriter.Write8((byte)effect.Target);
			mWriter.Write8((byte)effect.Trigger);
			mWriter.Write8((byte)effect.CenterKey);
			mWriter.WritePadding(4, 0);
			mWriter.WriteF32(effect.RangeLo);
			mWriter.WriteF32(effect.RangeHi);
			mWriter.WritePadding(16, 0);
		}

		int CalculateInstrumentSize(IInstrument instrument) {
			if (instrument is MelodicInstrument) {
				var melodic = (instrument as MelodicInstrument);

				return mareep.RoundUp32B(
					mareep.RoundUp16B(44 + 4 * melodic.Count) +
					(16 * melodic.EffectCount) +
					melodic.Sum(region => CalculateKeyRegionSize(region))
				);
			} else if (instrument is DrumSet) {
				var drumset = (instrument as DrumSet);

				return mareep.RoundUp32B(1056 + drumset.Sum(percussion => percussion != null ? CalculatePercussionSize(percussion) : 0));
			}

			return 0;
		}
		int CalculateKeyRegionSize(MelodicKeyRegion region) {
			return (mareep.RoundUp16B(8 + 4 * region.Count) + (16 * region.Count));
		}
		int CalculatePercussionSize(Percussion percussion) {
			return (mareep.RoundUp16B(20 + 4 * percussion.Count) + (16 * percussion.EffectCount) + (16 * percussion.Count));
		}
		int CalculateOscillatorSize(InstrumentOscillatorInfo oscillator) {
			return (32 + mareep.RoundUp32B(oscillator.StartTableCount * 6) + mareep.RoundUp32B(oscillator.ReleaseTableCount * 6));
		}
		int CalculateOscillatorOffset(InstrumentOscillatorInfo oscillator) {
			var offset = cDataStart;

			foreach (var osc in mOscTable) {
				if (oscillator.IsEquivalentTo(osc)) {
					break;
				}
				offset += CalculateOscillatorSize(osc);
			}

			return offset;
		}

		const int cDataStart = 1024; // fixed size of the IBNK/BANK blocks

	}

	abstract class XmlBankTransformer : Transformer<InstrumentBank> {

		protected const string cBank = "bank";
		protected const string cVirtualNumber = "virtual-number";

		protected const string cInstrument = "instrument";
		protected const string cDrumSet = "drum-set";
		protected const string cProgram = "program";
		protected const string cVolume = "volume";
		protected const string cPitch = "pitch";
		protected const string cPan = "pan";
		protected const string cRelease = "release";
		protected const string cKey = "key";

		protected const string cKeyRegion = "key-region";
		protected const string cPercussion = "percussion";
		protected const string cVelRegion = "velocity-region";
		protected const string cVelocity = "velocity";
		protected const string cWaveId = "wave-id";

		protected const string cOscillator = "oscillator";
		protected const string cOscTarget = "target";
		protected const string cOscRate = "rate";
		protected const string cOscWidth = "width";
		protected const string cOscBase = "base";
		protected const string cOscStartTable = "start-table";
		protected const string cOscRelTable = "release-table";
		protected const string cOscLinear = "linear";
		protected const string cOscSquare = "square";
		protected const string cOscSquareRoot = "square-root";
		protected const string cOscSampleCell = "sample-cell";
		protected const string cOscTime = "time";
		protected const string cOscOffset = "offset";
		protected const string cOscLoop = "loop";
		protected const string cOscLoopDest = "dest";
		protected const string cOscHold = "hold";
		protected const string cOscStop = "stop";

		protected const string cEffect = "-effect";
		protected const string cEffectTarget = "target";
		protected const string cEffectRand = "random-effect";
		protected const string cEffectRandBase = "base";
		protected const string cEffectRandDistance = "distance";
		protected const string cEffectSense = "sense-effect";
		protected const string cEffectSenseTrigger = "trigger";
		protected const string cEffectSenseCenterKey = "center-key";
		protected const string cEffectSenseRangeLo = "range-lo";
		protected const string cEffectSenseRangeHi = "range-hi";

	}

	class XmlBankDeserializer : XmlBankTransformer {

		xElement mRootElement;

		public XmlBankDeserializer(xElement element) {
			mRootElement = element;
		}

		protected override InstrumentBank DoTransform(InstrumentBank obj) {
			if (obj != null) {
				return obj;
			}

			var xvirtualnumber = mRootElement.Attribute(cVirtualNumber);

			if (xvirtualnumber == null) {
				mareep.WriteError("XML: line #{0}: missing virtual number", mRootElement.LineNumber);
			}

			var virtualnumber = (xvirtualnumber | -1);

			if (virtualnumber < 0) {
				mareep.WriteError("XML: line #{0}: bad virtual number '{1}'.", xvirtualnumber.LineNumber, xvirtualnumber.Value);
			}

			var bank = new InstrumentBank(virtualnumber, 256);
			var warnings = mareep.WarningCount;

			foreach (var element in mRootElement.Elements()) {
				switch (element.Name) {
					case cInstrument: LoadMelodic(bank, element); break;
					case cDrumSet: LoadDrumSet(bank, element); break;
				}
			}

			if (mareep.WarningCount != warnings) {
				mareep.WriteError("XML: bad input xml");
			}

			return bank;
		}

		int LoadProgramNumber(xElement element) {
			var attribute = element.Attribute(cProgram);

			if (attribute == null) {
				mareep.WriteWarning("XML: line #{0}: missing program number\n", element.LineNumber);
				return -1;
			}

			var program = (attribute | -1);

			if (program < 0 || program > 255) {
				mareep.WriteWarning("XML: line #{0}: bad program number '{1}'\n", attribute.LineNumber, attribute.Value);
				return -1;
			}

			return program;
		}

		void LoadMelodic(InstrumentBank bank, xElement element) {
			var program = LoadProgramNumber(element);

			if (program < 0) {
				return;
			} else if (bank[program] != null) {
				mareep.WriteWarning("XML: line #{0}: duplicate program number '{1}'\n", element.LineNumber, program);
				return;
			}

			var instrument = LoadMelodic(element);

			if (instrument != null) {
				mareep.WriteMessage(
					"#{0,-3} INST: volume {1:F1} pitch {2:F1} oscillators {3} effects {4} key regions {5}\n",
					program,
					instrument.Volume,
					instrument.Pitch,
					instrument.OscillatorCount,
					instrument.EffectCount,
					instrument.Count
				);

				bank.Add(program, instrument);
			}
		}
		MelodicInstrument LoadMelodic(xElement xinstrument) {
			xAttribute attribute;

			var instrument = new MelodicInstrument();

			instrument.Volume = (xinstrument.Attribute(cVolume) | 1.0f);
			instrument.Pitch = (xinstrument.Attribute(cPitch) | 1.0f);

			foreach (var xoscillator in xinstrument.Elements(cOscillator)) {
				instrument.AddOscillator(LoadOscillator(xoscillator));
			}

			foreach (var xeffect in xinstrument.Elements().Where(e => e.Name.EndsWith(cEffect))) {
				var effect = LoadEffect(xeffect);

				if (effect == null) {
					continue;
				}

				instrument.AddEffect(effect);
			}

			foreach (var xkeyregion in xinstrument.Elements(cKeyRegion)) {
				attribute = xkeyregion.Attribute(cKey);
				var keynumber = attribute.AsKeyNumber(127);

				if (keynumber < 0 || keynumber > 127) {
					mareep.WriteWarning("XML: line #{0}: bad key number '{1}'\n", attribute.LineNumber, attribute.Value);
					continue;
				}

				var keyregion = instrument.AddRegion(keynumber);

				foreach (var xvelregion in xkeyregion.Elements(cVelRegion)) {
					attribute = xvelregion.Attribute(cVelocity);
					var velocity = attribute.AsInt32(127);

					if (velocity < 0 || velocity > 127) {
						mareep.WriteWarning("XML: line #{0}: bad velocity '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					attribute = xvelregion.Attribute(cWaveId);

					if (attribute == null) {
						mareep.WriteWarning("XML: line #{0}: missing wave id\n", xvelregion.LineNumber);
						continue;
					}

					var waveid = attribute.AsInt32();

					if (waveid < 0) {
						mareep.WriteWarning("XML: line #{0}: bad wave id '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					var volume = (xvelregion.Attribute(cVolume) | 1.0f);
					var pitch = (xvelregion.Attribute(cPitch) | 1.0f);

					keyregion.AddRegion(velocity, waveid, volume, pitch);
				}
			}

			return instrument;
		}

		void LoadDrumSet(InstrumentBank bank, xElement element) {
			var program = LoadProgramNumber(element);

			if (program < 0) {
				return;
			} else if (bank[program] != null) {
				mareep.WriteWarning("XML: line #{0}: duplicate program number '{1}'\n", element.LineNumber, program);
				return;
			}

			var drumset = LoadDrumSet(element);

			if (drumset != null) {
				mareep.WriteMessage(
					"#{0,-3} PER2: {1} percussion(s)\n",
					program,
					drumset.Count
				);

				bank.Add(program, drumset);
			}
		}
		DrumSet LoadDrumSet(xElement xdrumset) {
			xAttribute attribute;

			var drumset = new DrumSet();

			foreach (var xpercussion in xdrumset.Elements(cPercussion)) {
				attribute = xpercussion.Attribute(cKey);

				if (attribute == null) {
					mareep.WriteWarning("XML: line #{0}: missing key number\n", xpercussion.LineNumber);
					continue;
				}

				var keynumber = attribute.AsKeyNumber();

				if (keynumber < 0 || keynumber > 127) {
					mareep.WriteWarning("XML: line #{0}: bad key number '{0}'\n", attribute.LineNumber, attribute.Value);
					continue;
				}

				var percussion = drumset.AddPercussion(keynumber);

				percussion.Volume = (xpercussion.Attribute(cVolume) | 1.0f);
				percussion.Pitch = (xpercussion.Attribute(cPitch) | 1.0f);
				percussion.Pan = (xpercussion.Attribute(cPan) | 0.5f);

				foreach (var xeffect in xpercussion.Elements().Where(e => e.Name.EndsWith(cEffect))) {
					var effect = LoadEffect(xeffect);

					if (effect == null) {
						continue;
					}

					percussion.AddEffect(effect);
				}

				foreach (var xvelregion in xpercussion.Elements(cVelRegion)) {
					attribute = xvelregion.Attribute(cVelocity);
					var velocity = attribute.AsInt32(127);

					if (velocity < 0 || velocity > 127) {
						mareep.WriteWarning("XML: line #{0}: bad velocity '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					attribute = xvelregion.Attribute(cWaveId);

					if (attribute == null) {
						mareep.WriteWarning("XML: line #{0}: missing wave id\n", xvelregion.LineNumber);
						continue;
					}

					var waveid = attribute.AsInt32();

					if (waveid < 0) {
						mareep.WriteWarning("XML: line #{0}: bad wave id '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					var volume = (xvelregion.Attribute(cVolume) | 1.0f);
					var pitch = (xvelregion.Attribute(cPitch) | 1.0f);

					percussion.AddRegion(velocity, waveid, volume, pitch);
				}
			}

			return drumset;
		}
		
		InstrumentOscillatorInfo LoadOscillator(xElement xoscillator) {
			var oscillator = new InstrumentOscillatorInfo();
			var xtarget = xoscillator.Attribute("target");

			if (xtarget == null) {
				mareep.WriteError("XML: line #{0}: missing oscillator target.", xoscillator.LineNumber);
			}

			var target = xtarget.AsEnum((InstrumentEffectTarget)(-1));

			if (!target.IsDefined()) {
				mareep.WriteError("XML: line #{0}: bad oscillator target '{1}'.", xtarget.LineNumber, xtarget.Value);
			}

			oscillator.Target = target;
			oscillator.Rate = (xoscillator.Attribute(cOscRate) | 1.0f);
			oscillator.Width = (xoscillator.Attribute(cOscWidth) | 1.0f);
			oscillator.Base = (xoscillator.Attribute(cOscBase) | 0.0f);

			var xtable = xoscillator.Element(cOscStartTable);

			if (xtable != null) {
				foreach (var table in LoadOscillatorTable(xtable)) {
					oscillator.AddStartTable(table.mode, table.time, table.amount);
				}
			}

			var xreltable = xoscillator.Element(cOscRelTable);

			if (xreltable != null) {
				foreach (var table in LoadOscillatorTable(xreltable)) {
					oscillator.AddReleaseTable(table.mode, table.time, table.amount);
				}
			}

			return oscillator;
		}
		IEnumerable<InstrumentOscillatorTable> LoadOscillatorTable(xElement xtable) {
			var tables = new List<InstrumentOscillatorTable>();

			foreach (var child in xtable) {
				var table = new InstrumentOscillatorTable();

				switch (child.Name) {
					case cOscLinear: {
						table.mode = InstrumentOscillatorTableMode.Linear;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSquare: {
						table.mode = InstrumentOscillatorTableMode.Square;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSquareRoot: {
						table.mode = InstrumentOscillatorTableMode.SquareRoot;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSampleCell: {
						table.mode = InstrumentOscillatorTableMode.SampleCell;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscLoop: {
						table.mode = InstrumentOscillatorTableMode.Loop;
						table.time = (child.Attribute(cOscLoopDest) | 0);
						break;
					}
					case cOscHold: table.mode = InstrumentOscillatorTableMode.Hold; break;
					case cOscStop: table.mode = InstrumentOscillatorTableMode.Stop; break;
					default: mareep.WriteError("XML: unknown oscillator table mode '{0}'.", child.Name); break;
				}

				tables.Add(table);
			}

			return tables;
		}

		InstrumentEffect LoadEffect(xElement xeffect) {
			switch (xeffect.Name) {
				case cEffectRand: return LoadRandomEffect(xeffect);
				case cEffectSense: return LoadSenseEffect(xeffect);
			}

			return null;
		}
		RandomInstrumentEffect LoadRandomEffect(xElement xeffect) {
			var target = xeffect.Attribute(cEffectTarget).AsEnum(InstrumentEffectTarget.Volume);
			var randomBase = (xeffect.Attribute(cEffectRandBase) | 1.0f);
			var randomDistance = (xeffect.Attribute(cEffectRandDistance) | 0.0f);

			return new RandomInstrumentEffect(target, randomBase, randomDistance);
		}
		SenseInstrumentEffect LoadSenseEffect(xElement xeffect) {
			var target = xeffect.Attribute(cEffectTarget).AsEnum(InstrumentEffectTarget.Volume);
			var trigger = xeffect.Attribute(cEffectSenseTrigger).AsEnum(SenseInstrumentEffectTrigger.Key);
			var centerKey = xeffect.Attribute(cEffectSenseCenterKey).AsKeyNumber(127);
			var rangeLo = (xeffect.Attribute(cEffectSenseRangeLo) | 0.0f);
			var rangeHi = (xeffect.Attribute(cEffectSenseRangeHi) | 1.0f);

			return new SenseInstrumentEffect(target, trigger, centerKey, rangeLo, rangeHi);
		}
		
	}

	class XmlBankSerializer : XmlBankTransformer {

		XmlWriter mWriter;
		InstrumentBank mBank;

		public XmlBankSerializer(XmlWriter writer) {
			mWriter = writer;
		}

		protected override InstrumentBank DoTransform(InstrumentBank obj) {
			if (obj == null) {
				return null;
			}

			mBank = obj;

			WriteBank();
			mWriter.Flush();

			return obj;
		}

		void WriteBank() {
			mWriter.WriteStartElement(cBank);
			mWriter.WriteAttributeString(cVirtualNumber, mBank.VirtualNumber);

			for (var program = 0; program < mBank.Capacity; ++program) {
				if (mBank[program] == null) {
					continue;
				}

				switch (mBank[program].Type) {
					case InstrumentType.Melodic: WriteMelodic((mBank[program] as MelodicInstrument), program); break;
					case InstrumentType.DrumSet: WriteDrumSet((mBank[program] as DrumSet), program); break;
				}
			}

			mWriter.WriteEndElement();
		}
		void WriteMelodic(MelodicInstrument instrument, int program) {
			mWriter.WriteStartElement(cInstrument);
			mWriter.WriteAttributeString(cProgram, program);

			foreach (var oscillator in instrument.Oscillators) {
				WriteOscillator(oscillator);
			}

			foreach (var effect in instrument.Effects) {
				WriteEffect(effect);
			}

			foreach (var keyregion in instrument) {
				mWriter.WriteStartElement(cKeyRegion);

				if (instrument.Count > 1 || keyregion.Key != 127) {
					mWriter.WriteAttributeString(cKey, mareep.ConvertKey(keyregion.Key));
				}

				foreach (var velregion in keyregion) {
					mWriter.WriteStartElement(cVelRegion);

					if (keyregion.Count > 1 || keyregion[0].Velocity < 127) {
						mWriter.WriteAttributeString(cVelocity, velregion.Velocity);
					}

					mWriter.WriteAttributeString(cWaveId, velregion.WaveId);

					if (velregion.Volume != 1.0f) {
						mWriter.WriteAttributeString(cVolume, velregion.Volume);
					}

					if (velregion.Pitch != 1.0f) {
						mWriter.WriteAttributeString(cPitch, velregion.Pitch);
					}

					mWriter.WriteEndElement();
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}
		void WriteDrumSet(DrumSet drumset, int program) {
			mWriter.WriteStartElement(cDrumSet);
			mWriter.WriteAttributeString(cProgram, program);

			foreach (var percussion in drumset.Percussions) {
				if (percussion == null) {
					continue;
				}

				mWriter.WriteStartElement(cPercussion);
				mWriter.WriteAttributeString(cKey, mareep.ConvertKey(percussion.Key));

				if (percussion.Volume != 1.0f) {
					mWriter.WriteAttributeString(cVolume, percussion.Volume);
				}

				if (percussion.Pitch != 1.0f) {
					mWriter.WriteAttributeString(cPitch, percussion.Pitch);
				}

				if (percussion.Pan != 0.5f) {
					mWriter.WriteAttributeString(cPan, percussion.Pan);
				}

				foreach (var effect in percussion.Effects) {
					WriteEffect(effect);
				}

				foreach (var velregion in percussion) {
					mWriter.WriteStartElement(cVelRegion);

					if (percussion.Count > 1 || percussion[0].Velocity < 127) {
						mWriter.WriteAttributeString(cVelocity, velregion.Velocity);
					}

					mWriter.WriteAttributeString(cWaveId, velregion.WaveId);

					if (velregion.Volume != 1.0f) {
						mWriter.WriteAttributeString(cVolume, velregion.Volume);
					}

					if (velregion.Pitch != 1.0f) {
						mWriter.WriteAttributeString(cPitch, velregion.Pitch);
					}

					mWriter.WriteEndElement();
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}
		void WriteOscillator(InstrumentOscillatorInfo oscillator) {
			mWriter.WriteStartElement(cOscillator);
			mWriter.WriteAttributeString(cOscTarget, oscillator.Target.ToLowerString());
			mWriter.WriteAttributeString(cOscRate, oscillator.Rate);
			mWriter.WriteAttributeString(cOscWidth, oscillator.Width);
			mWriter.WriteAttributeString(cOscBase, oscillator.Base);

			if (oscillator.StartTableCount > 0) {
				mWriter.WriteStartElement(cOscStartTable);

				for (var i = 0; i < oscillator.StartTableCount; ++i) {
					WriteOscillatorTable(oscillator.GetStartTable(i));
				}

				mWriter.WriteEndElement();
			}

			if (oscillator.ReleaseTableCount > 0) {
				mWriter.WriteStartElement(cOscRelTable);

				for (var i = 0; i < oscillator.ReleaseTableCount; ++i) {
					WriteOscillatorTable(oscillator.GetReleaseTable(i));
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}
		void WriteOscillatorTable(InstrumentOscillatorTable table) {
			string name;

			switch (table.mode) {
				case InstrumentOscillatorTableMode.Linear: name = cOscLinear; break;
				case InstrumentOscillatorTableMode.Square: name = cOscSquare; break;
				case InstrumentOscillatorTableMode.SquareRoot: name = cOscSquareRoot; break;
				case InstrumentOscillatorTableMode.SampleCell: name = cOscSampleCell; break;
				case InstrumentOscillatorTableMode.Loop: name = cOscLoop; break;
				case InstrumentOscillatorTableMode.Hold: name = cOscHold; break;
				case InstrumentOscillatorTableMode.Stop: name = cOscStop; break;
				default: return;
			}

			mWriter.WriteStartElement(name);

			switch (table.mode) {
				case InstrumentOscillatorTableMode.Loop: {
					mWriter.WriteAttributeString(cOscLoopDest, table.time);
					break;
				}
				case InstrumentOscillatorTableMode.Hold: break;
				case InstrumentOscillatorTableMode.Stop: break;
				default: {
					mWriter.WriteAttributeString(cOscTime, table.time);
					mWriter.WriteAttributeString(cOscOffset, table.amount);
					break;
				}
			}

			mWriter.WriteEndElement();
		}
		void WriteEffect(InstrumentEffect effect) {
			if (effect is RandomInstrumentEffect) {
				WriteRandomEffect(effect as RandomInstrumentEffect);
			} else if (effect is SenseInstrumentEffect) {
				WriteSenseEffect(effect as SenseInstrumentEffect);
			}
		}
		void WriteRandomEffect(RandomInstrumentEffect effect) {
			mWriter.WriteStartElement(cEffectRand);
			mWriter.WriteAttributeString(cEffectTarget, effect.Target.ToLowerString());
			mWriter.WriteAttributeString(cEffectRandBase, effect.RandomBase);
			mWriter.WriteAttributeString(cEffectRandDistance, effect.RandomDistance);
			mWriter.WriteEndElement();
		}
		void WriteSenseEffect(SenseInstrumentEffect effect) {
			mWriter.WriteStartElement(cEffectSense);
			mWriter.WriteAttributeString(cEffectTarget, effect.Target.ToLowerString());
			mWriter.WriteAttributeString(cEffectSenseTrigger, effect.Trigger.ToLowerString());

			if (effect.CenterKey < 127) {
				mWriter.WriteAttributeString(cEffectSenseCenterKey, mareep.ConvertKey(effect.CenterKey));
			}

			mWriter.WriteAttributeString(cEffectSenseRangeLo, effect.RangeLo);
			mWriter.WriteAttributeString(cEffectSenseRangeHi, effect.RangeHi);
			mWriter.WriteEndElement();
		}
	}

}
