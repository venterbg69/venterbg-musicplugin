﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Configurator.Code;
using MediaBrowser.Library.Plugins;
using MediaBrowser.Library.Threading;
using MediaBrowser.Library;
using System.Windows.Forms;
using System.Threading;
using System.Windows.Threading;
using MediaBrowser.Library.Logging;

namespace Configurator {
    /// <summary>
    /// Interaction logic for AddPluginWindow.xaml
    /// </summary>
    public partial class AddPluginWindow : Window {
        public AddPluginWindow() {
            InitializeComponent();
            progress.Minimum = 0;
            progress.Maximum = 100;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            PluginSourcesWindow window = new PluginSourcesWindow();
            window.ShowDialog();
        }

        private void InstallClick(object sender, RoutedEventArgs e) {
            InstallButton.IsEnabled = false;
            this.progress.Visibility = Visibility.Visible;
            PluginInstaller p = new PluginInstaller();
            callBack done = new callBack(InstallFinished);
            p.InstallPlugin(pluginList.SelectedItem as IPlugin, progress, this, done);

        }

        private delegate void callBack();

        public void InstallFinished()
        {
            //called when the install is finished - we want to close
            this.Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void pluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //validate the required MB version for this plug-in
            if (pluginList.SelectedItem != null && InstallButton != null && MessageLine != null)
            {
                IPlugin plugin = pluginList.SelectedItem as IPlugin;
                if (plugin.RequiredMBVersion > Kernel.Instance.Version)
                {
                    InstallButton.IsEnabled = false;
                    MessageLine.Content = plugin.Name + " requires at least version " + plugin.RequiredMBVersion + ".  Current MB version installed is " + Kernel.Instance.Version;
                }
                else
                {
                    InstallButton.IsEnabled = true;
                    MessageLine.Content = "";
                }
                if (RichDescFrame != null)
                {
                    if (!String.IsNullOrEmpty(plugin.RichDescURL))
                    {
                            RichDescFrame.Source = new Uri(plugin.RichDescURL, UriKind.Absolute);
                            RichDescFrame.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        RichDescFrame.Visibility = Visibility.Hidden;
                    }
                }
                    
            }
        }

        private void RichDescFrame_NavigationFailed(object sender, System.Windows.Navigation.NavigationFailedEventArgs e)
        {
            Logger.ReportError("Navigation to Rich Description failed");
            RichDescFrame.Visibility = Visibility.Hidden;
            e.Handled = true;
        }

    }
}
