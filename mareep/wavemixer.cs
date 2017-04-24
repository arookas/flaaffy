
using arookas.IO.Binary;
using System;
using System.IO;
using System.Text;

namespace arookas {

	enum WaveMixerMode {

		Mix,
		Left,
		Right,

	}

	abstract class WaveMixer {

		protected WaveMixerMode mMixerMode;

		public WaveMixerMode MixerMode {
			get { return mMixerMode; }
			set {
				if (!value.IsDefined()) {
					throw new ArgumentOutOfRangeException("value");
				}

				mMixerMode = value;
			}
		}

		public abstract int SampleCount { get; }

		public virtual void CopyWaveInfo(Wave wave) { }

		public abstract void WritePcm8(aBinaryWriter writer);
		public abstract void WritePcm16(aBinaryWriter writer);
		public abstract void WriteAdpcm2(aBinaryWriter writer);
		public abstract void WriteAdpcm4(aBinaryWriter writer);

	}

	class RawWaveMixer : WaveMixer {

		aBinaryReader mReader;
		WaveFormat mFormat;
		int mSampleCount;

		public override int SampleCount { get { return mSampleCount; } }
		public WaveFormat Format { get { return mFormat; } }

		public RawWaveMixer(Stream stream, WaveFormat format, int sampleCount) {
			if (!format.IsDefined()) {
				throw new ArgumentOutOfRangeException("format");
			}

			if (sampleCount < 0) {
				throw new ArgumentOutOfRangeException("sampleCount");
			}

			mFormat = format;
			mSampleCount = sampleCount;
			mReader = new aBinaryReader(stream, Endianness.Big);
			mReader.PushAnchor();
		}
		public RawWaveMixer(Stream stream, Wave wave, WaveFormat format, int sampleCount) : this(stream, format, sampleCount) {
			CopyWaveInfo(wave);
		}

		public override void CopyWaveInfo(Wave wave) {
			if (wave == null) {
				return;
			}

			if (wave.SampleCount <= 0) {
				wave.SampleCount = mSampleCount;
			}
		}

		public override void WritePcm8(aBinaryWriter writer) {
			mReader.Goto(0);

			switch (mFormat) {
				case WaveFormat.Pcm8: {
					writer.WriteS8s(mReader.ReadS8s(mSampleCount));
					break;
				}
				case WaveFormat.Pcm16: {
					for (var i = 0; i < mSampleCount; ++i) {
						sbyte pcm8;
						Waveform.Pcm16toPcm8(mReader.ReadS16(), out pcm8);
						writer.WriteS8(pcm8);
					}
					break;
				}
				case WaveFormat.Adpcm2: {
					var pcm8 = new sbyte[16];
					var pcm16 = new short[16];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm2toPcm16(mReader.Read8s(5), pcm16, ref last, ref penult);
						for (var j = 0; j < 16; ++j) {
							Waveform.Pcm16toPcm8(pcm16[j], out pcm8[j]);
						}
						writer.WriteS8s(pcm8, System.Math.Min(16, (mSampleCount - i)));
					}
					break;
				}
				case WaveFormat.Adpcm4: {
					var pcm8 = new sbyte[16];
					var pcm16 = new short[16];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm4toPcm16(mReader.Read8s(9), pcm16, ref last, ref penult);
						for (var j = 0; j < 16; ++j) {
							Waveform.Pcm16toPcm8(pcm16[j], out pcm8[j]);
						}
						writer.WriteS8s(pcm8, System.Math.Min(16, (mSampleCount - i)));
					}
					break;
				}
			}
		}
		public override void WritePcm16(aBinaryWriter writer) {
			mReader.Goto(0);

			switch (mFormat) {
				case WaveFormat.Pcm8: {
					for (var i = 0; i < mSampleCount; ++i) {
						short pcm16;
						Waveform.Pcm8toPcm16(mReader.ReadS8(), out pcm16);
						writer.WriteS16(pcm16);
					}
					break;
				}
				case WaveFormat.Pcm16: {
					writer.WriteS16s(mReader.ReadS16s(mSampleCount));
					break;
				}
				case WaveFormat.Adpcm2: {
					var pcm16 = new short[16];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm2toPcm16(mReader.Read8s(5), pcm16, ref last, ref penult);
						writer.WriteS16s(pcm16, System.Math.Min(16, (mSampleCount - i)));
					}
					break;
				}
				case WaveFormat.Adpcm4: {
					var pcm16 = new short[16];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm4toPcm16(mReader.Read8s(9), pcm16, ref last, ref penult);
						writer.WriteS16s(pcm16, System.Math.Min(16, (mSampleCount - i)));
					}
					break;
				}
			}
		}
		public override void WriteAdpcm2(aBinaryWriter writer) {
			mReader.Goto(0);

			switch (mFormat) {
				case WaveFormat.Pcm8: {
					var pcm16 = new short[16];
					var adpcm2 = new byte[5];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						for (var j = 0; j < 16; ++j) {
							pcm16[j] = 0;

							if (i + j < mSampleCount) {
								Waveform.Pcm8toPcm16(mReader.ReadS8(), out pcm16[j]);
							}
						}
						Waveform.Pcm16toAdpcm2(pcm16, adpcm2, ref last, ref penult);
						writer.Write8s(adpcm2);
					}
					break;
				}
				case WaveFormat.Pcm16: {
					var pcm16 = new short[16];
					var adpcm2 = new byte[5];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						for (var j = 0; j < 16; ++j) {
							pcm16[j] = (short)(i + j < mSampleCount ? mReader.ReadS16() : 0);
						}
						Waveform.Pcm16toAdpcm2(pcm16, adpcm2, ref last, ref penult);
						writer.Write8s(adpcm2);
					}
					break;
				}
				case WaveFormat.Adpcm2: {
					for (var i = 0; i < mSampleCount; i += 16) {
						writer.Write8s(mReader.Read8s(5));
					}
					break;
				}
				case WaveFormat.Adpcm4: {
					var pcm16 = new short[16];
					var adpcm2 = new byte[5];
					int last1 = 0, penult1 = 0;
					int last2 = 0, penult2 = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm4toPcm16(mReader.Read8s(9), pcm16, ref last1, ref penult1);
						Waveform.Pcm16toAdpcm2(pcm16, adpcm2, ref last2, ref penult2);
						writer.Write8s(adpcm2);
					}
					break;
				}
			}
		}
		public override void WriteAdpcm4(aBinaryWriter writer) {
			mReader.Goto(0);

			switch (mFormat) {
				case WaveFormat.Pcm8: {
					var pcm16 = new short[16];
					var adpcm4 = new byte[9];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						for (var j = 0; j < 16; ++j) {
							pcm16[j] = 0;

							if (i + j < mSampleCount) {
								Waveform.Pcm8toPcm16(mReader.ReadS8(), out pcm16[j]);
							}
						}
						Waveform.Pcm16toAdpcm4(pcm16, adpcm4, ref last, ref penult);
						writer.Write8s(adpcm4);
					}
					break;
				}
				case WaveFormat.Pcm16: {
					var pcm16 = new short[16];
					var adpcm4 = new byte[9];
					int last = 0, penult = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						for (var j = 0; j < 16; ++j) {
							pcm16[j] = (short)(i + j < mSampleCount ? mReader.ReadS16() : 0);
						}
						Waveform.Pcm16toAdpcm4(pcm16, adpcm4, ref last, ref penult);
						writer.Write8s(adpcm4);
					}
					break;
				}
				case WaveFormat.Adpcm2: {
					var pcm16 = new short[16];
					var adpcm4 = new byte[9];
					int last1 = 0, penult1 = 0;
					int last2 = 0, penult2 = 0;
					for (var i = 0; i < mSampleCount; i += 16) {
						Waveform.Adpcm2toPcm16(mReader.Read8s(9), pcm16, ref last1, ref penult1);
						Waveform.Pcm16toAdpcm4(pcm16, adpcm4, ref last2, ref penult2);
						writer.Write8s(adpcm4);
					}
					break;
				}
				case WaveFormat.Adpcm4: {
					for (var i = 0; i < mSampleCount; i += 16) {
						writer.Write8s(mReader.Read8s(9));
					}
					break;
				}
			}
		}

	}

	class MicrosoftWaveMixer : WaveMixer {

		aBinaryReader mReader;
		int mSize, mDataStart, mDataSize;
		int mSampleCount, mChannelCount;
		int mSampleRate, mByteRate, mBlockAlign, mBitDepth;

		public override int SampleCount { get { return mSampleCount; } }
		public int ChannelCount { get { return mChannelCount; } }
		public int SampleRate { get { return mSampleRate; } }
		public int ByteRate { get { return mByteRate; } }
		public int BlockAlign { get { return mBlockAlign; } }
		public int BitDepth { get { return mBitDepth; } }

		public MicrosoftWaveMixer(Stream stream) {
			mReader = new aBinaryReader(stream, Endianness.Little, Encoding.ASCII);
			mReader.PushAnchor();
			LoadRiffBlock();
		}
		public MicrosoftWaveMixer(Stream stream, Wave wave) : this(stream) {
			CopyWaveInfo(wave);
		}

		void LoadRiffBlock() {
			switch (mReader.ReadString(4)) {
				case "RIFF": break;
				case "RIFX": mReader.Endianness = Endianness.Big; break;
				default: mareep.WriteError("WAV: could not find 'RIFF'."); break;
			}

			mSize = mReader.ReadS32();

			if (mReader.ReadString(4) != "WAVE") {
				mareep.WriteError("WAV: could not find 'WAVE'.");
			}

			var fmt = false;
			var data = false;

			while ((mReader.Position - 8) < mSize) {
				var id = mReader.ReadString(4);
				var size = mReader.ReadS32();
				var start = mReader.Position;

				switch (id) {
					case "fmt ": fmt = true; LoadFmtBlock(size); break;
					case "data": data = true; LoadDataBlock(size); break;
					case "LIST": mareep.WriteWarning("WAV: skipping list '{0}'.\n", mReader.ReadString(4)); break;
					default: mareep.WriteWarning("WAV: unknown chunk '{0}'.\n", id); break;
				}

				mReader.Goto(start + size);
			}

			if (!fmt) {
				mareep.WriteError("WAV: missing 'fmt ' chunk.");
			} else if (!data) {
				mareep.WriteError("WAV: missing 'data' chunk.");
			}

			// calculate sample count here to ensure fmt has been loaded
			mSampleCount = (mDataSize / mBlockAlign);
		}
		void LoadFmtBlock(int size) {
			var format = mReader.ReadS16();

			if (format != 1) {
				mareep.WriteError("WAV: only LPCM is supported.");
			}

			mChannelCount = mReader.Read16();

			if (mChannelCount < 1 || mChannelCount > 2) {
				mareep.WriteError("WAV: only mono or stereo is supported.");
			}

			mSampleRate = mReader.ReadS32();
			mByteRate = mReader.ReadS32();
			mBlockAlign = mReader.Read16();
			mBitDepth = mReader.Read16();

			if (mBitDepth != 8 && mBitDepth != 16) {
				mareep.WriteError("WAV: only bit-depths of 8 and 16 are supported.");
			}
		}
		void LoadDataBlock(int size) {
			mDataStart = (int)mReader.Position;
			mDataSize = size;
		}

		public override void CopyWaveInfo(Wave wave) {
			if (wave == null) {
				return;
			}

			if (wave.SampleCount <= 0) {
				wave.SampleCount = mSampleCount;
			}

			if (wave.SampleRate <= 0.0f) {
				wave.SampleRate = mSampleRate;
			}
		}

		void GotoSample(int sample) {
			mReader.Goto(mDataStart + (mBlockAlign * sample));
		}
		sbyte ReadPcm8() {
			return (sbyte)(mReader.Read8() - 128); // 8-bit LPCM stores samples as unsigned
		}
		sbyte MixPcm8() {
			sbyte pcm8 = 0;

			if (mChannelCount == 1) {
				if (mBitDepth == 8) {
					pcm8 = ReadPcm8();
				} else {
					Waveform.Pcm16toPcm8(ReadPcm16(), out pcm8);
				}
			} else {
				if (mBitDepth == 8) {
					var left = ReadPcm8();
					var right = ReadPcm8();

					pcm8 = MixStereoPcm8(left, right);
				} else {
					var left = ReadPcm16();
					var right = ReadPcm16();

					Waveform.Pcm16toPcm8(MixStereoPcm16(left, right), out pcm8);
				}
			}

			return pcm8;
		}
		short ReadPcm16() {
			return mReader.ReadS16();
		}
		short MixPcm16() {
			short pcm16 = 0;

			if (mChannelCount == 1) {
				if (mBitDepth == 16) {
					pcm16 = ReadPcm16();
				} else {
					Waveform.Pcm8toPcm16(ReadPcm8(), out pcm16);
				}
			} else {
				if (mBitDepth == 16) {
					var left = ReadPcm16();
					var right = ReadPcm16();

					pcm16 = MixStereoPcm16(left, right);
				} else {
					var left = ReadPcm8();
					var right = ReadPcm8();

					Waveform.Pcm8toPcm16(MixStereoPcm8(left, right), out pcm16);
				}
			}

			return pcm16;
		}

		sbyte MixStereoPcm8(sbyte left, sbyte right) {
			switch (mMixerMode) {
				case WaveMixerMode.Mix: return (sbyte)((left + right) >> 1);
				case WaveMixerMode.Left: return left;
				case WaveMixerMode.Right: return right;
			}

			return 0;
		}
		short MixStereoPcm16(short left, short right) {
			switch (mMixerMode) {
				case WaveMixerMode.Mix: return (short)((left >> 1) + (right >> 1));
				case WaveMixerMode.Left: return left;
				case WaveMixerMode.Right: return right;
			}

			return 0;
		}

		public override void WritePcm8(aBinaryWriter writer) {
			for (var i = 0; i < mSampleCount; ++i) {
				GotoSample(i);
				writer.WriteS8(MixPcm8());
			}
		}
		public override void WritePcm16(aBinaryWriter writer) {
			for (var i = 0; i < mSampleCount; ++i) {
				GotoSample(i);
				writer.WriteS16(MixPcm16());
			}
		}
		public override void WriteAdpcm2(aBinaryWriter writer) {
			var adpcm2 = new byte[5];
			var pcm16 = new short[16];
			int last = 0, penult = 0;
			for (var i = 0; i < mSampleCount; i += 16) {
				for (var j = 0; j < 16; ++j) {
					pcm16[j] = 0;

					if (i + j < mSampleCount) {
						GotoSample(i + j);
						pcm16[j] = MixPcm16();
					}
				}

				Waveform.Pcm16toAdpcm2(pcm16, adpcm2, ref last, ref penult);
				writer.Write8s(adpcm2);
			}
		}
		public override void WriteAdpcm4(aBinaryWriter writer) {
			var adpcm4 = new byte[9];
			var pcm16 = new short[16];
			int last = 0, penult = 0;
			for (var i = 0; i < mSampleCount; i += 16) {
				for (var j = 0; j < 16; ++j) {
					pcm16[j] = 0;

					if (i + j < mSampleCount) {
						GotoSample(i + j);
						pcm16[j] = MixPcm16();
					}
				}

				Waveform.Pcm16toAdpcm4(pcm16, adpcm4, ref last, ref penult);
				writer.Write8s(adpcm4);
			}
		}

	}
}
