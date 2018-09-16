
using System;

namespace arookas {

	static partial class mareep {

		static Version sVersion = new Version(0, 6, 1);

		static void Main(string[] arguments) {
			Console.Title = String.Format("mareep v{0} arookas", sVersion);
			mareep.WriteMessage("mareep v{0} arookas\n", sVersion);
			mareep.WriteSeparator('=');

			if (arguments.Length == 0) {
				ShowUsage();
			}

			bool help = false;
			string name = null;
			int i;

			for (i = 0; (name == null && i < arguments.Length); ++i) {
				switch (arguments[i]) {
					case "-help": {
						help = true;
						break;
					}
					case "-errand": {
						if ((i + 1) >= arguments.Length) {
							ShowUsage();
						}

						name = arguments[++i];
						break;
					}
				}
			}

			if (name == null) {
				ShowUsage();
			}

			var errand = mareep.ReadErrand(name);
			var instance = mareep.InitErrand(errand);

			if (help) {
				instance.ShowUsage();
			} else {
				string[] args = new string[arguments.Length - i];
				Array.Copy(arguments, i, args, 0, args.Length);
				instance.LoadParams(args);
				instance.Perform();

				mareep.WriteLine();
				mareep.WriteSeparator('-');

				if (sWarningCount > 0) {
					mareep.WriteMessage("Completed with {0} warning(s).\n", sWarningCount);
				} else {
					mareep.WriteMessage("Completed successfully!\n");
				}
			}
		}

		static void ShowUsage() {
			mareep.WriteMessage("USAGE: mareep [-help] -errand <errand> [...]\n");
			mareep.WriteMessage("\n");
			mareep.WriteMessage("OPTIONS:\n");
			mareep.WriteMessage("  -help    display help on program or errand\n");
			mareep.WriteMessage("\n");
			mareep.WriteMessage("ERRANDS:\n");
			mareep.WriteMessage("  shock    convert banks 'IBNK'\n");
			mareep.WriteMessage("  whap     convert wave banks 'WSYS'\n");
			mareep.WriteMessage("  wave     convert audio files\n");
			mareep.WriteMessage("  cotton   assemble sequence files\n");
			mareep.WriteMessage("  jolt     convert midi to assembly\n");
			mareep.WriteMessage("  charge   extract and import aaf\n");
			mareep.Exit(0);
		}

	}

}
