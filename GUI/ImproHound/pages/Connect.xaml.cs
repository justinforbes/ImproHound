﻿using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImproHound.pages
{
    public partial class ConnectPage : Page
    {
        MainWindow containerWindow;

        public ConnectPage(MainWindow containerWindow)
        {
            InitializeComponent();
            this.containerWindow = containerWindow;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            object output;
            Mouse.OverrideCursor = Cursors.Wait;
            url.IsEnabled = false;
            username.IsEnabled = false;
            password.IsEnabled = false;
            connectButton.IsEnabled = false;

            // Make sure we can connect to the DB and the graph is not empty
            DBConnection connection = new DBConnection(url.Text, username.Text, password.Text);

            try
            {
                List<IRecord> response = await connection.Query("MATCH (n) RETURN COUNT(n)");
                if (!response[0].Values.TryGetValue("COUNT(n)", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong.\nNo authentication error but could not fetch number of nodes in graph.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetDefaultControls();
                    return;
                }

                long numOfNodes = (long)output;
                Console.WriteLine("Number of nodes in graph: " + numOfNodes);
                if (numOfNodes.Equals(0))
                {
                    // 0 nodes in graph error
                    MessageBox.Show("You have 0 nodes in your graph.\nMake sure you have upload BloodHound data to graph before connecting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetDefaultControls();
                    return;
                }

            }
            catch
            {
                // Authentication or connection error
                SetDefaultControls();
                return;
            }

            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage(connection));
            SetDefaultControls();
        }

        private void SetDefaultControls()
        {
            Mouse.OverrideCursor = null;
            url.IsEnabled = true;
            username.IsEnabled = true;
            password.IsEnabled = true;
            connectButton.IsEnabled = true;
        }
    }
}
