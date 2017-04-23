
using System;

namespace arookas {

	static partial class mareep {

		static Version sVersion = new Version(0, 1);
		
		static void Main(string[] arguments) {
			Console.Title = String.Format("mareep v{0} arookas", sVersion);
			mareep.WriteMessage("mareep v{0} arookas\n", sVersion);
			mareep.WriteSeparator('=');

			mareep.WriteMessage("Reading action...\n");
			var action = mareep.ReadAction(arguments);

			mareep.WriteMessage("Initializing performer...\n");
			var performer = mareep.InitPerformer(action);

			mareep.WriteMessage("Reading command-line parameters...\n");
			performer.LoadParams(arguments);

			mareep.WriteSeparator('-');
			mareep.WriteMessage("Calling action...\n");
			performer.Perform();
			mareep.WriteLine();
			mareep.WriteSeparator('-');

			if (sWarningCount > 0) {
				mareep.WriteMessage("Completed with {0} warning(s).\n", sWarningCount);
				Console.ReadKey();
			} else {
				mareep.WriteMessage("Completed successfully!\n");
			}
		}

	}

}
