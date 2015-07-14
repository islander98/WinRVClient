// Copyright Piotr Trojanowski 2015

// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2.1 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WinRVClient
{
    public partial class MainWindow : Form
    {
        public TabControl tabControl;
        public StatusTab statusTab;
        public SettingsTab settingsTab;
        public TabPage aboutTab;
        private ClientLogic logic;
        public StatusStrip statusBar;
        private System.Timers.Timer progressBarTimer;

        /// <summary>
        /// Applied connection data retrieved from Settings tab
        /// </summary>
        public SettingsTab.ConnectionSettingsStruct ConnectionData
        {
            get
            {
                return settingsTab.ConnectionData;
            }
            set
            {
                settingsTab.ConnectionData = value;
            }
        }

        /// <summary>
        /// Applied stream data retrieved from Settings tab
        /// </summary>
        public RVClient.StreamDataV1[] StreamData
        {
            get
            {
                return settingsTab.StreamData;
            }
            set
            {
                settingsTab.StreamData = value;
            }
        }

        /// <summary>
        /// Describes current application status
        /// </summary>
        public enum JobStatus { Error, Working, Idle };

        private JobStatus status;

        /// <summary>
        /// Current application status reflected in status bar color and label
        /// </summary>
        public JobStatus Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value;

                if (status == JobStatus.Working)
                {
                    this.statusBar.Items[0].Text = "Current Status: WORKING";
                    this.statusBar.BackColor = Color.FromArgb(181, 255, 198);
                    this.statusTab.EnableButtons(false, true);
                }
                else if (status == JobStatus.Idle)
                {
                    this.statusBar.Items[0].Text = "Current Status: IDLE";
                    this.statusBar.BackColor = Color.FromArgb(255, 245, 181);
                    this.statusTab.EnableButtons(true, false);
                }
                else if (status == JobStatus.Error)
                {
                    this.statusBar.Items[0].Text = "Current Status: WORKING / ERROR";
                    this.statusBar.BackColor = Color.FromArgb(255, 171, 171);
                }
            }
        }

        /// <summary>
        /// Creates all the controls and fills it with data from settings file
        /// </summary>
        /// <param name="logic">ClientLogic object used to control the window</param>
        public MainWindow(ClientLogic logic)
        {
            this.logic = logic;

            this.SuspendLayout();

            this.ClientSize = new Size(450, 400);
            this.Name = "MainWindow";
            this.Text = "WinRVClient";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            InitializeComponent();

            this.ResumeLayout(false);

            // Previous / Default settings loader
            SettingsTab.ConnectionSettingsStruct c;
            RVClient.StreamDataV1[] s;
            if (this.logic.LoadSettings(this, out c, out s))
            {
                // TODO This way settings loaded from file are not validated. 
                //      Probably we should somehow handle. Hovewer, methods with validation 
                //      logic are inside SettingsTab. Maybe it was wrong design concept
                //      but made it easier to mark controls where the problem occurs.

                this.ConnectionData = c;
                this.StreamData = s;
            }
        }

        /// <summary>
        /// Initialize sub-components
        /// </summary>
        private void InitializeComponent()
        {
            this.InitializeTabControl();
            this.InitializeStatusBar();
        }

        /// <summary>
        /// Initialize TabControl along with tab pages
        /// </summary>
        private void InitializeTabControl()
        {
            this.tabControl = new TabControl();
            this.tabControl.Dock = DockStyle.Fill;

            // Status Tab events
            this.statusTab = new StatusTab();
            this.statusTab.StartButtonClick += delegate(Object o, EventArgs e)
            {
                this.logic.Start(this);
            };
            this.statusTab.StopButtonClick += delegate(Object o, EventArgs e)
            {
                this.logic.Stop(this);
            };
            this.statusTab.NowButtonClick += delegate(Object o, EventArgs e)
            {
                this.logic.ConnectNow(this);
            };

            // Settings Tab events
            this.settingsTab = new SettingsTab();
            this.settingsTab.DataApplied += delegate(Object o, EventArgs e)
            {
                this.logic.SaveSettings(this, this.settingsTab.ConnectionData, this.settingsTab.StreamData);

                // If current state is not IDLE then we notify the user that 
                // he must restart the job for the changes to take effect
                if (this.Status != JobStatus.Idle)
                {
                    MessageBox.Show("The changes will have effect only when you start the schedule again.", "Schedule must be restarted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            this.aboutTab = this.getAboutTab();

            this.tabControl.TabPages.Add(this.statusTab);
            this.tabControl.TabPages.Add(this.settingsTab);
            this.tabControl.TabPages.Add(this.aboutTab);

            this.Controls.Add(this.tabControl);
        }

        /// <summary>
        /// Initialize status bar
        /// </summary>
        private void InitializeStatusBar()
        {
            this.statusBar = new StatusStrip();
            this.statusBar.Dock = DockStyle.Bottom;
            this.statusBar.SizingGrip = false;

            ToolStripStatusLabel label = new ToolStripStatusLabel();
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Spring = true;
            this.statusBar.Items.Add(label);

            this.progressBarTimer = new System.Timers.Timer(500);
            this.progressBarTimer.AutoReset = true;
            this.progressBarTimer.SynchronizingObject = this;
            this.progressBarTimer.Elapsed += delegate(Object source, System.Timers.ElapsedEventArgs e)
            {
                UpdateProgressBar();
            };

            this.Controls.Add(this.statusBar);
        }

        private void UpdateProgressBar()
        {
            // Check if progress bar is enabled
            if (this.statusBar.Items.Count != 2)
            {
                return;
            }
           
            ToolStripProgressBar progressBar = (ToolStripProgressBar) this.statusBar.Items[1];
            int step = 20;

            // Hacks to avoid progress bar animation
            if (progressBar.Value == progressBar.Maximum)
            {
                progressBar.Value = progressBar.Minimum;
            }
            else
            {
                if (progressBar.Value + step + 1 > progressBar.Maximum)
                {
                    progressBar.Maximum += 1;
                }

                progressBar.Value += (step + 1);
                progressBar.Value -= 1;

                if (progressBar.Maximum == progressBar.Value + 1)
                {
                    progressBar.Maximum -= 1;
                }
            }
        }

        /// <summary>
        /// Write log message to the RichTextBox on Status tab
        /// </summary>
        /// <param name="message"></param>
        public void WriteLog(string message)
        {
            this.statusTab.WriteLog(message);
        }

        /// <summary>
        /// WriteLog() overload allowing to synchronize with UI thread
        /// </summary>
        /// <param name="message"></param>
        /// <param name="synchronized">Whether to perform write in UI thread</param>
        /// <remarks>When synchronized is true the method is blocking due to
        /// using Invoke() method to synchronize it with the UI thread.</remarks>
        public void WriteLog(string message, bool synchronized)
        {
            if (synchronized)
            {
                this.Invoke((System.Windows.Forms.MethodInvoker)delegate()
                {
                    this.WriteLog(message);
                });
            }
            else
            {
                this.WriteLog(message);
            }
        }

        /// <summary>
        /// Show progress bar in status bar
        /// </summary>
        /// <param name="enable">Whether to show the bar</param>
        /// <remarks>Bar is meant not to show the progress status but only be visible
        /// as animated control. It's value is updated periodically in timer tasks.
        /// 
        /// This method is blocking due to Invoke() used to synchronize it with UI thread.
        /// </remarks>
        public void EnableProgressBar(bool enable)
        {
            this.Invoke((System.Windows.Forms.MethodInvoker)delegate()
            {
                // Status Bar has 1 item - label only; progress bar is not added yet
                if (enable && this.statusBar.Items.Count == 1)
                {
                    ToolStripProgressBar progressBar = new ToolStripProgressBar();
                    progressBar.Dock = DockStyle.Right;
                    progressBar.AutoSize = false;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = 100;
                    progressBar.Width = 50;
                    progressBar.Value = 0;
                    this.statusBar.Items.Add(progressBar);

                    this.progressBarTimer.Enabled = true;
                }
                // Status Bar has 2 items - label and progress bar; we can remove it
                else if (!enable && this.statusBar.Items.Count == 2)
                {
                    this.statusBar.Items.RemoveAt(1);
                    this.progressBarTimer.Enabled = false;
                }
            });
        }
    }
}
