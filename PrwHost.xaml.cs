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

namespace CRS
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PrwHost : Window
    {
        public PrwHost()
        {
            InitializeComponent();
        }


        private void PrwHost_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
           MainWindow.Prw = null;
        }
    }
}
