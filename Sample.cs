using Autodesk.AutoCAD.Runtime;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Threading;

[assembly: ExtensionApplication(typeof(FabricationSample.ACADSample))]

namespace FabricationSample
{
    public class Sample : IExternalApplication
    {
        FabricationWindow win = null;
        public Sample()
        {
        }

        public void Execute()
        {
            win = new FabricationWindow();
            WindowInteropHelper wih = new WindowInteropHelper(win);
            wih.Owner = Process.GetCurrentProcess().MainWindowHandle;

            win.ShowDialog();
        }

        public void Terminate()
        {
            try { win?.Close(); } catch { }
            // Database.Clear() and ProductDatabase.Clear() removed —
            // they can trigger internal Fabrication API threads that block
            // AppDomain unload. The process is exiting; OS reclaims resources.
        }
    }

    public class ACADSample : IExtensionApplication
    {
        public static string AcadYear { get; private set; }
        FabricationWindow _win = null;

        /// <summary>
        /// Check this before Dispatcher calls to avoid deadlocks during shutdown.
        /// </summary>
        public static bool IsShuttingDown { get; private set; }

        [CommandMethod("FabAPI", "FabAPI", CommandFlags.Modal)]
        public void RunFabApi()
        {
            //if (CheckCadMepLoaded() && CheckApiLoaded())
            //{
            _win = new FabricationWindow();
            WindowInteropHelper wih = new WindowInteropHelper(_win);
            wih.Owner = Process.GetCurrentProcess().MainWindowHandle;
            _win.ShowDialog();
            //}
        }

        public void Initialize()
        {
            //CheckCadMepLoaded();
            //CheckApiLoaded();
            var acadpath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            AcadYear = new String(acadpath.ToCharArray().Where(p => char.IsDigit(p) == true).ToArray());
            try
            {
                Assembly.LoadFrom($@"C:\Program Files\Autodesk\Fabrication {AcadYear}\CADmep\FabricationAPI.dll");
            }
            catch { }

            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nLoaded FabricationSample add-in!");
        }

        public void Terminate()
        {
            // Set shutdown flag first — this prevents background threads from
            // calling Dispatcher.Invoke, which would deadlock and prevent the
            // AppDomain from unloading (CannotUnloadAppDomainException).
            IsShuttingDown = true;

            // Close the window if still open
            try { _win?.Close(); } catch { }
            _win = null;

            // Do NOT call Database.Clear() or ProductDatabase.Clear() here.
            // These Fabrication API calls can spin up internal threads or access
            // native resources that block AppDomain unload, causing the
            // CannotUnloadAppDomainException and triggering Autodesk CER.
            // The process is exiting — OS will reclaim all resources.
        }

        #region Fabrication API Checking routines
        private bool CheckApiLoaded()
        {
            try
            {
                var fabApi = AppDomain.CurrentDomain.GetAssemblies()
                  .Where(x => !x.IsDynamic)
                  .FirstOrDefault(x => Path.GetFileName(x.Location).Equals("FabricationAPI.dll", StringComparison.OrdinalIgnoreCase));

                var loaded = fabApi != null;

                if (!loaded)
                {
                    var fabCore = AppDomain.CurrentDomain.GetAssemblies()
                      .Where(x => !x.IsDynamic)
                      .FirstOrDefault(x => Path.GetFileName(x.Location).Equals("FabricationCore.dll", StringComparison.OrdinalIgnoreCase));

                    if (fabCore != null)
                    {
                        var directory = Path.GetDirectoryName(fabCore.Location);
                        fabApi = Assembly.LoadFrom(Path.Combine(directory, "FabricationAPI.dll"));
                        loaded = fabApi != null;
                    }
                }

                if (!loaded)
                {
                    System.Windows.Forms.MessageBox.Show("FabricationAPI.dll could not be loaded", "Fabrication API",
                      System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                }

                return loaded;
            }
            catch (System.Exception e)
            {
                return false;
            }
        }

        private bool CheckCadMepLoaded()
        {
            var modules = SystemObjects.DynamicLinker.GetLoadedModules().Cast<string>().ToList();
            var cadMepLoaded = modules
              .Where(x => x.ToLower().Contains("cadmep"))
              .Where(x => x.ToLower().Contains(".arx"))
              .Any();

            if (!cadMepLoaded)
                System.Windows.Forms.MessageBox.Show("CADmep is not loaded and is required to run this addin", "Fabrication API",
                  System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);

            return cadMepLoaded;
        }
        #endregion

    }
}

