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
    public class StatusTab : TabPage
    {
        public enum MessageCode { Welcome };
        private static string[] messages =
        {
            "Welcome to WinRVClient version " + ClientLogic.Version
        };

        /// <summary>
        /// Start Button click
        /// </summary>
        public event EventHandler StartButtonClick;
        /// <summary>
        /// Stop Button click
        /// </summary>
        public event EventHandler StopButtonClick;
        /// <summary>
        /// Connect Now Button click
        /// </summary>
        public event EventHandler NowButtonClick;

        private RichTextBox logBox;
        private Button startButton;
        private Button stopButton;
        private Button connectNowButton;

        public StatusTab()
        {
            this.Name = "statusTab";
            this.Text = "Status";

            this.logBox = this.GetLogBox();
            this.Controls.Add(this.logBox);

            this.Controls.Add(this.GetButtons());
            
            WriteLog(MessageCode.Welcome);
        }

        /// <summary>
        /// Writes custom log message to log box and scrolls the content
        /// </summary>
        /// <param name="message">Message content</param>
        public void WriteLog(string message)
        {
            this.logBox.Text += GetFormattedTime() + message + "\n";
            this.logBox.SelectionStart = this.logBox.Text.Length;
            this.logBox.ScrollToCaret();
        }

        /// <summary>
        /// Writes predefined log message to log box and scrolls the content
        /// </summary>
        /// <param name="code">Predefined message code</param>
        public void WriteLog(MessageCode code)
        {
            this.logBox.Text += GetFormattedTime() + messages[(int)code] + "\n";
            this.logBox.SelectionStart = this.logBox.Text.Length;
            this.logBox.ScrollToCaret();
        }

        /// <summary>
        /// Provides a way to enable/disable buttons
        /// </summary>
        /// <param name="startButton">whether to enable start button</param>
        /// <param name="stopButton">whether to enable stop button</param>
        /// <remarks>By default both buttons are enabled.</remarks>
        public void EnableButtons(bool startButton, bool stopButton)
        {
            this.startButton.Enabled = startButton;
            this.stopButton.Enabled = stopButton;
        }

        /// <summary>
        /// Returns time formatted into a string
        /// </summary>
        /// <returns>Time string</returns>
        private string GetFormattedTime()
        {
            return DateTime.Now.ToString("[HH:mm] "); // TODO Take system 12/24 hour clock setting 
        }

        /// <summary>
        /// Returns RichTextBox ready to store application logs
        /// </summary>
        /// <returns>RichTextBox control</returns>
        private RichTextBox GetLogBox()
        {
            RichTextBox box = new RichTextBox();
            box.Size = new Size(380, 200);
            box.Location = new Point(30, 40);
            box.Font = new Font("Microsoft San Serif", 10, FontStyle.Regular);
            box.ReadOnly = true;
            box.BackColor = Color.White;

            return box;
        }

        /// <summary>
        /// Returns Panel with Stop, Start, Connect Now buttons
        /// </summary>
        /// <returns>Flow Layout</returns>
        private FlowLayoutPanel GetButtons()
        {
            int spaceBetween = 30;

            FlowLayoutPanel layout = new FlowLayoutPanel();
            layout.FlowDirection = FlowDirection.LeftToRight;
            layout.AutoSize = true;
            layout.Location = new Point(35, 275);
            //layout.Anchor = AnchorStyles.Right;

            this.stopButton = new Button();
            this.stopButton.Margin = new Padding(0, 0, spaceBetween / 2, 0);
            this.stopButton.Text = "Stop";

            // Stop Button Event Handlers
            this.stopButton.Click += delegate(Object sender, EventArgs e)
            {
                StopButtonClick(sender, e);
            };

            this.startButton = new Button();
            this.startButton.Margin = new Padding(spaceBetween / 2, 0, spaceBetween / 2, 0);
            this.startButton.Size = new Size(150, 50);
            this.startButton.Text = "START";
            this.startButton.Font = new Font("Microsoft San Serif", 12, FontStyle.Regular);

            // Start Button Event Handlers
            this.startButton.Click += delegate(Object sender, EventArgs e)
            {
                StartButtonClick(sender, e);
            };

            this.connectNowButton = new Button();
            this.connectNowButton.Margin = new Padding(spaceBetween / 2, 0, 0, 0);
            this.connectNowButton.AutoSize = true;
            this.connectNowButton.Text = "Connect Now";

            // Connect Now Button Event Handlers
            this.connectNowButton.Click += delegate(Object sender, EventArgs e)
            {
                NowButtonClick(sender, e);
            };

            layout.Controls.Add(this.stopButton);
            layout.Controls.Add(this.startButton);
            layout.Controls.Add(this.connectNowButton);

            return layout;
        }
    }
}
