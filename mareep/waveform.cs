
using System;
using System.Linq;
using System.Text;

namespace arookas {

	static class Waveform {

		static int[] sSigned2BitTable = new int[4] {
			0, 1, -2, -1,
		};
		static int[] sSigned4BitTable = new int[16] {
			0, 1, 2, 3, 4, 5, 6, 7, -8, -7, -6, -5, -4, -3, -2, -1,
		};
		static short[,] sAdpcmCoefficents = new short[16, 2] {
			{ 0, 0, }, { 2048, 0, }, { 0, 2048, }, { 1024, 1024, },
			{ 4096, -2048, }, { 3584, -1536, }, { 3072, -1024, }, { 4608, -2560, },
			{ 4200, -2248, }, { 4800, -2300, }, { 5120, -3072, }, { 2048, -2048, },
			{ 1024, -1024, }, { -1024, 1024, }, { -1024, 0, }, { -2048, 0, },
		};

		static short ClampSample16Bit(int sample) {
			if (sample < -32768) {
				sample = -32768;
			} else if (sample > 32767) {
				sample = 32767;
			}

			return (short)sample;
		}

		public static void Adpcm4toPcm8(byte[] adpcm4, sbyte[] pcm8, ref int last, ref int penult) {
			var pcm16 = new short[16];
			Adpcm4toPcm16(adpcm4, pcm16, ref last, ref penult);
			for (var i = 0; i < 16; ++i) {
				Pcm16toPcm8(pcm16[i], out pcm8[i]);
			}
		}
		public static void Adpcm4toPcm16(byte[] adpcm4, short[] pcm16, ref int last, ref int penult) {
			var header = adpcm4[0];
			var nibbleCoeff = (2048 << (header >> 4));

			var coeffIndex = (header & 0xF);
			var lastCoeff = sAdpcmCoefficents[coeffIndex, 0];
			var penultCoeff = sAdpcmCoefficents[coeffIndex, 1];

			for (var i = 0; i < 8; ++i) {
				var input = adpcm4[1 + i];

				for (var j = 0; j < 2; ++j) {
					var nibble = sSigned4BitTable[(input >> 4) & 0xF];
					var sample = ClampSample16Bit((nibbleCoeff * nibble + lastCoeff * last + penultCoeff * penult) >> 11);

					penult = last;
					last = sample;
					pcm16[i * 2 + j] = sample;
					input <<= 4;
				}
			}
		}
		public static void Pcm8toAdpcm4(sbyte[] pcm8, byte[] adpcm4, ref int last, ref int penult) {
			var pcm16 = new short[16];

			for (var i = 0; i < 16; ++i) {
				Pcm8toPcm16(pcm8[i], out pcm16[i]);
			}

			Pcm16toAdpcm4(pcm16, adpcm4, ref last, ref penult);
		}
		public static void Pcm16toAdpcm4(short[] pcm16, byte[] adpcm4, ref int last, ref int penult) {
			// check if all samples in frame are zero
			// if so, write out an empty adpcm frame
			if (pcm16.All(sample => sample == 0)) {
				for (var i = 0; i < 9; ++i) {
					adpcm4[i] = 0;
				}

				last = 0;
				penult = 0;

				return;
			}

			var pcm4 = false;
			var nibbles = new int[16];
			int coeffIndex = 0, scale = 0;

			// try to use coefficient zero for static silence
			for (var i = 0; i < 3; ++i) {
				var step = (1 << i);
				var range = (8 << i);

				if (pcm16.All(sample => sample >= -range && sample < range)) {
					pcm4 = true;
					coeffIndex = 0;
					scale = i;
					break;
				}
			}

			if (!pcm4) {
				coeffIndex = -1;
				var minerror = Int32.MaxValue;

				// otherwise, select one of the remaining coefficients by smallest error
				for (var coeff = 1; coeff < 16; ++coeff) {
					var lastCoeff = sAdpcmCoefficents[coeff, 0];
					var penultCoeff = sAdpcmCoefficents[coeff, 1];
					var found_scale = -1;
					var coeff_error = 0;

					// select the first scale that fits all differences
					for (var current_scale = 0; current_scale < 16; ++current_scale) {
						var step = (1 << current_scale);
						var nibbleCoeff = (2048 << current_scale);
						var success = true;
						coeff_error = 0;
						
						// use non-ref copies
						var _last = last;
						var _penult = penult;

						for (var i = 0; i < 16; ++i) {
							var prediction = ClampSample16Bit((lastCoeff * _last + penultCoeff * _penult) >> 11);
							var difference = -(prediction - pcm16[i]); // negate because we need to counteract it
							var nibble = (difference / step);

							if (nibble < -8 || nibble > 7) {
								success = false;
								break;
							}

							var decoded = ClampSample16Bit((nibbleCoeff * nibble + lastCoeff * _last + penultCoeff * _penult) >> 11);

							_penult = _last;
							_last = decoded;

							// don't let +/- differences cancel each other out
							coeff_error += System.Math.Abs(difference);
						}

						if (success) {
							found_scale = current_scale;
							break;
						}
					}

					if (found_scale < 0) {
						continue;
					}

					if (coeff_error < minerror) {
						minerror = coeff_error;
						coeffIndex = coeff;
						scale = found_scale;
					}
				}

				if (coeffIndex < 0) {
					var sb = new StringBuilder(256);
					sb.Append("could not find coefficient!\nPCM16:");

					for (var i = 0; i < 16; ++i) {
						sb.AppendFormat(" {0,6}", pcm16[i]);
					}

					sb.AppendFormat("\nLAST: {0,6} PENULT: {1,6}\n", last, penult);
					
					mareep.WriteError(sb.ToString());
				}
			}

			{
				// calculate each delta and write to the nibbles
				var lastCoeff = sAdpcmCoefficents[coeffIndex, 0];
				var penultCoeff = sAdpcmCoefficents[coeffIndex, 1];

				var step = (1 << scale);
				
				for (var i = 0; i < 16; ++i) {
					var prediction = ClampSample16Bit((lastCoeff * last + penultCoeff * penult) >> 11);
					var difference = -(prediction - pcm16[i]); // negate because we need to counteract it
					nibbles[i] = (difference / step);

					var decoded = ClampSample16Bit((nibbles[i] * (2048 << scale) + lastCoeff * last + penultCoeff * penult) >> 11);

					penult = last;
					last = decoded;
				}
			}

			// write out adpcm bytes
			adpcm4[0] = (byte)((scale << 4) | coeffIndex);

			for (var i = 0; i < 8; ++i) {
				adpcm4[1 + i] = (byte)(((nibbles[i * 2] << 4) & 0xF0) | (nibbles[i * 2 + 1] & 0xF));
			}
		}

		public static void Adpcm2toPcm8(byte[] adpcm2, sbyte[] pcm8, ref int last, ref int penult) {
			var pcm16 = new short[16];
			Adpcm2toPcm16(adpcm2, pcm16, ref last, ref penult);
			for (var i = 0; i < 16; ++i) {
				Pcm16toPcm8(pcm16[i], out pcm8[i]);
			}
		}
		public static void Adpcm2toPcm16(byte[] adpcm2, short[] pcm16, ref int last, ref int penult) {
			var header = adpcm2[0];
			var nibbleCoeff = (8192 << (header >> 4));

			var coeffIndex = (header & 0xF);
			var lastCoeff = sAdpcmCoefficents[coeffIndex, 0];
			var penultCoeff = sAdpcmCoefficents[coeffIndex, 1];

			for (var i = 0; i < 4; ++i) {
				var input = adpcm2[1 + i];

				for (var j = 0; j < 4; ++j) {
					var nibble = sSigned2BitTable[(input >> 6) & 0x3];
					var sample = ClampSample16Bit(((nibble * nibbleCoeff) + (lastCoeff * last) + (penultCoeff * penult)) >> 11);

					penult = last;
					last = sample;
					pcm16[i * 4 + j] = sample;
					input <<= 2;
				}
			}
		}
		public static void Pcm8toAdpcm2(sbyte[] pcm8, byte[] adpcm2, ref int last, ref int penult) {
			var pcm16 = new short[16];

			for (var i = 0; i < 16; ++i) {
				Pcm8toPcm16(pcm8[i], out pcm16[i]);
			}

			Pcm16toAdpcm2(pcm16, adpcm2, ref last, ref penult);
		}
		public static void Pcm16toAdpcm2(short[] pcm16, byte[] adpcm2, ref int last, ref int penult) {
			// check if all samples in frame are zero
			// if so, write out an empty adpcm frame
			if (pcm16.All(sample => sample == 0)) {
				for (var i = 0; i < 5; ++i) {
					adpcm2[i] = 0;
				}

				last = 0;
				penult = 0;

				return;
			}

			var pcm4 = false;
			var nibbles = new int[16];
			int coeffIndex = 0, scale = 0;

			// try to use coefficient zero for static silence
			for (var i = 0; i < 2; ++i) {
				var step = (1 << i);
				var range = (2 << i);

				if (pcm16.All(sample => sample >= -range && sample < range)) {
					pcm4 = true;
					coeffIndex = 0;
					scale = i;
					break;
				}
			}

			if (!pcm4) {
				coeffIndex = -1;
				var minerror = Int32.MaxValue;

				// otherwise, select one of the remaining coefficients by smallest error
				for (var coeff = 1; coeff < 16; ++coeff) {
					var lastCoeff = sAdpcmCoefficents[coeff, 0];
					var penultCoeff = sAdpcmCoefficents[coeff, 1];
					var found_scale = -1;
					var coeff_error = 0;

					// select the first scale that fits all differences
					for (var current_scale = 0; current_scale < 16; ++current_scale) {
						var step = (1 << current_scale);
						var nibbleCoeff = (8192 << current_scale);
						var success = true;
						coeff_error = 0;

						// use non-ref copies
						var _last = last;
						var _penult = penult;

						for (var i = 0; i < 16; ++i) {
							var prediction = ClampSample16Bit((lastCoeff * _last + penultCoeff * _penult) >> 11);
							var difference = -(prediction - pcm16[i]); // negate because we need to counteract it
							var nibble = (difference / step);

							if (nibble < -2 || nibble > 1) {
								success = false;
								break;
							}

							var decoded = ClampSample16Bit((nibbleCoeff * nibble + lastCoeff * _last + penultCoeff * _penult) >> 11);

							_penult = _last;
							_last = decoded;

							// don't let +/- differences cancel each other out
							coeff_error += System.Math.Abs(difference);
						}

						if (success) {
							found_scale = current_scale;
							break;
						}
					}

					if (found_scale < 0) {
						continue;
					}

					if (coeff_error < minerror) {
						minerror = coeff_error;
						coeffIndex = coeff;
						scale = found_scale;
					}
				}

				if (coeffIndex < 0) {
					var sb = new StringBuilder(256);
					sb.Append("could not find coefficient!\nPCM16:");

					for (var i = 0; i < 16; ++i) {
						sb.AppendFormat(" {0,6}", pcm16[i]);
					}

					sb.AppendFormat("\nLAST: {0,6} PENULT: {1,6}\n", last, penult);

					mareep.WriteError(sb.ToString());
				}
			}

			{
				// calculate each delta and write to the nibbles
				var lastCoeff = sAdpcmCoefficents[coeffIndex, 0];
				var penultCoeff = sAdpcmCoefficents[coeffIndex, 1];

				var step = (1 << scale);

				for (var i = 0; i < 16; ++i) {
					var prediction = ClampSample16Bit((lastCoeff * last + penultCoeff * penult) / 2048);
					var difference = -(prediction - pcm16[i]); // negate because we need to counteract it
					nibbles[i] = (difference / step);

					var decoded = ClampSample16Bit((nibbles[i] * (8192 << scale) + lastCoeff * last + penultCoeff * penult) / 2048);

					penult = last;
					last = decoded;
				}
			}

			// write out adpcm bytes
			adpcm2[0] = (byte)((scale << 4) | coeffIndex);

			for (var i = 0; i < 4; ++i) {
				adpcm2[1 + i] = (byte)(((nibbles[i * 2] << 4) & 0xC0) | ((nibbles[i * 2 + 1] << 4) & 0x30) | ((nibbles[i * 2 + 2] << 4) & 0x0C) | (nibbles[i * 2 + 3] & 0x03));
			}
		}
		
		public static void Pcm8toPcm16(sbyte pcm8, out short pcm16) {
			pcm16 = (short)(pcm8 * (pcm8 < 0 ? 256 : 258));
		}
		public static void Pcm16toPcm8(short pcm16, out sbyte pcm8) {
			pcm8 = (sbyte)(pcm16 >> 8);
		}

	}

}
