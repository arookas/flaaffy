
using arookas.IO.Binary;
using System;

namespace arookas {

	[Performer(Action.Wave)]
	class WavePerformer : IPerformer {

		string mInput, mOutput;
		WaveFormat mInputFormat, mOutputFormat;
		int mSampleCount;

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter param;

			param = mareep.GetLastCmdParam(cmdline, "-input");

			if (param == null) {
				mareep.WriteError("Missing -input parameter.");
			}

			if (param.Count != 2 || !Enum.TryParse(param[1], true, out mInputFormat)) {
				mareep.WriteError("Bad -input parameter.");
			}

			mInput = param[0];

			param = mareep.GetLastCmdParam(cmdline, "-output");

			if (param == null) {
				mareep.WriteError("Missing -output parameter.");
			}

			if (param.Count != 2 || !Enum.TryParse(param[1], true, out mOutputFormat)) {
				mareep.WriteError("Bad -output parameter.");
			}

			mOutput = param[0];

			param = mareep.GetLastCmdParam(cmdline, "-sample-count");

			if (param == null) {
				mareep.WriteError("Missing -sample-count parameter.");
			}

			if (param.Count != 1 || !Int32.TryParse(param[0], out mSampleCount) || mSampleCount < 0) {
				mareep.WriteError("Bad -sample-count parameter.");
			}
		}

		public void Perform() {
			using (var instream = mareep.OpenFile(mInput)) {
				var mixer = new RawWaveMixer(instream, null, mInputFormat, mSampleCount);

				using (var outstream = mareep.CreateFile(mOutput)) {
					var writer = new aBinaryWriter(outstream, Endianness.Big);

					switch (mOutputFormat) {
						case WaveFormat.Pcm8: mixer.WritePcm8(writer); break;
						case WaveFormat.Pcm16: mixer.WritePcm16(writer); break;
						case WaveFormat.Adpcm2: mixer.WriteAdpcm2(writer); break;
						case WaveFormat.Adpcm4: mixer.WriteAdpcm4(writer); break;
					}
				}
			}
		}

	}

}
