﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Network;

namespace Terminals.Forms
{
    /// <summary>
    /// Responsible to create and connect connections user interface
    /// </summary>
    internal class ConnectionsUiFactory
    {
        private readonly MainForm mainForm;
        private readonly TerminalTabsSelectionControler terminalsControler;

        internal ConnectionsUiFactory(MainForm mainForm, TerminalTabsSelectionControler terminalsControler)
        {
            this.mainForm = mainForm;
            this.terminalsControler = terminalsControler;
        }

        internal void CreateCaptureManagerTab()
        {
            string captureTitle = Program.Resources.GetString("CaptureManager");
            TerminalTabControlItem terminalTabPage = new TerminalTabControlItem(captureTitle);
            try
            {
                terminalTabPage.AllowDrop = false;
                terminalTabPage.ToolTipText = captureTitle;
                terminalTabPage.Favorite = null;
                this.mainForm.AssingDoubleClickEventHandler(terminalTabPage);
                this.terminalsControler.AddAndSelect(terminalTabPage);
                this.mainForm.UpdateControls();

                IConnection conn = new CaptureManagerConnection();
                conn.TerminalTabPage = terminalTabPage;
                conn.ParentForm = this.mainForm;
                conn.Connect();
                (conn as Control).BringToFront();
                (conn as Control).Update();

                this.mainForm.UpdateControls();
            }
            catch (Exception exc)
            {
                Logging.Log.Error("Error loading the Capture Manager Tab Page", exc);
                this.terminalsControler.RemoveAndUnSelect(terminalTabPage);
                terminalTabPage.Dispose();
            }
        }

        internal void OpenNetworkingTools(NettworkingTools action, string host)
        {
            var terminalTabPage = new TerminalTabControlItem(Program.Resources.GetString("NetworkingTools"));
            try
            {
                terminalTabPage.AllowDrop = false;
                terminalTabPage.ToolTipText = Program.Resources.GetString("NetworkingTools");
                terminalTabPage.Favorite = null;
                this.mainForm.AssingDoubleClickEventHandler(terminalTabPage);
                this.terminalsControler.AddAndSelect(terminalTabPage);
                this.mainForm.UpdateControls();
                using (var conn = new NetworkingToolsConnection())
                {
                    conn.TerminalTabPage = terminalTabPage;
                    conn.ParentForm = this.mainForm;
                    conn.Connect();
                    conn.BringToFront();
                    conn.Update();
                    this.mainForm.UpdateControls();
                    conn.Execute(action, host);
                }
            }
            catch (Exception exc)
            {
                Logging.Log.Error("Open Networking Tools Failure", exc);
                this.terminalsControler.RemoveAndUnSelect(terminalTabPage);
                terminalTabPage.Dispose();
            }
        }

        internal void Connect(String connectionName, Boolean forceConsole, Boolean forceNewWindow, ICredentialSet credential = null)
        {
            IFavorite existingFavorite = Persistence.Instance.Favorites[connectionName];
            IFavorite favorite = FavoritesFactory.GetFavoriteUpdatedCopy(connectionName,
                forceConsole, forceNewWindow, credential);

            if (favorite != null)
            {
                Persistence.Instance.ConnectionHistory.RecordHistoryItem(existingFavorite);
                this.mainForm.SendNativeMessageToFocus();
                this.CreateTerminalTab(favorite);
            }
            else
                this.CreateNewTerminal(connectionName);
        }

        internal void CreateNewTerminal(String name = null)
        {
            using (var frmNewTerminal = new NewTerminalForm(name))
            {
                TerminalFormDialogResult result = frmNewTerminal.ShowDialog();
                if (result != TerminalFormDialogResult.Cancel)
                {
                    string favoriteName = frmNewTerminal.Favorite.Name;
                    this.mainForm.FocusFavoriteInQuickConnectCombobox(favoriteName);

                    if (result == TerminalFormDialogResult.SaveAndConnect)
                        this.CreateTerminalTab(frmNewTerminal.Favorite);
                }
            }
        }

        internal void CreateReleaseTab()
        {
            this.CreateTerminalTab(FavoritesFactory.CreateReleaseFavorite());
        }

        internal void CreateTerminalTab(IFavorite favorite)
        {
            CallExecuteBeforeConnectedFromSettings();
            CallExecuteFeforeConnectedFromFavorite(favorite);

            TerminalTabControlItem terminalTabPage = CreateTerminalTabPageByFavoriteName(favorite);
            this.TryConnectTabPage(favorite, terminalTabPage);
        }

        private static void CallExecuteBeforeConnectedFromSettings()
        {
            if (Settings.ExecuteBeforeConnect && !string.IsNullOrEmpty(Settings.ExecuteBeforeConnectCommand))
            {
                var processStartInfo = new ProcessStartInfo(Settings.ExecuteBeforeConnectCommand, Settings.ExecuteBeforeConnectArgs);
                processStartInfo.WorkingDirectory = Settings.ExecuteBeforeConnectInitialDirectory;
                Process process = Process.Start(processStartInfo);
                if (Settings.ExecuteBeforeConnectWaitForExit)
                {
                    process.WaitForExit();
                }
            }
        }

        private static void CallExecuteFeforeConnectedFromFavorite(IFavorite favorite)
        {
            IBeforeConnectExecuteOptions executeOptions = favorite.ExecuteBeforeConnect;
            if (executeOptions.Execute && !string.IsNullOrEmpty(executeOptions.Command))
            {
                var processStartInfo = new ProcessStartInfo(executeOptions.Command, executeOptions.CommandArguments);
                processStartInfo.WorkingDirectory = executeOptions.InitialDirectory;
                Process process = Process.Start(processStartInfo);
                if (executeOptions.WaitForExit)
                {
                    process.WaitForExit();
                }
            }
        }

        private static TerminalTabControlItem CreateTerminalTabPageByFavoriteName(IFavorite favorite)
        {
            String terminalTabTitle = favorite.Name;
            if (Settings.ShowUserNameInTitle)
            {
                var security = favorite.Security;
                string title = HelperFunctions.UserDisplayName(security.Domain, security.UserName);
                terminalTabTitle += String.Format(" ({0})", title);
            }

            return new TerminalTabControlItem(terminalTabTitle);
        }

        private void TryConnectTabPage(IFavorite favorite, TerminalTabControlItem terminalTabPage)
        {
            try
            {
                this.mainForm.AssignEventsToConnectionTab(favorite, terminalTabPage);
                IConnection conn = ConnectionManager.CreateConnection(favorite, terminalTabPage, this.mainForm);
                this.UpdateConnectionTabPageByConnectionState(favorite, terminalTabPage, conn);

                if (conn.Connected && favorite.NewWindow)
                {
                    this.terminalsControler.DetachTabToNewWindow(terminalTabPage);
                }
            }
            catch (Exception exc)
            {
                Logging.Log.Error("Error Creating A Terminal Tab", exc);
                this.terminalsControler.UnSelect();
            }
        }

        private void UpdateConnectionTabPageByConnectionState(IFavorite favorite, TerminalTabControlItem terminalTabPage, IConnection conn)
        {
            if (conn.Connect())
            {
                (conn as Control).BringToFront();
                (conn as Control).Update();
                this.mainForm.UpdateControls();
                if (favorite.Display.DesktopSize == DesktopSize.FullScreen)
                    this.mainForm.FullScreen = true;

                var b = conn as Connection;
                //b.OnTerminalServerStateDiscovery += new Connection.TerminalServerStateDiscovery(this.b_OnTerminalServerStateDiscovery);
                if (b != null)
                    b.CheckForTerminalServer(favorite);
            }
            else
            {
                String msg = Program.Resources.GetString("SorryTerminalswasunabletoconnecttotheremotemachineTryagainorcheckthelogformoreinformation");
                MessageBox.Show(msg);
                this.terminalsControler.RemoveAndUnSelect(terminalTabPage);
            }
        }
    }
}