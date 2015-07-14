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
    public class SettingsTab : TabPage
    {
        public struct ConnectionSettingsStruct
        {
            public string host;
            public int port;
            public int period;

            public string username;
            public string password;
        }

        /// <summary>
        /// Same as RVClient.StreamDataV1 but with strings only
        /// </summary>
        /// <remarks>This struct is created to hold values of TextBox field which is string
        /// and postpone validation for later. It's not possible to hold invalid port number
        /// in int field before user applies data.</remarks>
        private struct StringsStreamData
        {
            public string proxiedName;
            public string streamName;
            public string port;

            /// <summary>
            /// Use RVClient.StreamDataV1 to initialize new struct
            /// </summary>
            /// <param name="streamData"></param>
            public StringsStreamData(RVClient.StreamDataV1 streamData)
            {
                this.proxiedName = streamData.proxiedName;
                this.streamName = streamData.streamName;
                this.port = streamData.port.ToString();
            }

            /// <summary>
            /// Convert StringsStreamData to RVClient.StreamDataV1
            /// </summary>
            /// <returns>Converted struct</returns>
            /// <remarks>This method uses Int32.Parse() which may throw an exception in case
            /// string port value conversion to int failes.</remarks>
            public RVClient.StreamDataV1 Convert()
            {
                RVClient.StreamDataV1 converted = new RVClient.StreamDataV1()
                {
                    port = Int32.Parse(this.port),
                    streamName = this.streamName,
                    proxiedName = this.proxiedName,
                };

                return converted;
            }
        }

        public event EventHandler DataApplied;

        /// <summary>
        /// Holds connection data.
        /// </summary>
        private ConnectionSettingsStruct connectionData;
        /// <summary>
        /// Holds connection data. Modifying this property will cause the controls holding connection information 
        /// to update itself.
        /// </summary>
        public ConnectionSettingsStruct ConnectionData
        {
            get
            {
                return connectionData;
            }
            set
            {
                connectionData = value;
                this.RestoreConnectionData();
            }
        }

        /// <summary>
        /// Holds stream data.
        /// </summary>
        /// <remarks>Maybe it's not nice to use RVClient's structure but this saves us time to convert
        /// between structure A and B which would both be the same</remarks>
        private List<RVClient.StreamDataV1> streamData;
        /// <summary>
        /// Holds stream data. Modifying this property will cause the controls holding stream information to update.
        /// </summary>
        public RVClient.StreamDataV1[] StreamData
        {
            get
            {
                RVClient.StreamDataV1[] array = new RVClient.StreamDataV1[streamData.Count()];
                streamData.CopyTo(array);
                return array;
            }
            set
            {
                foreach (RVClient.StreamDataV1 element in value)
                {
                    streamData.Add(element);
                    this.RestoreStreamData();
                }
            }
        }

        /// <summary>
        /// A place to hold the stream data before it is applied by the user with APPLY button.
        /// </summary>
        private List<StringsStreamData> temporaryStreamData;
        private bool isDataModified;
        /// <summary>
        /// Informs whether settings were modified and require to be applied or cancelled
        /// </summary>
        private bool IsDataModified
        {
            get
            {
                return this.isDataModified;
            }
            set
            {
                // When changes are made button are enabled
                this.controls[(int)ControlKeys.ApplyButton].Enabled = value;
                this.controls[(int)ControlKeys.CancelButton].Enabled = value;
                isDataModified = value;
            }
        }

        /// <summary>
        /// Keys allowing to access the specific control in the array
        /// </summary>
        /// <see cref="SettingsTab.controls"/>
        private enum ControlKeys { Host, Port, Period, Username, Password, StreamProxiedName, StreamLocalPort, StreamLocalName, StreamList, CancelButton, ApplyButton };
        /// <summary>
        /// Holds controls editable by user and makes the access easier.
        /// </summary>
        private Control[] controls;

        /// <summary>
        /// Set to true when list's IndexChanged event should not be processed.
        /// </summary>
        /// <remarks>This is necessary to set when stream proxied name is updated by user. Setting it to true
        /// prevents the currently updated text box to refresh while being edited.</remarks>
        bool blockIndexChanged;

        public SettingsTab()
        {
            this.Name = "settingsTab";
            this.Text = "Settings";

            this.connectionData = new ConnectionSettingsStruct();
            this.streamData = new List<RVClient.StreamDataV1>();
            this.temporaryStreamData = new List<StringsStreamData>();
            this.controls = new Control[Enum.GetNames(typeof(ControlKeys)).Length];
            this.blockIndexChanged = false;

            this.Controls.Add(GetTextBoxesWithLabelsLayout(
                new string[] { "Host", "Port", "Reconnection period (mins)" }, 
                new ControlKeys[] { ControlKeys.Host, ControlKeys.Port, ControlKeys.Period },
                50, 
                new Point(0, 0)));
            this.Controls.Add(GetTextBoxesWithLabelsLayout(
                new string[] { "Username", "Password" }, 
                new ControlKeys[] { ControlKeys.Username, ControlKeys.Password },
                50, 
                new Point(0, 50)));
            this.Controls.Add(GetStreamSelectorGroupBox());
            this.Controls.Add(GetButtonsLayout());

            this.IsDataModified = false;
            AddTextChangedHandlers(new ControlKeys[] 
            {
                ControlKeys.Host, 
                ControlKeys.Port, 
                ControlKeys.Period, 
                ControlKeys.Username, 
                ControlKeys.Password 
            });

            this.ParentChanged += delegate(Object o, EventArgs e)
            {
                if (this.Parent != null)
                {
                    AddSelectingHandler();
                }
            };
        }

        /// <summary>
        /// Adds Selecting event handler to the tab's parent
        /// </summary>
        /// <remarks>Parent is not available when the object is being created. For this reason
        /// assigning the Selecting handler must be postponed in the ParentChanged handler event.</remarks>
        private void AddSelectingHandler()
        {
            TabControl parent = (TabControl)this.Parent;
            parent.Deselecting += delegate(Object o, TabControlCancelEventArgs e)
            {
                if (e.TabPage != this || this.IsDataModified == false)
                {
                    return;
                }

                if (this.showMessageBox("Apply changes?", "   You left unapplied changes in the Settings tab.\n\n   Would you like to apply them now?"))
                {
                    if (!this.ApplyData())
                    {
                        // If validaiton fails action is cancelled anyway
                        e.Cancel = true;
                    }
                }
                else
                {
                    e.Cancel = true;
                }
            };
        }

        /// <summary>
        /// Event handler changing the layout state to notify that data was modified by user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBoxTextChangedHandler(Object sender, EventArgs e)
        {
            this.IsDataModified = true;
        }

        /// <summary>
        /// Add event handlers to selected TextBox object
        /// </summary>
        /// <param name="affectedControls">Array of affected controls</param>
        /// <remarks>Occurance of TextChange event in any of the affected controls will cause the
        /// Apply and Cancel Changes buttons to become enabled. </remarks>
        private void AddTextChangedHandlers(ControlKeys[] affectedControls)
        {
            foreach (ControlKeys key in affectedControls)
            {
                TextBox box = (TextBox)this.controls[(int)key];
                box.TextChanged += TextBoxTextChangedHandler;
            }
        }

        /// <summary>
        /// Show OK/Cancel MessageBox and return the result
        /// </summary>
        /// <param name="caption"></param>
        /// <param name="message"></param>
        /// <returns>Whether user pressed OK (true) or Cancel (false)</returns>
        private bool showMessageBox(string caption, string message)
        {
            DialogResult result = MessageBox.Show(message, caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (result == DialogResult.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Disables or enables 3 controls describing the stream data from user's modification
        /// </summary>
        /// <param name="enable">true = enable, false = disable</param>
        public void EnableStreamDataControls(bool enable)
        {
            this.controls[(int)ControlKeys.StreamLocalName].Enabled = enable;
            this.controls[(int)ControlKeys.StreamLocalPort].Enabled = enable;
            this.controls[(int)ControlKeys.StreamProxiedName].Enabled = enable;
        }

        /// <summary>
        /// Fill the stream data controls with information about currently selected stream.
        /// </summary>
        /// <remarks>If no stream is selected controls are cleared and become disabled.</remarks>
        private void SetStreamDataControls()
        {
            ListBox list = (ListBox)this.controls[(int)ControlKeys.StreamList];
            int index = list.SelectedIndex;
            TextBox localName = (TextBox) this.controls[(int)ControlKeys.StreamLocalName];
            TextBox localPort = (TextBox) this.controls[(int)ControlKeys.StreamLocalPort];
            TextBox proxiedName = (TextBox) this.controls[(int)ControlKeys.StreamProxiedName];

            this.UnmarkAllControls();

            if (index >= 0)
            {
                this.EnableStreamDataControls(true);
                localName.Text = temporaryStreamData[index].streamName;
                localPort.Text = temporaryStreamData[index].port.ToString();
                proxiedName.Text = temporaryStreamData[index].proxiedName;
            }
            else
            {
                localName.Clear();
                localPort.Clear();
                proxiedName.Clear();
                this.EnableStreamDataControls(false);
            }
        }


        /// <summary>
        /// Add the new stream to the list and temporary stream data with default values. The new item becomes selected.
        /// </summary>
        public void AddNewStream()
        {
            ListBox list = (ListBox) controls[(int)ControlKeys.StreamList];
            string name = "my_stream";
            int index = list.Items.Add(name);
            this.temporaryStreamData.Add(new StringsStreamData
            {
                port = "554", 
                proxiedName = name, 
                streamName = "my_local_stream_name"
            });
            this.IsDataModified = true;

            // Selection must be done after stream is added to temporary listy (event handling)
            list.SelectedIndex = index;
        }

        /// <summary>
        /// Delete the nth stream from the list
        /// </summary>
        /// <param name="n">Stream index in the ListBox control</param>
        public void DeleteStream(int n)
        {
            ListBox list = (ListBox) controls[(int)ControlKeys.StreamList];         
            list.Items.RemoveAt(n);
            this.temporaryStreamData.RemoveAt(n);
            this.IsDataModified = true;
        }

        /// <summary>
        /// Fills the controls with data saved in connectionData.
        /// </summary>
        /// <remarks>This can be used to initialize the controls with start values
        /// or reset them after cancel button click event.</remarks>
        private void RestoreConnectionData()
        {
            this.controls[(int)ControlKeys.Host].Text = this.connectionData.host;
            this.controls[(int)ControlKeys.Port].Text = this.connectionData.port.ToString();
            this.controls[(int)ControlKeys.Period].Text = this.connectionData.period.ToString();
            this.controls[(int)ControlKeys.Username].Text = this.connectionData.username;
            this.controls[(int)ControlKeys.Password].Text = this.connectionData.password;
        }

        /// <summary>
        /// Fills the controls with data saved in streamData.
        /// </summary>
        /// <remarks>This can be used to initialize the controls with start values
        /// or reset them after cancel button click event.</remarks>
        public void RestoreStreamData()
        {
            ListBox list = (ListBox) this.controls[(int)ControlKeys.StreamList];
            list.SelectedIndex = -1;
            list.Items.Clear();
            this.temporaryStreamData.Clear();

            // temporary stream data becomes a deep copy of the stream data list
            foreach(RVClient.StreamDataV1 element in this.streamData)
            {
                temporaryStreamData.Add(new StringsStreamData(element)); //element is struct so we're passing memory block not a reference
                list.Items.Add(element.proxiedName);
            }

            this.IsDataModified = false;
        }

        /// <summary>
        /// Mark control with a red border.
        /// </summary>
        /// <param name="control">Control to mark.</param>
        /// <param name="mark">Whether to mark control</param>
        private void MarkControl(TextBox control, bool mark)
        {
            if (mark)
            {
                control.BackColor = Color.FromArgb(255, 171, 171);
            }
            else
            {
                control.BackColor = Color.Empty;
            }
        }

        /// <summary>
        /// Unmarks textboxes
        /// </summary>
        private void UnmarkAllControls()
        {
            // Unmark controls (remove red background)
            foreach (ControlKeys key in new ControlKeys[] 
                {
                    ControlKeys.Host,
                    ControlKeys.Port,
                    ControlKeys.Period,
                    ControlKeys.Username,
                    ControlKeys.Password,
                    ControlKeys.StreamLocalName,
                    ControlKeys.StreamLocalPort,
                    ControlKeys.StreamProxiedName
                })
            {
                this.MarkControl((TextBox)this.controls[(int)key], false);
            }
        }

        /// <summary>
        /// Validate stream data saved in SettingsTab.temporaryStreamData with ClientLogic.InputValidator and show MessageBox with error message (if any)
        /// </summary>
        /// <returns>Result of validation</returns>
        /// <remarks>Additionally method selects stream data item on the list and marks the control in which validation error occured</remarks>
        private bool ValidateStreamData()
        {
            ListBox list = (ListBox) this.controls[(int)ControlKeys.StreamList];
            string errorMessage = "";
            int i = 0;

            foreach (StringsStreamData item in temporaryStreamData)
            {
                // TODO Remove code clones
                if (!ClientLogic.InputValidator.ValidateString(item.proxiedName, out errorMessage))
                {
                    list.SelectedIndex = i;
                    this.MarkControl((TextBox)this.controls[(int)ControlKeys.StreamProxiedName], true);
                    MessageBox.Show("Provided proxied stream name is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    return false;
                }

                if (!ClientLogic.InputValidator.ValidatePortNumber(item.port, out errorMessage))
                {
                    list.SelectedIndex = i;
                    this.MarkControl((TextBox)this.controls[(int)ControlKeys.StreamLocalPort], true);
                    MessageBox.Show("Provided local port number is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    return false;
                }

                if (!ClientLogic.InputValidator.ValidateString(item.streamName, out errorMessage))
                {
                    list.SelectedIndex = i;
                    this.MarkControl((TextBox)this.controls[(int)ControlKeys.StreamLocalName], true);
                    MessageBox.Show("Provided local stream name is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    return false;
                }

                ++i;
            }

            return true;
        }

        /// <summary>
        /// Validate controls with ClientLogic.InputValidator and show MessageBox with error message (if any)
        /// </summary>
        /// <returns>Result of validation</returns>
        /// <remarks>Additionally method marks the control in which validation error occured.</remarks>
        private bool ValidateControls()
        {
            string errorMessage;

            //TODO remove code clones
            if (!ClientLogic.InputValidator.ValidateString(this.controls[(int)ControlKeys.Host].Text, out errorMessage))
            {
                this.MarkControl((TextBox)this.controls[(int)ControlKeys.Host], true);
                MessageBox.Show("Provided host name is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!ClientLogic.InputValidator.ValidateString(this.controls[(int)ControlKeys.Username].Text, out errorMessage))
            {
                this.MarkControl((TextBox)this.controls[(int)ControlKeys.Username], true);
                MessageBox.Show("Provided username is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!ClientLogic.InputValidator.ValidatePassword(this.controls[(int)ControlKeys.Password].Text, out errorMessage))
            {
                this.MarkControl((TextBox) this.controls[(int)ControlKeys.Password], true);
                MessageBox.Show("Provided password is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!ClientLogic.InputValidator.ValidatePortNumber(this.controls[(int)ControlKeys.Port].Text, out errorMessage))
            {
                this.MarkControl((TextBox)this.controls[(int)ControlKeys.Port], true);
                MessageBox.Show("Provided server port number is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // We use port validator to validate period but that's ok
            if (!ClientLogic.InputValidator.ValidatePortNumber(this.controls[(int)ControlKeys.Period].Text, out errorMessage))
            {
                this.MarkControl((TextBox)this.controls[(int)ControlKeys.Period], true);
                MessageBox.Show("Provided period value is invalid: " + errorMessage, "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Saves the data placed in controls into internal structures.
        /// </summary>
        /// <remarks>After calling this method external objects become able to extract 
        /// data provided by user with ConnectionData and StreamData properties.</remarks>
        private bool ApplyData()
        {
            this.UnmarkAllControls();

            if (this.ValidateControls() && this.ValidateStreamData())
            {
                // Apply connection data
                this.connectionData.host = this.controls[(int)ControlKeys.Host].Text;
                this.connectionData.port = Int32.Parse(this.controls[(int)ControlKeys.Port].Text);
                this.connectionData.period = Int32.Parse(this.controls[(int)ControlKeys.Period].Text);
                this.connectionData.username = this.controls[(int)ControlKeys.Username].Text;
                this.connectionData.password = this.controls[(int)ControlKeys.Password].Text;

                // Apply stream data
                ListBox list = (ListBox)this.controls[(int)ControlKeys.StreamList];
                this.streamData.Clear();

                // Temporary stream data becomes a deep copy of the stream data list
                foreach (StringsStreamData element in this.temporaryStreamData)
                {
                    streamData.Add(element.Convert()); //element is struct so we're passing memory block not a reference
                }

                this.IsDataModified = false;
                list.SelectedIndex = -1;
                DataApplied(this, null); // Emit event

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Handles the ModifiedChanged event on stream data controls to store the value of 
        /// a textBox in temporary stream data
        /// </summary>
        /// <param name="sender">Control reference (used to determine what kind if data is 
        /// going to be processed)</param>
        /// <param name="e"></param>
        private void streamDataModifiedChangeHandler(Object sender, EventArgs e)
        {
            TextBox localName = (TextBox) this.controls[(int)ControlKeys.StreamLocalName];
            TextBox localPort = (TextBox) this.controls[(int)ControlKeys.StreamLocalPort];
            TextBox proxiedName = (TextBox) this.controls[(int)ControlKeys.StreamProxiedName];
            ListBox list = (ListBox) controls[(int)ControlKeys.StreamList];
            int index = list.SelectedIndex;

            // Since list holds struct we cannot modify only one field. The whole struct has to be replaced.
            StringsStreamData newStreamData = this.temporaryStreamData[index];

            if (sender == localName)
            {
                newStreamData.streamName = localName.Text;
                localName.Modified = false;
            }
            else if (sender == localPort)
            {
                newStreamData.port = localPort.Text;
                localPort.Modified = false;
            }
            else if (sender == proxiedName)
            {
                // Prevent event handler from updating the current field 
                this.blockIndexChanged = true;

                newStreamData.proxiedName = proxiedName.Text;
                list.Items[index] = proxiedName.Text; // This call would normally cause IndexChanged handler to interfere
                proxiedName.Modified = false;

                this.blockIndexChanged = false;
            }
            else
            {
                return;
            }

            // finally update the stream data item 
            this.temporaryStreamData[index] = newStreamData;
            this.IsDataModified = true;
        }

        /// ### Layout creation methods ###

        /// <summary>
        /// Creates, initializes and returns layout with stream selector group
        /// </summary>
        /// <returns></returns>
        private GroupBox GetStreamSelectorGroupBox()
        {
            GroupBox streamSelectorGroup = new GroupBox();
            streamSelectorGroup.Location = new Point(5, 100);
            streamSelectorGroup.Size = new Size(190, 200); // these are some strange values that give the right position only if anchors are set
            streamSelectorGroup.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            streamSelectorGroup.Text = "Stream data";

            ListBox list = new ListBox();
            list.AutoSize = true;
            list.Location = new Point(10, 20);
            list.Size = new Size(150, 160);
            list.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            list.SelectionMode = SelectionMode.One;
            this.controls[(int)ControlKeys.StreamList] = list;

            //Streams List Event Handlers
            list.SelectedIndexChanged += delegate(Object sender, EventArgs e)
            {
                if (this.blockIndexChanged == false)
                {
                    SetStreamDataControls();
                }
            };

            streamSelectorGroup.Controls.Add(list);

            FlowLayoutPanel streamInfoPanel = new FlowLayoutPanel();
            streamInfoPanel.Location = new Point(250, 20);
            streamInfoPanel.Size = new Size(160, 150);
            streamInfoPanel.FlowDirection = FlowDirection.TopDown;

            string[] textBoxLabels = { "Proxied stream name", "Local port number", "Local stream name" };
            ControlKeys[] keys = { ControlKeys.StreamProxiedName, ControlKeys.StreamLocalPort, ControlKeys.StreamLocalName };
            for (int i = 0; i < 3; i++)
            {
                Label label = new Label();
                label.Text = textBoxLabels[i];
                label.AutoSize = true;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.Padding = new Padding(0, 10, 0, 0);
                streamInfoPanel.Controls.Add(label);

                TextBox textBox = new TextBox();
                textBox.Width = 150;
                streamInfoPanel.Controls.Add(textBox);
                this.controls[(int)keys[i]] = textBox;

                textBox.ModifiedChanged += this.streamDataModifiedChangeHandler;
            }
            // Disable controls until element on the list becomes selected
            this.EnableStreamDataControls(false);

            streamSelectorGroup.Controls.Add(streamInfoPanel);

            // TODO icon for button
            Button addButton = new Button();
            addButton.Location = new Point(170, 165);
            addButton.Size = new Size(25, 25);
            addButton.Text = "+";

            // Add Button Event Handlers
            addButton.Click += delegate(Object sender, EventArgs e)
            {
                this.AddNewStream();
            };

            // TODO icon for button
            Button deleteButton = new Button();
            deleteButton.Location = new Point(200, 165);
            deleteButton.Size = new Size(25, 25);
            deleteButton.Text = "x";

            // Delete Button Event Handlers
            deleteButton.Click += delegate(Object sender, EventArgs e)
            {
                ListBox l = (ListBox)this.controls[(int)ControlKeys.StreamList];
                int n = l.SelectedIndex;

                // delete only if anything is selected
                if (n >= 0)
                {
                    this.DeleteStream(l.SelectedIndex);
                    if (n == 0 && l.Items.Count > 0)
                    {
                        // if the removed element was first, again the first one becomes selected
                        l.SelectedIndex = 0;
                    }
                    else if (l.Items.Count > 0)
                    {
                        // else the previous element is selected
                        l.SelectedIndex = n - 1;
                    }
                }
            };

            streamSelectorGroup.Controls.Add(addButton);
            streamSelectorGroup.Controls.Add(deleteButton);

            return streamSelectorGroup;
        }

        /// <summary>
        /// Returns table layout with pairs TextBox and Labels
        /// </summary>
        /// <param name="labels">Labels describing text boxes</param>
        /// <param name="keys">Keys to store the created text boxes in this.controls</param>
        /// <param name="height">Height of the whole table layout</param>
        /// <param name="location">Location of the whole table layout</param>
        /// <returns>Initialized table layout</returns>
        private TableLayoutPanel GetTextBoxesWithLabelsLayout(string[] labels, ControlKeys[] keys, int height, Point location)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = labels.Length;
            layout.RowCount = 2;

            layout.Location = location;
            layout.Height = height;
            layout.Padding = new Padding(5);

            layout.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top;

            TableLayoutColumnStyleCollection styles = layout.ColumnStyles;
            for (int i = 0; i < layout.ColumnCount; i++)
            {
                // Make all columns equal size
                ColumnStyle columnStyle = new ColumnStyle(SizeType.Percent, 100 / layout.ColumnCount);
                styles.Add(columnStyle);

                Label label = new Label();
                label.Text = labels[i];
                label.AutoSize = true;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleCenter;
                layout.Controls.Add(label, i, 0);

                TextBox textBox = new TextBox();
                textBox.AutoSize = true;
                textBox.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
                layout.Controls.Add(textBox, i, 1);
                this.controls[(int)keys[i]] = textBox;
            }

            return layout;
        }

        private Panel GetButtonsLayout()
        {
            FlowLayoutPanel layout = new FlowLayoutPanel();
            layout.FlowDirection = FlowDirection.LeftToRight;
            layout.Margin = new Padding(10);
            layout.Size = new Size(200, 200);
            layout.Location = new Point(250, 310);
            //layout.Anchor = AnchorStyles.Right;

            Button applyButton = new Button();
            applyButton.Text = "Apply";
            this.controls[(int)ControlKeys.ApplyButton] = applyButton;

            // Apply Button Event Handlers
            applyButton.Click += delegate(Object sender, EventArgs s)
            {
                this.ApplyData();
            };

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel Changes";
            cancelButton.AutoSize = true;
            this.controls[(int)ControlKeys.CancelButton] = cancelButton;

            // Cancel Button Event Handlers
            cancelButton.Click += delegate(Object sender, EventArgs e)
            {
                this.UnmarkAllControls();
                this.RestoreConnectionData();
                this.RestoreStreamData();
            };

            layout.Controls.Add(applyButton);
            layout.Controls.Add(cancelButton);

            return layout;
        }
    }
}
