using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Reflection;
using ProMsgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using PRZH = NCC.PRZTools.PRZHelper;

namespace NCC.PRZTools
{
    internal class Button_Export_PRZ : Button
    {

        protected override async void OnClick()
        {
            try
            {
                #region VALIDATE PROJECT WORKSPACE AND CONTENTS

                // Verify that the Project Folder exists
                if (!PRZH.FolderExists_Project().exists)
                {
                    ProMsgBox.Show($"Unable to retrieve Project Folder at: {PRZH.GetPath_ProjectFolder()}");
                    return;
                }

                // Verify that the Project Geodatabase exists
                var try_exists = await PRZH.GDBExists_Project();
                if (!try_exists.exists)
                {
                    ProMsgBox.Show($"Unable to retrieve project geodatabase.\n{try_exists.message}");
                    return;
                }

                #endregion

                #region SHOW DIALOG

                ExportWTW dlg = new ExportWTW();                    // View
                ExportWTWVM vm = (ExportWTWVM)dlg.DataContext;      // View Model

                dlg.Owner = FrameworkApplication.Current.MainWindow;

                // Closing event handler
                dlg.Closing += (o, e) =>
                {
                    // Event handler for Dialog closing event
                    if (vm.OperationIsUnderway)
                    {
                        ProMsgBox.Show("Operation is underway.  Please cancel the operation before closing this window.");
                        e.Cancel = true;
                    }
                };

                // Closed Event Handler
                dlg.Closed += (o, e) =>
                {
                    // Event Handler for Dialog close in case I need to do things...
                    // ProMsgBox.Show("Closed...");
                    // System.Diagnostics.Debug.WriteLine("Pro Window Dialog Closed";)
                };

                // Loaded Event Handler
                dlg.Loaded += async (sender, e) =>
                {
                    if (vm != null)
                    {
                        await vm.OnProWinLoaded();
                    }
                };

                var result = dlg.ShowDialog();

                // Take whatever action required here once the dialog is closed (true or false)
                // do stuff here!

                #endregion
            }
            catch (Exception ex)
            {
                ProMsgBox.Show(ex.Message + Environment.NewLine + "Error in method: " + MethodBase.GetCurrentMethod().Name);
            }
        }

    }
}
