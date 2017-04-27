
using arookas.IO.Binary;
using System;
using System.IO;

namespace arookas {

	[Performer(Action.Wave)]
	class WavePerformer : IPerformer {

		string mInput, mOutput;
		IOFormat mInputFormat, mOutputFormat;
		WaveMixerMode mMixerMode;
		Mode mMode;

		// raw
		WaveFormat mRawInputFormat, mRawOutputFormat;
		int mRawSampleCount, mRawSampleRate;

		// streams
		bool mStreamLoop;
		int mStreamLoopStart;

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);

			// input
			var inputparam = mareep.GetLastCmdParam(cmdline, "-input");

			if (inputparam == null) {
				mareep.WriteError("Missing -input parameter.");
			} else if (inputparam.Count == 0) {
				mareep.WriteError("Missing input filename.");
			}

			mInput = inputparam[0];
			mInputFormat = GetFormat(Path.GetExtension(mInput));

			if (mInputFormat == IOFormat.Raw) {
				if (inputparam.Count < 2) {
					mareep.WriteError("Missing format for raw input.");
				}

				if (!Enum.TryParse(inputparam[1], true, out mRawInputFormat)) {
					mareep.WriteError("Bad format '{0}' for raw input.", inputparam[1]);
				}
			}

			// output
			var outputparam = mareep.GetLastCmdParam(cmdline, "-output");

			if (outputparam == null) {
				mareep.WriteError("Missing -output parameter.");
			} else if (outputparam.Count == 0) {
				mareep.WriteError("Missing output filename.");
			}

			mOutput = outputparam[0];
			mOutputFormat = GetFormat(Path.GetExtension(mOutput));

			if (mOutputFormat == IOFormat.Raw) {
				if (outputparam.Count < 2) {
					mareep.WriteError("Missing format for raw output.");
				}

				if (!Enum.TryParse(outputparam[1], true, out mRawOutputFormat)) {
					mareep.WriteError("Bad format '{0}' for raw output.", outputparam[1]);
				}
			}

			// mode
			if (mInputFormat == IOFormat.Raw && mOutputFormat == IOFormat.Raw) {
				mMode = Mode.RawToRaw;
			} else if (mInputFormat == IOFormat.Raw && mOutputFormat == IOFormat.MicrosoftWave) {
				mMode = Mode.RawToWav;
			} else if (mInputFormat == IOFormat.MicrosoftWave && mOutputFormat == IOFormat.Raw) {
				mMode = Mode.WavToRaw;
			} else if (mInputFormat == IOFormat.MicrosoftWave && mOutputFormat == IOFormat.AdpcmStream) {
				mMode = Mode.WavToStream;
			} else if (mInputFormat == IOFormat.AdpcmStream && mOutputFormat == IOFormat.MicrosoftWave) {
				mMode = Mode.StreamToWav;
			} else {
				mareep.WriteError("Unsupported combination of input and output formats.");
			}

			// mix mode
			var mixmodeparam = mareep.GetLastCmdParam(cmdline, "-mix-mode");

			if (mixmodeparam != null) {
				if (mixmodeparam.Count < 1) {
					mareep.WriteError("Bad -mix-mode parameter.");
				}

				if (!Enum.TryParse(mixmodeparam[0], true, out mMixerMode)) {
					mareep.WriteError("Bad mixer mode '{0}' in -mix-mode parameter.", mixmodeparam[0]);
				}
			}

			// sample count
			var samplecountparam = mareep.GetLastCmdParam(cmdline, "-sample-count");

			if (samplecountparam != null) {
				if (samplecountparam.Count < 1) {
					mareep.WriteError("Bad -sample-count parameter.");
				}

				if (!Int32.TryParse(samplecountparam[0], out mRawSampleCount) || mRawSampleCount < 0) {
					mareep.WriteError("Bad sample count '{0}' in -sample-count parameter.", samplecountparam[0]);
				}
			} else if (mInputFormat == IOFormat.Raw) {
				mareep.WriteError("Missing -sample-count parameter for raw input.");
			}

			// sample rate
			var samplerateparam = mareep.GetLastCmdParam(cmdline, "-sample-rate");

			if (samplerateparam != null) {
				if (samplecountparam.Count < 1) {
					mareep.WriteError("Bad -sample-rate parameter.");
				}

				if (!Int32.TryParse(samplerateparam[0], out mRawSampleRate) || mRawSampleRate < 0) {
					mareep.WriteError("Bad sample rate '{0}' in -sample-rate parameter.", samplerateparam[0]);
				}
			} else if (mInputFormat == IOFormat.Raw && mOutputFormat != IOFormat.Raw) {
				mareep.WriteError("Missing -sample-rate parameter for raw input.");
			}

			// loop
			var loopparam = mareep.GetLastCmdParam(cmdline, "-loop");

			if (loopparam != null) {
				mStreamLoop = true;

				if (loopparam.Count < 1) {
					mareep.WriteError("Bad -loop parameter.");
				}

				if (!Int32.TryParse(loopparam[0], out mStreamLoopStart) || mStreamLoopStart < 0) {
					mareep.WriteError("Bad loop '{0}' in -loop parameter.", loopparam[0]);
				}
			}
		}

		public void Perform() {
			using (var instream = mareep.OpenFile(mInput)) {
				using (var outstream = mareep.CreateFile(mOutput)) {
					switch (mMode) {
						case Mode.RawToRaw: PerformRawToRaw(instream, outstream); break;
						case Mode.RawToWav: PerformRawToWav(instream, outstream); break;
						case Mode.WavToRaw: PerformWavToRaw(instream, outstream); break;
						case Mode.WavToStream: PerformWavToStream(instream, outstream); break;
						case Mode.StreamToWav: PerformStreamToWav(instream, outstream); break;
						default: mareep.WriteError("Unimplemented mode '{0}'.", mMode); break;
					}
				}
			}
		}

		void PerformRawToRaw(Stream instream, Stream outstream) {
			var mixer = new RawWaveMixer(instream, mRawInputFormat, mRawSampleCount);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			switch (mRawOutputFormat) {
				case WaveFormat.Pcm8: mixer.WritePcm8(writer); break;
				case WaveFormat.Pcm16: mixer.WritePcm16(writer); break;
				case WaveFormat.Adpcm2: mixer.WriteAdpcm2(writer); break;
				case WaveFormat.Adpcm4: mixer.WriteAdpcm4(writer); break;
			}
		}
		void PerformRawToWav(Stream instream, Stream outstream) {
			var mixer = new RawWaveMixer(instream, mRawInputFormat, mRawSampleCount);
			var writer = new aBinaryWriter(outstream, Endianness.Little);
			var dataSize = (mRawSampleCount * 2);

			writer.WriteString("RIFF");
			writer.WriteS32(36 + dataSize);
			writer.WriteString("WAVE");
			writer.WriteString("fmt ");
			writer.WriteS32(16);
			writer.WriteS16(1); // format
			writer.Write16(1); // channel count
			writer.WriteS32(mRawSampleRate);
			writer.WriteS32(mRawSampleRate * 2); // byte rate
			writer.Write16(2); // block align
			writer.Write16(16); // bit depth
			writer.WriteString("data");
			writer.WriteS32(dataSize);
			mixer.WritePcm16(writer);
		}
		void PerformWavToRaw(Stream instream, Stream outstream) {
			var mixer = new MicrosoftWaveMixer(instream);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			mixer.MixerMode = mMixerMode;

			switch (mRawOutputFormat) {
				case WaveFormat.Pcm8: mixer.WritePcm8(writer); break;
				case WaveFormat.Pcm16: mixer.WritePcm16(writer); break;
				case WaveFormat.Adpcm2: mixer.WriteAdpcm2(writer); break;
				case WaveFormat.Adpcm4: mixer.WriteAdpcm4(writer); break;
			}
		}
		void PerformStreamToWav(Stream instream, Stream outstream) {
			var reader = new aBinaryReader(instream, Endianness.Big);
			var writer = new aBinaryWriter(outstream, Endianness.Little);

			var streamDataSize = reader.ReadS32();
			var sampleCount = reader.ReadS32();
			var sampleRate = reader.Read16();
			var dataSize = (sampleCount * 4);

			writer.WriteString("RIFF");
			writer.WriteS32(36 + dataSize);
			writer.WriteString("WAVE");
			writer.WriteString("fmt ");
			writer.WriteS32(16);
			writer.WriteS16(1); // format
			writer.Write16(2); // channel count
			writer.WriteS32(sampleRate);
			writer.WriteS32(sampleRate * 4); // byte rate
			writer.Write16(4); // block align
			writer.Write16(16); // bit depth
			writer.WriteString("data");
			writer.WriteS32(dataSize);

			reader.Goto(32);

			var left = new short[16];
			int left_last = 0, left_penult = 0;

			var right = new short[16];
			int right_last = 0, right_penult = 0;

			for (var i = 0; i < sampleCount; i += 16) {
				Waveform.Adpcm4toPcm16(reader.Read8s(9), left, ref left_last, ref left_penult);
				Waveform.Adpcm4toPcm16(reader.Read8s(9), right, ref right_last, ref right_penult);

				for (var j = 0; j < 16 && (i + j) < sampleCount; ++j) {
					writer.WriteS16(left[j]);
					writer.WriteS16(right[j]);
				}
			}
		}
		void PerformWavToStream(Stream instream, Stream outstream) {
			var mixer = new MicrosoftWaveMixer(instream);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			writer.WriteS32((mixer.SampleCount + 15) & ~15); // data size
			writer.WriteS32(mixer.SampleCount);
			writer.Write16((ushort)mixer.SampleRate);
			writer.WriteS16(4); // unknown
			writer.WriteS16(0x10); // unknown
			writer.WriteS16(0x1E); // unknown
			writer.WriteS32(mStreamLoop ? 1 : 0); // loop flag
			writer.WriteS32(mStreamLoop ? mStreamLoopStart : 0); // loop start
			writer.WritePadding(32, 0);

			var left_adpcm4 = new byte[9];
			int left_last = 0, left_penult = 0;

			var right_adpcm4 = new byte[9];
			int right_last = 0, right_penult = 0;

			for (var i = 0; i < mixer.SampleCount; i += 16) {
				var left = new short[16];
				var right = new short[16];

				for (var j = 0; j < 16; ++j) {
					if ((i + j) < mixer.SampleCount) {
						mixer.ReadPcm16((i + j), out left[j], out right[j]);
					} else {
						left[j] = 0;
						right[j] = 0;
					}
				}

				Waveform.Pcm16toAdpcm4(left, left_adpcm4, ref left_last, ref left_penult);
				Waveform.Pcm16toAdpcm4(right, right_adpcm4, ref right_last, ref right_penult);

				writer.Write8s(left_adpcm4);
				writer.Write8s(right_adpcm4);
			}

			writer.WritePadding(32, 0);
		}

		static IOFormat GetFormat(string extension) {
			switch (extension.ToLowerInvariant()) {
				case ".raw": return IOFormat.Raw;
				case ".wav": return IOFormat.MicrosoftWave;
				case ".afc": return IOFormat.AdpcmStream;
			}

			return IOFormat.Unknown;
		}

		enum IOFormat {

			Unknown = -1,
			Raw,
			MicrosoftWave,
			AdpcmStream,

		}

		enum Mode {

			RawToRaw,
			RawToWav,
			WavToRaw,
			WavToStream,
			StreamToWav,

		}

	}

}
