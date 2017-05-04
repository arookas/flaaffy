
using System;

namespace arookas {

	static partial class mareep {

		static Version sVersion = new Version(0, 3);
		
		static void Main(string[] arguments) {
			Console.Title = String.Format("mareep v{0} arookas", sVersion);
			mareep.WriteMessage("mareep v{0} arookas\n", sVersion);
			mareep.WriteSeparator('=');

			mareep.WriteMessage("Reading errand...\n");
			var errand = mareep.ReadErrand(arguments);

			mareep.WriteMessage("Initializing errand...\n");
			var instance = mareep.InitErrand(errand);

			mareep.WriteMessage("Reading command-line parameters...\n");
			instance.LoadParams(arguments);

			mareep.WriteSeparator('-');
			mareep.WriteMessage("Calling errand...\n");
			instance.Perform();
			mareep.WriteLine();
			mareep.WriteSeparator('-');

			if (sWarningCount > 0) {
				mareep.WriteMessage("Completed with {0} warning(s).", sWarningCount);
				Console.ReadKey();
			} else {
				mareep.WriteMessage("Completed successfully!");
			}
		}

	}

}
