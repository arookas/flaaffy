
using arookas.IO.Binary;
using arookas.Xml;
using System;
using System.Linq;
using System.Xml;

namespace arookas {

	[Performer(Action.Shock)]
	class ShockPerformer : InputOutputPerformer {

		public override void Perform() {
			InstrumentBank bank = null;

			mareep.WriteMessage("Opening input file \"{0}\"...\n", mInputFile);

			using (var stream = mareep.OpenFile(mInputFile)) {
				mareep.WriteMessage("Loading bank...\n");

				switch (mInputFormat) {
					case IOFormat.Xml: {
						var xml = CreateXmlInput(stream);
						bank = Xml.LoadBank(xml.Root);
						break;
					}
					case IOFormat.LittleBinary: {
						var reader = CreateLittleBinaryInput(stream);
						bank = Binary.LoadBank(reader);
						break;
					}
					case IOFormat.BigBinary: {
						var reader = CreateBigBinaryInput(stream);
						bank = Binary.LoadBank(reader);
						break;
					}
					default: mareep.WriteError("Unimplemented input format \"{0}\".\n", mInputFormat); break;
				}
			}

			if (bank == null) {
				mareep.WriteError("Failed to load bank.\n");
			}

			mareep.WriteMessage("Creating output file \"{0}\"...\n", mOutputFile);

			using (var stream = mareep.CreateFile(mOutputFile)) {
				mareep.WriteMessage("Saving bank...\n");

				switch (mOutputFormat) {
					case IOFormat.Xml: {
						using (var writer = CreateXmlOutput(stream)) {
							writer.WriteStartDocument();
							Xml.SaveBank(bank, writer);
							writer.WriteEndDocument();
						}
						break;
					}
					case IOFormat.LittleBinary: {
						var writer = CreateLittleBinaryOutput(stream);
						Binary.SaveBank(bank, writer);
						break;
					}
					case IOFormat.BigBinary: {
						var writer = CreateBigBinaryOutput(stream);
						Binary.SaveBank(bank, writer);
						break;
					}
					default: mareep.WriteError("Unimplemented output format \"{0}\".\n", mOutputFormat); break;
				}
			}
		}

		public static class Xml {

			static InstrumentEffect LoadEffect(xElement effectElem, string warningPrefix) {
				var target = effectElem.Attribute("target").AsEnum(InstrumentEffectTarget.Volume);

				if (effectElem.Name.Equals("random-effect", StringComparison.InvariantCultureIgnoreCase)) {
					var randomBase = (effectElem.Attribute("base") | 1.0f);
					var randomDistance = (effectElem.Attribute("distance") | 0.0f);

					return new RandomInstrumentEffect(target, randomBase, randomDistance);
				} else if (effectElem.Name.Equals("sense-effect", StringComparison.InvariantCultureIgnoreCase)) {
					SenseInstrumentEffectSource source;

					if (!Enum.TryParse((effectElem.Attribute("source") | "key"), true, out source)) {
						mareep.WriteWarning("{0}: Bad source in sense effect. Defaulting to 'none'.\n", warningPrefix);
						source = SenseInstrumentEffectSource.None;
					}

					var weight = (effectElem.Attribute("weight") | 127);

					if (weight < 0 || weight > 127) {
						mareep.WriteWarning("{0}: Bad weight value '{1}' in sense effect. Skipping...\n", warningPrefix, weight);
						return null;
					}

					var rangeLo = (effectElem.Attribute("range-lo") | 0.0f);
					var rangeHi = (effectElem.Attribute("range-hi") | 1.0f);

					return new SenseInstrumentEffect(target, source, weight, rangeLo, rangeHi);
				}

				mareep.WriteWarning("{0}: Unknown effect type \"{1}\". Skipping...\n", warningPrefix, effectElem.Name);
				return null;
			}
			static MelodicInstrument LoadMelodic(xElement instrumentElem, ref int lastProgram, out int program) {
				var programElem = instrumentElem.Attribute("program");
				program = (programElem != null ? (programElem | 0) : (lastProgram + 1));

				if (program < 0 || program > 255) {
					mareep.WriteWarning("Bad program number '{0}' in basic instrument. Skipping...\n", program);
					return null;
				}

				lastProgram = program;

				var instrument = new MelodicInstrument();

				instrument.Volume = (instrumentElem.Attribute("volume") | 1.0f);
				instrument.Pitch = (instrumentElem.Attribute("pitch") | 1.0f);

				foreach (var effectElem in instrumentElem.Elements().Where(e => e.Name.EndsWith("-effect", StringComparison.InvariantCultureIgnoreCase))) {
					var effect = LoadEffect(effectElem, String.Format("Basic #{0}", program));

					if (effect == null) {
						continue;
					}

					instrument.AddEffect(effect);
				}

				foreach (var keyregionElem in instrumentElem.Elements("key-region")) {
					var keyAttr = keyregionElem.Attribute("key");
					var key = 127;

					if (keyAttr != null) {
						key = mareep.ConvertKey(keyAttr.Value);

						if (key < 0) {
							key = (int)keyAttr;
						}

						if (key < 0 || key > 127) {
							mareep.WriteWarning("Bad key '{0}' in melodic instrument.", keyAttr.Value);
						}
					}

					var keyregion = instrument.AddRegion(key);

					foreach (var velocityregionElem in keyregionElem.Elements("velocity-region")) {
						var velocity = (velocityregionElem.Attribute("velocity") | 127);

						if (velocity < 0 || velocity > 127) {
							continue;
						}

						var waveid = (velocityregionElem.Attribute("wave-id") | 0);

						if (waveid < 0) {
							continue;
						}

						var volume = (velocityregionElem.Attribute("volume") | 1.0f);
						var pitch = (velocityregionElem.Attribute("pitch") | 1.0f);

						keyregion.AddRegion(velocity, waveid, volume, pitch);
					}
				}

				return instrument;
			}
			static DrumSet LoadDrumSet(xElement drumsetElem, ref int lastProgram, out int program) {
				var programElem = drumsetElem.Attribute("program");
				program = (programElem != null ? (programElem | 0) : ++lastProgram);

				if (program < 0 || program > 255) {
					mareep.WriteWarning("Bad program number '{0}' in drum set. Skipping...\n", program);
					return null;
				}

				var drumset = new DrumSet();

				foreach (var percussionElem in drumsetElem.Elements("percussion")) {
					var keyAttr = percussionElem.Attribute("key");

					if (keyAttr == null) {
						mareep.WriteWarning("Drum set #{0}: Missing key number in percussion. Skipping...\n", program);
						continue;
					}

					var key = mareep.ConvertKey(keyAttr.Value);

					if (key < 0) {
						key = (int)keyAttr;
					}

					if (key < 0 || key > 127) {
						mareep.WriteWarning("Drum set #{0}: Bad key number '{0}' in percussion. Skipping...\n", program, keyAttr.Value);
						continue;
					}

					var percussion = drumset.AddPercussion(key);

					percussion.Volume = (percussionElem.Attribute("volume") | 1.0f);
					percussion.Pitch = (percussionElem.Attribute("pitch") | 1.0f);
					percussion.Pan = (percussionElem.Attribute("pan") | 0.5f);

					foreach (var effectElem in percussionElem.Elements().Where(e => e.Name.EndsWith("-effect", StringComparison.InvariantCultureIgnoreCase))) {
						var effect = LoadEffect(effectElem, String.Format("Drum set #{0} (percussion #{1})", program, percussion));

						if (effect == null) {
							continue;
						}

						percussion.AddEffect(effect);
					}

					foreach (var velocityregionElem in percussionElem.Elements("velocity-region")) {
						var velocity = (velocityregionElem.Attribute("velocity") | 127);

						if (velocity < 0 || velocity > 127) {
							mareep.WriteWarning("Drum set #{0}: Bad velocity '{1}' in percussion #{2}. Skipping...\n", program, velocity, key);
							drumset.RemovePercussionAt(key);
							break;
						}

						var waveid = (velocityregionElem.Attribute("wave-id") | 0);

						if (waveid < 0) {
							mareep.WriteWarning("Drum set #{0}: Bad wave id '{1}' in percussion #{2}. Skipping...\n", program, waveid, key);
							drumset.RemovePercussionAt(key);
							break;
						}

						var volume = (velocityregionElem.Attribute("volume") | 1.0f);
						var pitch = (velocityregionElem.Attribute("pitch") | 1.0f);

						percussion.AddRegion(velocity, waveid, volume, pitch);
					}
				}

				return drumset;
			}
			public static InstrumentBank LoadBank(xElement root) {
				var virtualNumber = (root.Attribute("virtual-number") | 0);

				if (virtualNumber < 0) {
					mareep.WriteError("Bad virtual number in bank.\n");
				}

				var bank = new InstrumentBank(virtualNumber, 256);
				var lastProgram = -1;
				int program;

				foreach (var element in root.Elements()) {
					if (element.Name.Equals("instrument", StringComparison.InvariantCultureIgnoreCase)) {
						var instrument = LoadMelodic(element, ref lastProgram, out program);

						if (instrument != null) {
							mareep.WriteMessage(
								"#{0,-3} Basic instrument, volume {1:F1} pitch {2:F1}, {3} effect(s), {4} key regions(s)\n",
								program,
								instrument.Volume,
								instrument.Pitch,
								instrument.EffectCount,
								instrument.Count
							);

							bank.Add(program, instrument);
						}
					} else if (element.Name.Equals("drum-set", StringComparison.InvariantCultureIgnoreCase)) {
						var drumset = LoadDrumSet(element, ref lastProgram, out program);

						if (drumset != null) {
							mareep.WriteMessage(
								"#{0,-3} Drum set, {1} percussion(s)\n",
								program,
								drumset.Count
							);

							bank.Add(program, drumset);
						}
					}
				}

				return bank;
			}

			static void SaveEffect(InstrumentEffect effect, XmlWriter writer) {
				if (effect is RandomInstrumentEffect) {
					var rand = (effect as RandomInstrumentEffect);

					writer.WriteStartElement("random-effect");
					writer.WriteAttributeString("target", rand.Target.ToString().ToLowerInvariant());
					writer.WriteAttributeString("base", rand.RandomBase.ToString("R"));
					writer.WriteAttributeString("distance", rand.RandomDistance.ToString("R"));
					writer.WriteEndElement();
				} else if (effect is SenseInstrumentEffect) {
					var sense = (effect as SenseInstrumentEffect);

					writer.WriteStartElement("sense-effect");
					writer.WriteAttributeString("target", sense.Target.ToString().ToLowerInvariant());
					writer.WriteAttributeString("source", sense.Source.ToString().ToLowerInvariant());

					if (sense.Weight > 0 && sense.Weight < 127) {
						writer.WriteAttributeString("weight", sense.Weight.ToString());
					}

					writer.WriteAttributeString("range-lo", sense.RangeLo.ToString("R"));
					writer.WriteAttributeString("range-hi", sense.RangeHi.ToString("R"));
					writer.WriteEndElement();
				}
			}
			static void SaveMelodic(MelodicInstrument instrument, int program, XmlWriter writer) {
				if (instrument == null) {
					mareep.WriteWarning("#{0}: Instrument type does not match object type. Skipping...\n", program);
					return;
				}

				writer.WriteStartElement("instrument");
				writer.WriteAttributeString("program", program.ToString());

				foreach (var effect in instrument.Effects) {
					SaveEffect(effect, writer);
				}

				foreach (var keyregion in instrument) {
					writer.WriteStartElement("key-region");

					if (instrument.Count > 1 || instrument[0].Key < 127) {
						writer.WriteAttributeString("key", keyregion.Key.ToString());
					}

					foreach (var velregion in keyregion) {
						writer.WriteStartElement("velocity-region");

						if (keyregion.Count > 1 || keyregion[0].Velocity < 127) {
							writer.WriteAttributeString("velocity", velregion.Velocity.ToString());
						}

						writer.WriteAttributeString("wave-id", velregion.WaveId.ToString());

						if (velregion.Volume != 1.0f) {
							writer.WriteAttributeString("volume", velregion.Volume.ToString("R"));
						}

						if (velregion.Pitch != 1.0f) {
							writer.WriteAttributeString("pitch", velregion.Pitch.ToString("R"));
						}

						writer.WriteEndElement();
					}

					writer.WriteEndElement();
				}

				writer.WriteEndElement();
			}
			static void SaveDrumSet(DrumSet drumset, int program, XmlWriter writer) {
				if (drumset == null) {
					mareep.WriteWarning("#{0}: Instrument type does not match object type. Skipping...\n", program);
					return;
				}

				writer.WriteStartElement("drum-set");
				writer.WriteAttributeString("program", program.ToString());

				foreach (var percussion in drumset.Percussions) {
					if (percussion == null) {
						continue;
					}

					writer.WriteStartElement("percussion");
					writer.WriteAttributeString("key", percussion.Key.ToString());

					if (percussion.Volume != 1.0f) {
						writer.WriteAttributeString("volume", percussion.Volume.ToString("R"));
					}

					if (percussion.Pitch != 1.0f) {
						writer.WriteAttributeString("pitch", percussion.Pitch.ToString("R"));
					}

					if (percussion.Pan != 0.5f) {
						writer.WriteAttributeString("pan", percussion.Pan.ToString("R"));
					}

					foreach (var effect in percussion.Effects) {
						SaveEffect(effect, writer);
					}

					foreach (var velregion in percussion) {
						writer.WriteStartElement("velocity-region");

						if (percussion.Count > 1 || percussion[0].Velocity < 127) {
							writer.WriteAttributeString("velocity", velregion.Velocity.ToString());
						}

						writer.WriteAttributeString("wave-id", velregion.WaveId.ToString());

						if (velregion.Volume != 1.0f) {
							writer.WriteAttributeString("volume", velregion.Volume.ToString("R"));
						}

						if (velregion.Pitch != 1.0f) {
							writer.WriteAttributeString("pitch", velregion.Pitch.ToString("R"));
						}
					}

					writer.WriteEndElement();
				}

				writer.WriteEndElement();
			}
			public static void SaveBank(InstrumentBank bank, XmlWriter writer) {
				writer.WriteStartElement("bank");

				for (var program = 0; program < bank.Capacity; ++program) {
					if (bank[program] == null) {
						continue;
					}

					switch (bank[program].Type) {
						case InstrumentType.Melodic: SaveMelodic((bank[program] as MelodicInstrument), program, writer); break;
						case InstrumentType.DrumSet: SaveDrumSet((bank[program] as DrumSet), program, writer); break;
					}
				}

				writer.WriteEndElement();
			}

		}

		public static class Binary {

			static MelodicInstrument LoadMelodic(aBinaryReader reader) {
				var instrument = new MelodicInstrument();

				reader.Step(4); // unused

				instrument.Volume = reader.ReadF32();
				instrument.Pitch = reader.ReadF32();

				var oscillatorOffsets = reader.ReadS32s(2);
				var randomEffectOffsets = reader.ReadS32s(2);
				var senseEffectOffsets = reader.ReadS32s(2);
				var keyRegionOffsets = reader.ReadS32s(reader.ReadS32());

				mareep.WriteMessage(
					"INST: volume {0:F1} pitch {1:F1} effects: {2} key regions: {3}\n",
					instrument.Volume,
					instrument.Pitch,
					(randomEffectOffsets.Count(offset => offset != 0) + senseEffectOffsets.Count(offset => offset != 0)),
					keyRegionOffsets.Length
				);

				foreach (var offset in randomEffectOffsets) {
					if (offset == 0) {
						continue;
					}

					reader.Goto(offset);

					var target = (InstrumentEffectTarget)reader.Read8();

					if (!target.IsDefined()) {
						mareep.WriteWarning("Bad random effect target '{0}' at 0x{1:X6}\n", (byte)target, (reader.Position - 1));
						return null;
					}

					reader.Step(3); // alignment

					var randomBase = reader.ReadF32();
					var randomDistance = reader.ReadF32();

					instrument.AddEffect(new RandomInstrumentEffect(target, randomBase, randomDistance));
				}

				foreach (var offset in senseEffectOffsets) {
					if (offset == 0) {
						continue;
					}

					reader.Goto(offset);

					var target = (InstrumentEffectTarget)reader.Read8();

					if (!target.IsDefined()) {
						mareep.WriteWarning("Bad sense effect target '{0}' at 0x{1:X6}\n", (byte)target, (reader.Position - 1));
						return null;
					}

					var source = (SenseInstrumentEffectSource)reader.Read8();

					if (!source.IsDefined()) {
						source = SenseInstrumentEffectSource.None;
					}

					var weight = reader.Read8();

					if (weight > 127) {
						mareep.WriteWarning("Bad sense effect weight '{0}' at 0x{1:X6}\n", weight, (reader.Position - 1));
						return null;
					}

					reader.Step(1); // alignment

					var rangeLo = reader.ReadF32();
					var rangeHi = reader.ReadF32();

					instrument.AddEffect(new SenseInstrumentEffect(target, source, weight, rangeLo, rangeHi));
				}

				foreach (var keyRegionOffset in keyRegionOffsets) {
					reader.Goto(keyRegionOffset);

					var key = reader.Read8();

					if (key > 127) {
						mareep.WriteWarning("Bad key region key number '{0}' at 0x{1:X6}\n", key, (reader.Position - 1));
						return null;
					}

					reader.Step(3); // alignment

					var velRegionOffsets = reader.ReadS32s(reader.ReadS32());
					var keyRegion = instrument.AddRegion(key);

					foreach (var velRegionOffset in velRegionOffsets) {
						reader.Goto(velRegionOffset);

						var velocity = reader.Read8();

						if (velocity > 127) {
							mareep.WriteWarning("Bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (reader.Position - 1));
							return null;
						}

						reader.Step(3); // alignment

						var waveid = reader.Read32();
						var volume = reader.ReadF32();
						var pitch = reader.ReadF32();

						var velRegion = keyRegion.AddRegion(velocity, (int)(waveid & 0xFFFF));
						velRegion.Volume = volume;
						velRegion.Pitch = pitch;
					}
				}

				return instrument;
			}
			static DrumSet LoadDrumSet(aBinaryReader reader, int version) {
				var drumset = new DrumSet();

				reader.Step(4); // unused
				reader.Step(0x80); // unused 128-item byte array

				var percussionOffsets = reader.ReadS32s(128);
				sbyte[] panTable = null;
				ushort[] releaseTable = null;

				if (version == 2) {
					panTable = reader.ReadS8s(128);
					releaseTable = reader.Read16s(128);
				}

				for (var i = 0; i < 128; ++i) {
					if (percussionOffsets[i] == 0) {
						continue;
					}

					reader.Goto(percussionOffsets[i]);

					var percussion = drumset.AddPercussion(i);

					percussion.Volume = reader.ReadF32();
					percussion.Pitch = reader.ReadF32();
					var randomEffectOffsets = reader.ReadS32s(2);
					var velRegionOffsets = reader.ReadS32s(reader.ReadS32());

					if (version == 2) {
						percussion.Pan = ((float)panTable[i] / 127.0f);
						percussion.Release = releaseTable[i];
					}

					foreach (var offset in randomEffectOffsets) {
						if (offset == 0) {
							continue;
						}

						reader.Goto(offset);

						var target = (InstrumentEffectTarget)reader.Read8();

						if (!target.IsDefined()) {
							mareep.WriteWarning("Bad random effect target '{0}' at 0x{1:X6}\n", (byte)target, (reader.Position - 1));
							return null;
						}

						reader.Step(3); // alignment

						var randomBase = reader.ReadF32();
						var randomDistance = reader.ReadF32();

						percussion.AddEffect(new RandomInstrumentEffect(target, randomBase, randomDistance));
					}

					foreach (var velRegionOffset in velRegionOffsets) {
						reader.Goto(velRegionOffset);

						var velocity = reader.Read8();

						if (velocity > 127) {
							mareep.WriteWarning("Bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (reader.Position - 1));
							return null;
						}

						reader.Step(3); // alignment

						var waveid = reader.Read32();
						var volume = reader.ReadF32();
						var pitch = reader.ReadF32();

						var velRegion = percussion.AddRegion(velocity, (int)(waveid & 0xFFFF));
						velRegion.Volume = volume;
						velRegion.Pitch = pitch;
					}
				}

				return drumset;
			}
			public static InstrumentBank LoadBank(aBinaryReader reader) {
				reader.Keep();
				reader.PushAnchor();

				if (reader.Read32() != IBNK) {
					mareep.WriteError("Could not find IBNK.\n");
				}

				var size = reader.ReadS32();
				var virtualNumber = reader.ReadS32();

				mareep.WriteMessage("IBNK found, size {0:F1} KB, virtual number {1}\n", ((double)size / 1024.0d), virtualNumber);

				var bank = new InstrumentBank(virtualNumber, 256);

				reader.Goto(32);

				if (reader.Read32() != BANK) {
					mareep.WriteError("Could not find BANK.\n");
				}

				var instrumentOffsets = reader.ReadS32s(240);

				mareep.WriteMessage("BANK found, {0} instrument(s)\n", instrumentOffsets.Count(offset => offset != 0));

				for (var i = 0; i < 240; ++i) {
					if (instrumentOffsets[i] == 0) {
						continue;
					}

					reader.Goto(instrumentOffsets[i]);

					var instrumentType = reader.Read32();
					IInstrument instrument = null;

					mareep.WriteMessage("#{0,-3} ", i);

					switch (instrumentType) {
						case INST: instrument = LoadMelodic(reader); break;
						case PERC: instrument = LoadDrumSet(reader, 1); break;
						case PER2: instrument = LoadDrumSet(reader, 2); break;
					}

					if (instrument == null) {
						mareep.WriteMessage("(null)\n");
						continue;
					}

					bank.Add(i, instrument);
				}

				reader.PopAnchor();
				reader.Back();

				return bank;
			}

			static int RoundUp16B(int value) {
				return ((value + 15) & ~15);
			}
			static int RoundUp32B(int value) {
				return ((value + 31) & ~31);
			}

			static int CalculatePercussionSize(Percussion percussion) {
				return (RoundUp16B(20 + 4 * percussion.Count) + (16 * percussion.EffectCount) + (16 * percussion.Count));
			}
			static int CalculateKeyRegionSize(MelodicKeyRegion region) {
				return (RoundUp16B(8 + 4 * region.Count) + (16 * region.Count));
			}
			static int CalculateInstrumentSize(IInstrument instrument) {
				if (instrument is MelodicInstrument) {
					var melodic = (instrument as MelodicInstrument);

					return RoundUp32B(
						RoundUp16B(44 + 4 * melodic.Count) +
						(16 * melodic.EffectCount) +
						melodic.Sum(region => CalculateKeyRegionSize(region))
					);
				} else if (instrument is DrumSet) {
					var drumset = (instrument as DrumSet);

					return RoundUp32B(1056 + drumset.Sum(percussion => percussion != null ? CalculatePercussionSize(percussion) : 0));
				}

				return 0;
			}
			static int CalculateBankSize(InstrumentBank bank) {
				return (1024 + bank.Sum(instrument => CalculateInstrumentSize(instrument)));
			}

			static void SaveRandomEffect(RandomInstrumentEffect effect, aBinaryWriter writer) {
				writer.Write8((byte)effect.Target);
				writer.WritePadding(4, 0);
				writer.WriteF32(effect.RandomBase);
				writer.WriteF32(effect.RandomDistance);
				writer.WritePadding(16, 0);
			}
			static void SaveSenseEffect(SenseInstrumentEffect effect, aBinaryWriter writer) {
				writer.Write8((byte)effect.Target);
				writer.Write8((byte)effect.Source);
				writer.Write8((byte)effect.Weight);
				writer.WritePadding(4, 0);
				writer.WriteF32(effect.RangeLo);
				writer.WriteF32(effect.RangeHi);
				writer.WritePadding(16, 0);
			}
			static void SaveVelocityRegion(InstrumentVelocityRegion region, aBinaryWriter writer) {
				writer.Write8((byte)region.Velocity);
				writer.WritePadding(4, 0);
				writer.Write32((uint)(region.WaveId & 0xFFFF));
				writer.WriteF32(region.Volume);
				writer.WriteF32(region.Pitch);
			}
			static void SaveKeyRegion(MelodicKeyRegion region, aBinaryWriter writer) {
				var offset = ((int)writer.Position + RoundUp16B(8 + 4 * region.Count));

				writer.Write8((byte)region.Key);
				writer.WritePadding(4, 0);
				writer.WriteS32(region.Count);

				foreach (var velregion in region) {
					writer.WriteS32(offset);
					offset += 16;
				}

				writer.WritePadding(16, 0);

				foreach (var velregion in region) {
					SaveVelocityRegion(velregion, writer);
				}
			}
			static void SavePercussion(Percussion percussion, int program, aBinaryWriter writer) {
				var offset = ((int)writer.Position + RoundUp16B(20 + 4 * percussion.Count));

				writer.WriteF32(percussion.Volume);
				writer.WriteF32(percussion.Pitch);

				var randomEffects = percussion.Effects.OfType<RandomInstrumentEffect>().ToArray();

				if (randomEffects.Length > 2) {
					mareep.WriteWarning("Drum set #{0}, percussion key #{1} has more than two random effects.\n", program, percussion.Key);
				}

				if (percussion.Effects.Any(effect => effect is SenseInstrumentEffect)) {
					mareep.WriteWarning("Drum set #{0}, percussion key #{1} has sense effects.\n", program, percussion.Key);
				}

				for (var i = 0; i < 2; ++i) {
					if (i < randomEffects.Length) {
						writer.WriteS32(offset);
						offset += 16;
					} else {
						writer.WriteS32(0);
					}
				}

				writer.WriteS32(percussion.Count);

				for (var i = 0; i < percussion.Count; ++i) {
					writer.WriteS32(offset);
					offset += 16;
				}

				writer.WritePadding(16, 0);

				for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
					SaveRandomEffect(randomEffects[i], writer);
				}

				foreach (var velregion in percussion) {
					SaveVelocityRegion(velregion, writer);
				}
			}
			static void SaveMelodic(MelodicInstrument instrument, int program, aBinaryWriter writer) {
				var offset = ((int)writer.Position + RoundUp16B(44 + 4 * instrument.Count));

				writer.Write32(INST);
				writer.WriteS32(0); // unused
				writer.WriteF32(instrument.Volume);
				writer.WriteF32(instrument.Pitch);

				// TODO: oscillators
				var oscillatorOffsets = new int[2];
				writer.WriteS32s(oscillatorOffsets);

				var randomEffects = instrument.Effects.OfType<RandomInstrumentEffect>().ToArray();

				if (randomEffects.Length > 2) {
					mareep.WriteWarning("Instrument #{0} has more than 2 random effects.\n", program);
				}

				for (var i = 0; i < 2; ++i) {
					if (i < randomEffects.Length) {
						writer.WriteS32(offset);
						offset += 16;
					} else {
						writer.WriteS32(0);
					}
				}

				var senseEffects = instrument.Effects.OfType<SenseInstrumentEffect>().ToArray();

				if (senseEffects.Length > 2) {
					mareep.WriteWarning("Instrument #{0} has more than 2 sense effects.\n", program);
				}

				for (var i = 0; i < 2; ++i) {
					if (i < senseEffects.Length) {
						writer.WriteS32(offset);
						offset += 16;
					} else {
						writer.WriteS32(0);
					}
				}

				writer.WriteS32(instrument.Count);

				foreach (var keyregion in instrument) {
					writer.WriteS32(offset);
					offset += CalculateKeyRegionSize(keyregion);
				}

				writer.WritePadding(16, 0);

				for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
					SaveRandomEffect(randomEffects[i], writer);
				}

				for (var i = 0; i < 2 && i < senseEffects.Length; ++i) {
					SaveSenseEffect(senseEffects[i], writer);
				}

				foreach (var keyregion in instrument) {
					SaveKeyRegion(keyregion, writer);
				}

				writer.WritePadding(32, 0);
			}
			static void SaveDrumSet(DrumSet drumset, int program, aBinaryWriter writer) {
				var offset = ((int)writer.Position + 1056);

				writer.Write32(PER2);
				writer.WriteS32(0); // unused
				writer.Write8s(new byte[128]); // unused

				foreach (var percussion in drumset) {
					if (percussion != null) {
						writer.WriteS32(offset);
						offset += CalculatePercussionSize(percussion);
					} else {
						writer.WriteS32(0);
					}
				}

				foreach (var percussion in drumset) {
					if (percussion != null) {
						writer.WriteS8((sbyte)(percussion.Pan * 127.0f));
					} else {
						writer.WriteS8(0);
					}
				}

				foreach (var percussion in drumset) {
					if (percussion != null) {
						writer.Write16((ushort)percussion.Release);
					} else {
						writer.Write16(0);
					}
				}

				writer.WritePadding(32, 0);

				foreach (var percussion in drumset) {
					if (percussion == null) {
						continue;
					}

					SavePercussion(percussion, program, writer);
				}

				writer.WritePadding(32, 0);
			}
			static void SaveInstrumentTable(InstrumentBank bank, aBinaryWriter writer) {
				var offset = 1024;

				for (var i = 0; i < 240; ++i) {
					if (bank[i] != null) {
						writer.WriteS32(offset);
						offset += CalculateInstrumentSize(bank[i]);
					} else {
						writer.WriteS32(0);
					}
				}

				writer.WritePadding(32, 0);
			}
			public static void SaveBank(InstrumentBank bank, aBinaryWriter writer) {
				writer.PushAnchor();

				writer.Write32(IBNK);
				writer.Keep();
				writer.WriteS32(CalculateBankSize(bank));
				writer.WriteS32(bank.VirtualNumber);
				writer.WritePadding(32, 0);

				writer.Write32(BANK);
				SaveInstrumentTable(bank, writer);

				for (var i = 0; i < 240; ++i) {
					if (bank[i] == null) {
						continue;
					}

					if (bank[i] is MelodicInstrument) {
						SaveMelodic((bank[i] as MelodicInstrument), i, writer);
					} else if (bank[i] is DrumSet) {
						SaveDrumSet((bank[i] as DrumSet), i, writer);
					} else {
						mareep.WriteError("Instrument #{0} is unknown type '{1}'.\n", i, bank[i].GetType().Name);
					}
				}

				writer.PopAnchor();
			}

			const uint IBNK = 0x49424E4Bu;
			const uint BANK = 0x42414E4Bu;
			const uint INST = 0x494E5354u;
			const uint PERC = 0x50455243u;
			const uint PER2 = 0x50455232u;

		}

	}

}
