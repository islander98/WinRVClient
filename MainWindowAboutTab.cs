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
    public partial class MainWindow
    {
        private TabPage getAboutTab()
        {
            TabPage page = new TabPage();
            page.Name = "aboutTab";
            page.Text = "About";

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.ColumnCount = 1;
            layout.RowCount = 4;

            layout.Location = new Point(0, 0);
            layout.Padding = new Padding(5);

            layout.Anchor = AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;

            Label appName = new Label();
            appName.Text = "WinRVClient";
            appName.Font = new Font(new FontFamily("Arial Black"), 16, FontStyle.Regular);
            appName.Dock = DockStyle.Fill;
            appName.TextAlign = ContentAlignment.MiddleCenter;
            appName.Height = 50;
            layout.Controls.Add(appName, 0, 0);

            Label appVersion = new Label();
            appVersion.Text = "Version " + ClientLogic.Version;
            appVersion.Font = new Font(new FontFamily("Arial"), 10, FontStyle.Regular);
            appVersion.Dock = DockStyle.Fill;
            appVersion.TextAlign = ContentAlignment.MiddleCenter;
            appVersion.Height = 40;
            layout.Controls.Add(appVersion, 0, 1);

            Label appCopyright = new Label();
            appCopyright.Text = "Copyright 2015 Piotr Trojanowski";
            appCopyright.Font = new Font(new FontFamily("Arial"), 10, FontStyle.Regular);
            appCopyright.Dock = DockStyle.Fill;
            appCopyright.TextAlign = ContentAlignment.MiddleCenter;
            appCopyright.Height = 40;
            layout.Controls.Add(appCopyright, 0, 2);

            // This glue label is empty but extends to the end of the window.
            // Might be used to add some more info.
            Label glue = new Label();
            glue.AutoSize = true;
            glue.Dock = DockStyle.Fill;
            glue.TextAlign = ContentAlignment.TopLeft;
            layout.Controls.Add(glue, 0, 3);

            page.Controls.Add(layout);
            return page;
        }
    }
}
