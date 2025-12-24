using System;
using System.Windows.Forms;

namespace GPhoto
{
	internal static class Program
	{
		[STAThread]
		static void Main()
		{
			// Add global exception handlers
			Application.ThreadException += (sender, e) =>
			{
				MessageBox.Show($"Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", 
					"Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			};
			
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				var ex = e.ExceptionObject as Exception;
				MessageBox.Show($"Fatal Error: {ex?.Message}\n\n{ex?.StackTrace}", 
					"Fatal Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			};

			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());
		}
	}
}
