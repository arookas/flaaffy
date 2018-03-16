
using System;
using System.IO;
using System.Text;

namespace arookas {

	static partial class mareep {

		static bool sWriteLine = false;
		static int sWarningCount = 0;

		public static int WarningCount { get { return sWarningCount; } }

		public static void Exit(int code) {
			Environment.Exit(code);
		}

		public static void WriteLine() {
			if (sWriteLine) {
				Console.WriteLine();
			}

			sWriteLine = false;
		}

		public static void WriteMessage(string message) { mareep.WriteMessage("{0}", message); }

		public static void WriteMessage(string format, params object[] arguments) {
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.BackgroundColor = ConsoleColor.Black;
			var output = String.Format(format, arguments);
			Console.Write(output);
			sWriteLine = !output.EndsWith("\n");
			Console.ResetColor();
		}

		public static void WriteWarning(string message) { mareep.WriteWarning("{0}", message); }

		public static void WriteWarning(string format, params object[] arguments) {
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.BackgroundColor = ConsoleColor.Black;
			var output = String.Format(format, arguments);
			Console.Write(output);
			sWriteLine = !output.EndsWith("\n");
			Console.ResetColor();
			++sWarningCount;
		}

		public static void WriteError(string message) { mareep.WriteError("{0}", message); }

		public static void WriteError(string format, params object[] arguments) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.BackgroundColor = ConsoleColor.Black;
			mareep.WriteLine();
			Console.WriteLine(format, arguments);
			Console.ResetColor();
			mareep.Exit(1);
		}

		public static void WriteSeparator(char character) {
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.BackgroundColor = ConsoleColor.Black;
			mareep.WriteLine();
			Console.Write(new String(character, 72));
			Console.WriteLine();
			Console.ResetColor();
		}

		public static Stream CreateFile(string filename, bool fatal = true) {
			if (filename == null) {
				if (fatal) {
					mareep.WriteError("SYSTEM: null filename while creating file.");
				} else {
					mareep.WriteWarning("SYSTEM: null filename while creating file\n");
				}

				return null;
			}

			try {
				return File.Create(filename);
			} catch {
				if (fatal) {
					mareep.WriteError("SYSTEM: failed to create file '{0}'.", filename);
				} else {
					mareep.WriteWarning("SYSTEM: failed to create file '{0}'\n", filename);
				}

				return null;
			}
		}

		public static Stream OpenFile(string filename, bool fatal = true) {
			if (filename == null) {
				if (fatal) {
					mareep.WriteError("SYSTEM: null filename while opening file.");
				} else {
					mareep.WriteWarning("SYSTEM: null filename while opening file\n");
				}

				return null;
			}

			try {
				return File.OpenRead(filename);
			} catch {
				if (fatal) {
					mareep.WriteError("SYSTEM: failed to open file '{0}'.", filename);
				} else {
					mareep.WriteWarning("SYSTEM: failed to open file '{0}'\n", filename);
				}

				return null;
			}
		}

		static readonly int[] srKeyBases = new int[7] {
			0, 2, 4, 5, 7, 9, 11,
		};

		static readonly string[] srKeyLetters = new string[12] {
			"C-", "C#", "D-", "Eb", "E-", "F-", "F#", "G-", "G#", "A-", "Bb", "B-",
		};

		public static int ConvertKey(string keyname) {
			if (keyname == null || keyname.Length < 3) {
				return -1;
			}

			var keyletter = keyname[0];
			int keybase;

			if (keyletter >= 'a' && keyletter <= 'g') {
				keybase = (keyletter - 'a');
			} else if (keyletter >= 'A' && keyletter <= 'G') {
				keybase = (keyletter - 'A');
			} else {
				return -1;
			}

			// we need to map ABCDEFG to CDEFGAB for the array
			// in base-7, adding 5 is the same as subtracting 2
			// but it allows us to avoid subtraction and use modulo
			var keynumber = srKeyBases[(keybase + 5) % 7];

			switch (keyname[1]) {
				case 'b': keynumber -= 1; break;
				case '#': keynumber += 1; break;
				case '-': break;
				default: return -1;
			}

			var octave = 0;

			for (var i = 2; i < keyname.Length; ++i) {
				if (keyname[i] >= '0' && keyname[i] <= '9') {
					octave *= 10;
					octave += (keyname[i] - '0');
				} else {
					return -1;
				}
			}

			keynumber += (octave * 12);

			if (keynumber < 0 || keynumber > 127) {
				return -1;
			}

			return keynumber;
		}

		public static string ConvertKey(int keynumber) {
			if (keynumber < 0 || keynumber > 127) {
				return null;
			}

			var octave = (keynumber / 12);
			var keybase = (keynumber % 12);

			return String.Format("{0}{1}", srKeyLetters[keybase], octave);

		}

		public static string ToLowerString(this object obj) {
			return obj.ToString().ToLowerInvariant();
		}

		public static string[] GetPathComponents(string path) {
			return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar);
		}

		public static string GetRelativePath(string basepath, string path) {
			const StringComparison cComparison = StringComparison.InvariantCultureIgnoreCase;

			if (!Path.IsPathRooted(path)) {
				return path;
			}

			if (!Path.IsPathRooted(basepath)) {
				basepath = Path.GetFullPath(basepath);
			}

			var relativepath = new StringBuilder(path.Length);
			var basepaths = mareep.GetPathComponents(basepath);
			var paths = mareep.GetPathComponents(path);
			var maxlevel = System.Math.Min(basepaths.Length, paths.Length);

			if (basepaths.Length < 1 || paths.Length < 1) {
				return path;
			}

			int level;

			for (level = 0; level < maxlevel && basepaths[level].Equals(paths[level], cComparison); ++level);

			if (level == 0) {
				return path;
			}

			for (var i = level; i < basepaths.Length; ++i) {
				relativepath.Append("..");
				relativepath.Append(Path.DirectorySeparatorChar);
			}

			for (var i = level; i < paths.Length; ++i) {
				relativepath.Append(paths[i]);

				if (i < (paths.Length - 1)) {
					relativepath.Append(Path.DirectorySeparatorChar);
				}
			}

			return relativepath.ToString();
		}

	}

}
