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
using System.Linq;
using System.Text;
using System.Timers;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

namespace WinRVClient
{
    public partial class ClientLogic
    {
        public class SerializedSettings
        {
            public SettingsTab.ConnectionSettingsStruct connectionData;
            public RVClient.StreamDataV1[] streamData;
        }

        public class ThreadContext
        {
            public SettingsTab.ConnectionSettingsStruct connectionData;
            public RVClient.StreamDataV1[] streamData;
            public MainWindow view;
        }

        public const string Version = "0.9";

        private SettingsTab.ConnectionSettingsStruct connectionData;
        private RVClient.StreamDataV1[] streamData;
        private System.Timers.Timer timer;
        private Object isConnectingLock;
        private bool isConnecting;

        public ClientLogic()
        {
            isConnectingLock = new Object();
            isConnecting = false;
        }

        /// <summary>
        /// Connect to RVServer, send data, disconnect
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <param name="connectionData"></param>
        /// <param name="streamData"></param>
        private void ConnectOnce(MainWindow view, SettingsTab.ConnectionSettingsStruct connectionData, RVClient.StreamDataV1[] streamData)
        {
            // Comments on object synchronization
            ThreadContext context = new ThreadContext();
            context.connectionData = connectionData; // struct - needs no syncrhonization
            context.streamData = streamData; // array - this object is created every time when accessing MainWindow's property;
            // it can be also this.streamData reference but it is never modified in this class
            context.view = view; // passed only to call Invoke()
            // Conclusion: everything here is threadsafe.

            Thread thread = new Thread(delegate(Object _tContext)
            {
                ThreadContext tContext = (ThreadContext)_tContext;

                lock (this.isConnectingLock)
                {
                    // If other thread is already connecting don't do the same
                    if (this.isConnecting)
                    {
                        tContext.view.WriteLog("Warning: Can't start new connection because the previous one is still handled.", true);
                        return;
                    }
                    else
                    {
                        this.isConnecting = true;
                    }
                }

                tContext.view.EnableProgressBar(true);

                try
                {
                    tContext.view.WriteLog("Opening connection to " + connectionData.host + ":" + connectionData.port + ".", true);                    

                    RVClient client = new RVClient(tContext.connectionData.host, tContext.connectionData.port);
                    bool result = client.sendStreamDataV1(tContext.connectionData.username, tContext.connectionData.password, tContext.streamData);
                    if (result)
                    {
                        tContext.view.WriteLog("Connection successful. Data Sent.", true);
                    }
                    else
                    {
                        tContext.view.Invoke((System.Windows.Forms.MethodInvoker)delegate()
                        {
                            if (tContext.view.Status != MainWindow.JobStatus.Idle)
                            {
                                tContext.view.Status = MainWindow.JobStatus.Error;
                            }
                        });
                        tContext.view.WriteLog("Error: Data undelivered.", true);
                        tContext.view.WriteLog("Additional error info: Server replied with failure code. Check username and/or password and try again.", true);

                    }
                }
                catch (Exception e)
                {
                    tContext.view.Invoke((System.Windows.Forms.MethodInvoker)delegate()
                    {
                        if (tContext.view.Status != MainWindow.JobStatus.Idle)
                        {
                            tContext.view.Status = MainWindow.JobStatus.Error;
                        }
                    });
                    tContext.view.WriteLog("Error: " + e.Message, true);
                    if (e.InnerException != null)
                    {
                        tContext.view.WriteLog("Additional error info: " + e.InnerException.Message, true);
                    }
                }

                tContext.view.EnableProgressBar(false);

                lock (isConnectingLock)
                {
                    this.isConnecting = false;
                }

            });

            thread.Start(context);
        }

        /// <summary>
        /// Initializes the settings file with default data
        /// </summary>
        /// <param name="stream">Settings file opened stream</param>
        /// <remarks>This method is used as WinAppStock initializer</remarks>
        private void InitializeSettingsFile(FileStream stream)
        {
            SettingsTab.ConnectionSettingsStruct connectionData = new SettingsTab.ConnectionSettingsStruct()
            {
                host = "127.0.0.1",
                port = 27960,
                period = 5,
                username = "username",
                password = "password"
            };
            RVClient.StreamDataV1[] streamData = new RVClient.StreamDataV1[1];

            streamData[0].streamName = "my_local_stream_name";
            streamData[0].port = 554;
            streamData[0].proxiedName = "my_stream_name";

            SerializedSettings settings = new SerializedSettings();
            settings.connectionData = connectionData;
            settings.streamData = streamData;

            XmlSerializer serializer = new XmlSerializer(typeof(SerializedSettings));
            serializer.Serialize(stream, settings);
        }

        /// <summary>
        /// Obtain settings file stream
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <param name="clearFile">Whether to clear the file from its content</param>
        /// <returns>A stream or null if file is unavailable</returns>
        private FileStream GetSettingsFileStream(MainWindow view, bool clearFile)
        {
            FileStream stream = null;

            try
            {
                WinAppStock.BaseStock stock = new WinAppStock.BaseStock("WinRVClient");
                stream = stock.GetChildFile("settings.xml", this.InitializeSettingsFile);
                if (clearFile)
                {
                    stream.SetLength(0);
                }
            }
            catch (Exception e)
            {
                view.WriteLog("Error: Could not open settings file.");
                view.WriteLog("Additional error info: " + e.Message);

                if (stream != null)
                {
                    stream.Close();
                }

                return null;
            }

            return stream;
        }

        /// <summary>
        /// Initialize Main Window state (set controls to default values)
        /// </summary>
        /// <param name="view">Main Window reference</param>
        public void InitializeState(MainWindow view)
        {
            view.Status = MainWindow.JobStatus.Idle;
        }

        /// <summary>
        /// Save passed settings to settings file
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <param name="connectionData"></param>
        /// <param name="streamData"></param>
        public void SaveSettings(MainWindow view, SettingsTab.ConnectionSettingsStruct connectionData, RVClient.StreamDataV1[] streamData)
        {
            FileStream stream = this.GetSettingsFileStream(view, true);

            if (stream != null)
            {
                SerializedSettings settings = new SerializedSettings();
                settings.connectionData = connectionData;
                settings.streamData = streamData;

                XmlSerializer serializer = new XmlSerializer(typeof(SerializedSettings));
                try
                {
                    serializer.Serialize(stream, settings);
                }
                catch
                {
                    view.WriteLog("Error: Could not save new settings");
                    stream.Close();
                    return;
                }

                stream.Close();
                view.WriteLog("New settings saved successfully");
            }
        }

        /// <summary>
        /// Load settings from a file
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <param name="connectionData"></param>
        /// <param name="streamData"></param>
        /// <returns>Whether loading data succeeded and out parameters can be read.</returns>
        /// <remarks>If file is corrupted and loading fails this method tries to reinitialize the file.</remarks>
        public bool LoadSettings(MainWindow view, out SettingsTab.ConnectionSettingsStruct connectionData, out RVClient.StreamDataV1[] streamData)
        {
            FileStream stream = this.GetSettingsFileStream(view, false);
            bool defaultSettings = false;

            if (stream != null)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SerializedSettings));
                SerializedSettings settings;
                try
                {
                    settings = (SerializedSettings)serializer.Deserialize(stream);
                }
                catch
                {
                    view.WriteLog("Error: Could not load previous settings");
                    view.WriteLog("Falling back to default settings");
                    defaultSettings = true;

                    stream.SetLength(0);
                    InitializeSettingsFile(stream);
                    stream.Flush();
                    stream.Seek(0, SeekOrigin.Begin);

                    try
                    {
                        settings = (SerializedSettings)serializer.Deserialize(stream);
                    }
                    catch (Exception e2)
                    {
                        // Something must be really screwed up now...
                        view.WriteLog("Error: Could not reset to default settings");
                        view.WriteLog("Additional error info: " + e2.Message);

                        stream.Close();
                        connectionData = new SettingsTab.ConnectionSettingsStruct();
                        streamData = null;

                        return false;
                    }
                }

                connectionData = settings.connectionData;
                streamData = settings.streamData;

                stream.Close();

                if (!defaultSettings)
                {
                    view.WriteLog("Previous settings loaded successfully");
                }

                return true;
            }
            else
            {
                connectionData = new SettingsTab.ConnectionSettingsStruct();
                streamData = null;

                return false;
            }
        }

        /// <summary>
        /// Handles Connect Now button action
        /// </summary>
        /// <param name="view">Main Window reference</param>
        public void ConnectNow(MainWindow view)
        {
            ConnectOnce(view, view.ConnectionData, view.StreamData);
        }

        /// <summary>
        /// Handles Start button action
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <remarks>Starts a timer to connect to RV server periodically</remarks>
        public void Start(MainWindow view)
        {
            //only one timer per class allowed
            if (this.timer != null)
            {
                view.WriteLog("Error: Job is already in progress.");
                return;
            }

            view.Status = MainWindow.JobStatus.Working;

            this.connectionData = view.ConnectionData;
            this.streamData = view.StreamData;
            ConnectOnce(view, this.connectionData, this.streamData);
            view.WriteLog("Scheduling new connection in " + this.connectionData.period + " minutes.");

            this.timer = new System.Timers.Timer(this.connectionData.period * 1000 * 60);

            this.timer.Elapsed += delegate(Object source, ElapsedEventArgs e)
            {
                ConnectOnce(view, this.connectionData, this.streamData);
                view.WriteLog("Scheduling new connection in " + this.connectionData.period + " minutes.");
            };
            this.timer.SynchronizingObject = view;
            this.timer.AutoReset = true;
            this.timer.Enabled = true;
        }

        /// <summary>
        /// Handles Stop button action
        /// </summary>
        /// <param name="view">Main Window reference</param>
        /// <remarks>Stops the ongoing timers.</remarks>
        public void Stop(MainWindow view)
        {
            if (this.timer != null)
            {
                view.Status = MainWindow.JobStatus.Idle;
                this.timer.Stop();
                this.timer = null;
                view.WriteLog("Connection scheduling aborted.");
            }
            else
            {
                view.WriteLog("Error: There is currently no job in progress.");
            }
        }


    }
}
