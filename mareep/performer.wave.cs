
using arookas.IO.Binary;
using System;
using System.IO;

namespace arookas {

	[Performer(Action.Wave)]
	class WavePerformer : IPerformer {

		bool mInputWave, mOutputWave;
		string mInput, mOutput;
		WaveFormat mInputFormat, mOutputFormat;
		WaveMixerMode mMixerMode;
		int mSampleCount, mSampleRate;

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter param;

			param = mareep.GetLastCmdParam(cmdline, "-input");

			if (param == null) {
				mareep.WriteError("Missing -input parameter.");
			} else if (param.Count == 0) {
				mareep.WriteError("Missing input filename.");
			}

			mInput = param[0];
			mInputWave = Path.GetExtension(mInput).Equals(".wav", StringComparison.InvariantCultureIgnoreCase);

			if (mInputWave) {
				if (param.Count > 1 && !Enum.TryParse(param[1], true, out mMixerMode)) {
					mareep.WriteError("Bad mixer mode '{0}' for .wav source.", param[1]);
				}
			} else {
				if (param.Count != 3) {
					mareep.WriteError("Missing input format and sample count.");
				}

				if (!Enum.TryParse(param[1], true, out mInputFormat)) {
					mareep.WriteError("Unknown input format '{0}'.", param[1]);
				}

				if (!Int32.TryParse(param[2], out mSampleCount) || mSampleCount < 0) {
					mareep.WriteError("Bad sample count '{0}'.", param[2]);
				}
			}

			param = mareep.GetLastCmdParam(cmdline, "-output");

			if (param == null) {
				mareep.WriteError("Missing -output parameter.");
			} else if (param.Count == 0) {
				mareep.WriteError("Missing output filename.");
			}

			mOutput = param[0];
			mOutputWave = Path.GetExtension(mOutput).Equals(".wav", StringComparison.InvariantCultureIgnoreCase);

			if (mOutputWave) {
				if (mInputWave) {
					if (param.Count != 1) {
						mareep.WriteError("No output format necessary for .wav destination.");
					}
					
				} else {
					if (param.Count != 2) {
						mareep.WriteError("Missing output sample rate for .wav destination.");
					} else if (!Int32.TryParse(param[1], out mSampleRate) || mSampleRate < 0) {
						mareep.WriteError("Bad sample rate '{0}'.", param[1]);
					}
				}
			} else {
				if (param.Count != 2) {
					mareep.WriteError("Missing output format.");
				} else if (!Enum.TryParse(param[1], true, out mOutputFormat)) {
					mareep.WriteError("Unknown output format '{0}'.", param[1]);
				}
			}
		}

		public void Perform() {
			using (var instream = mareep.OpenFile(mInput)) {
				WaveMixer mixer = null;

				if (mInputWave) {
					mixer = new MicrosoftWaveMixer(instream);
					mixer.MixerMode = mMixerMode;
				} else {
					mixer = new RawWaveMixer(instream, mInputFormat, mSampleCount);
				}

				using (var outstream = mareep.CreateFile(mOutput)) {
					if (mOutputWave) {
						WriteWav(mixer, outstream);
					} else {
						WriteRaw(mixer, outstream);
					}
				}
			}
		}
		void WriteWav(WaveMixer mixer, Stream stream) {
			var writer = new aBinaryWriter(stream, Endianness.Little);
			var sampleRate = (mInputWave ? (mixer as MicrosoftWaveMixer).SampleRate : mSampleRate);
			var sampleCount = (mInputWave ? (mixer as MicrosoftWaveMixer).SampleCount : mSampleCount);
			var dataSize = (sampleCount * 2);

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
			mixer.WritePcm16(writer);
		}
		void WriteRaw(WaveMixer mixer, Stream stream) {
			var writer = new aBinaryWriter(stream, Endianness.Big); 
			
			switch (mOutputFormat) {
				case WaveFormat.Pcm8: mixer.WritePcm8(writer); break;
				case WaveFormat.Pcm16: mixer.WritePcm16(writer); break;
				case WaveFormat.Adpcm2: mixer.WriteAdpcm2(writer); break;
				case WaveFormat.Adpcm4: mixer.WriteAdpcm4(writer); break;
			}
		}

	}

}
