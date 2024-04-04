// Copyright (c) 2010-2022 Lockheed Martin Corporation. All rights reserved.
// Use of this file is bound by the PREPAR3D® SOFTWARE DEVELOPER KIT END USER LICENSE AGREEMENT

//
// Managed Data Request sample
//
// Click on Connect to try and connect to a running version of Prepar3D
// Click on Request Data any number of times
// Click on Disconnect to close the connection, and then you should
// be able to click on Connect and restart the process
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// Gaby -- imports to use LiveCharts
using LiveCharts;
//using LiveCharts.Wpf;
using LiveCharts.WinForms;

// Add these two statements to all SimConnect clients
using LockheedMartin.Prepar3D.SimConnect;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Managed_Dashboard;
using LiveCharts.Wpf;

namespace Managed_Dashboard
{
    public partial class Form1 : Form
    {
        // Gaby --
        private LiveCharts.WinForms.CartesianChart altitude_chart;
        private LiveCharts.WinForms.CartesianChart speed_chart;
        // Add a Panel control to the form
        private Panel chartPanel;
        // Add a GroupBox to contain the chart
        private GroupBox chartGroupBox;

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;

        // Ryan-- timer will space out requests by 1 second
        Timer requestTimer = new Timer();
        enum DEFINITIONS
        {
            Struct1,
        }

        enum DATA_REQUESTS
        {
            REQUEST_1,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String title;
            public double latitude;
            public double longitude;
            public double altitude;
            // Ryan-- adding speed property
            public double speed;
            // Ryan-- absolute time
            public double time;
            // Ryan--
            public double magnetic_heading;
        };

        public Form1()
        {

            InitializeComponent();

            chartGroupBox = new GroupBox();
            chartGroupBox.Text = "Chart";
            chartGroupBox.Dock = DockStyle.Bottom; // Dock the GroupBox to the bottom of the form
            chartGroupBox.Height = 300; // Set the height of the GroupBox as needed
            Controls.Add(chartGroupBox);

            chartPanel = new Panel()
            {
                Dock = DockStyle.Fill // Fill the entire form area
            };
            chartGroupBox.Controls.Add(chartPanel);

            // Ryan-- remove middle button parameter
            setButtons(true, false);
            
            // Ryan-- Set timer interval to 1 second
            requestTimer.Interval = 1000;
            requestTimer.Tick += RequestTimer_Tick;

            // Initialize the chart
            InitializeAltitude();
            InitializeSpeed();

        }
        // Simconnect client will send a win32 message when there is 
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.

        protected override void DefWndProc(ref Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    simconnect.ReceiveMessage();
                }
            }
            else
            {
                base.DefWndProc(ref m);
            }
        }

        // Ryan-- remove one parameter (bool bGet). commented out middle button line.
        private void setButtons(bool bConnect, bool bDisconnect)
        {
            buttonConnect.Enabled = bConnect;
            // buttonRequestData.Enabled = bGet;
            buttonDisconnect.Enabled = bDisconnect;
        }

        private void closeConnection()
        {
            if (simconnect != null)
            {
                // Dispose serves the same purpose as SimConnect_Close()
                simconnect.Dispose();
                simconnect = null;
                displayText("Connection closed");
            }
        }

        // Set up all the SimConnect related data definitions and event handlers
        private void initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- changed from altitude to altitude above ground
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Alt Above Ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- on data request, we also define speed
                // Gaby -- fixed to be able to update speed
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED); // Add speed definition
                // Ryan-- get absolute time from epoch in seconds
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Absolute Time", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // Ryan-- get the x and y velocity
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLane Heading Degrees Magnetic", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "STRUCT PBH32", "Simconnect");

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                displayText(ex.Message);
            }
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            displayText("Connected to Prepar3D");

            // Ryan-- Start the timer when connected
            requestTimer.Start();
        }

        // The case where the user closes Prepar3D
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            displayText("Prepar3D has exited");
            closeConnection();
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            displayText("Exception received: " + data.dwException);
        }

        // The case where the user closes the client
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeConnection();
        }

        void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    Struct1 s1 = (Struct1)data.dwData[0];

                    // Ryan--
                    // Convert seconds to ticks (1 tick = 100 nanoseconds)
                    // Create DateTime object from ticks
                    long ticks = (long)(s1.time * TimeSpan.TicksPerSecond);
                    DateTime dateTime = new DateTime(ticks, DateTimeKind.Utc);

                    // Ryan--
                    // Convert radians to degrees
                    double degrees_north = s1.magnetic_heading * (180 / Math.PI);

                    displayText("Title: " + s1.title);
                    displayText("Lat:   " + s1.latitude);
                    displayText("Lon:   " + s1.longitude);
                    displayText("Alt:   " + s1.altitude);
                    // Ryan-- adding ground speed to display
                    displayText("Speed: " + s1.speed);
                    // Ryan-- display time
                    displayText("Time: " + dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    // Ryan--
                    displayText("Magnetic heading: " + degrees_north);

                    // Send info to ChartForm
                    // Gaby
                    UpdateAltitude(s1.altitude);
                    UpdateSpeed(s1.speed);
                    break;

                default:
                    displayText("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        // Method to receive altitude data from the simulation
        // Gaby
        private void UpdateAltitude(double altitude)
        {
            // Get the altitude series from the chart
            var altitudeSeries = altitude_chart.Series[0] as LineSeries;

            // Add the new altitude value to the series
            altitudeSeries.Values.Add(altitude);

            // Limit the number of displayed data points (optional)
            if (altitudeSeries.Values.Count > 50)
            {
                altitudeSeries.Values.RemoveAt(0);
            }

            // Refresh the chart
            altitude_chart.Update();
        }

        private void InitializeAltitude()
        {
            // Create a new Cartesian chart
            altitude_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            altitude_chart.AxisX.Add(new Axis
            {
                Title = "Time", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            altitude_chart.AxisY.Add(new Axis
            {
                Title = "Altitude (feet)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Add the chart to the panel instead of directly to the form
            chartPanel.Controls.Add(altitude_chart);

            // Define a new LineSeries for altitude data
            var altitudeSeries = new LineSeries
            {
                Title = "Altitude",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = null // Hide points on the line
            };

            // Add the series to the chart
            altitude_chart.Series.Add(altitudeSeries);
        }

        private void InitializeSpeed()
        {
            // Create a new Cartesian chart
            speed_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            speed_chart.AxisX.Add(new Axis
            {
                Title = "Time", // X axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            speed_chart.AxisY.Add(new Axis
            {
                Title = "Speed (knots)", // Y axis label
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Add the chart to the panel instead of directly to the form
            chartPanel.Controls.Add(speed_chart);

            // Define a new LineSeries for altitude data
            var speedSeries = new LineSeries
            {
                Title = "Speed",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = null // Hide points on the line
            };

            // Add the series to the chart
            speed_chart.Series.Add(speedSeries);
        }

        private void UpdateSpeed(double speed)
        {
            // Get the altitude series from the chart
            var speedSeries = speed_chart.Series[0] as LineSeries;

            // Add the new altitude value to the series
            speedSeries.Values.Add(speed);

            // Limit the number of displayed data points (optional)
            if (speedSeries.Values.Count > 50)
            {
                speedSeries.Values.RemoveAt(0);
            }

            // Refresh the chart
            speed_chart.Update();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (simconnect == null)
            {
                try
                {
                    // the constructor is similar to SimConnect_Open in the native API4
                    // Ryan-- change name to Managed Dashboard
                    simconnect = new SimConnect("Managed Dashboard", this.Handle, WM_USER_SIMCONNECT, null, 0);
                   
                    // Ryan-- middle parameter removed
                    setButtons(false, true);

                    initDataRequest();
                }
                catch (COMException ex)
                {
                    displayText("Unable to connect to Prepar3D:\n\n" + ex.Message);
                }
            }
            else
            {
                displayText("Error - try again");
                closeConnection();

                // Ryan-- middle parameter removed
                setButtons(true, false);
            }
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            closeConnection();
            // Ryan-- middle parameter removed
            setButtons(true, false);
        }

        /* Ryan-- comment out old button request
        private void buttonRequestData_Click(object sender, EventArgs e)
        {
            // The following call returns identical information to:
            // simconnect.RequestDataOnSimObject(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.ONCE);

            simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
            displayText("Request sent...");
        }
        */

        // Ryan-- new request event handler
        private void RequestTimer_Tick(object sender, EventArgs e)
        {
            // Send data request every second
            if (simconnect != null)
            {
                simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                displayText("Request sent...");
            }
        }

        // Response number
        int response = 1;

        // Output text - display a maximum of 10 lines
        string output = "\n\n\n\n\n\n\n\n\n\n";

        void displayText(string s)
        {
            // remove first string from output
            output = output.Substring(output.IndexOf("\n") + 1);

            // add the new string
            output += "\n" + response++ + ": " + s;

            // display it
            richResponse.Text = output;
        }
    }
}
// End of sample
