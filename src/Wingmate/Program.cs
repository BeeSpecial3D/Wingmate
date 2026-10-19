using System;
using Velopack;

namespace Wingmate
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Must run first: handles Velopack's install/update/uninstall hooks (and exits for them), and applies an
            // update that finished downloading last session before the app starts. Harmless for the loose exe.
            try { VelopackApp.Build().Run(); }
            catch (Exception ex) { Core.Log.Write("velopack: " + ex.Message); }
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
