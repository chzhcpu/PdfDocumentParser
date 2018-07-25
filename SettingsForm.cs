﻿//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cliver.PdfDocumentParser
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();

            this.Icon = AssemblyRoutines.GetAppIcon();
            //Text = Application.ProductName;
            Text = AboutBox.AssemblyTitle;

            load_settings();
        }

        void load_settings()
        {
            FloatingAnchorMasterBoxColor.ForeColor = Settings.Appearance.FloatingAnchorMasterBoxColor;
            FloatingAnchorSecondaryBoxColor.ForeColor = Settings.Appearance.FloatingAnchorSecondaryBoxColor;
            SelectionBoxColor.ForeColor = Settings.Appearance.SelectionBoxColor;

            PdfPageImageResolution.Value = Settings.ImageProcessing.PdfPageImageResolution;
            CoordinateDeviationMargin.Value = (decimal)Settings.ImageProcessing.CoordinateDeviationMargin;
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void bSave_Click(object sender, EventArgs e)
        {
            try
            {
                Settings.Appearance.FloatingAnchorMasterBoxColor = FloatingAnchorMasterBoxColor.ForeColor;
                Settings.Appearance.FloatingAnchorSecondaryBoxColor = FloatingAnchorSecondaryBoxColor.ForeColor;
                Settings.Appearance.SelectionBoxColor = SelectionBoxColor.ForeColor;

                Settings.Appearance.Save();
                Settings.Appearance.Reload();

                Settings.ImageProcessing.PdfPageImageResolution = (int)PdfPageImageResolution.Value;
                Settings.ImageProcessing.CoordinateDeviationMargin = (float)CoordinateDeviationMargin.Value;

                Settings.ImageProcessing.Save();
                Settings.ImageProcessing.Reload();

                Close();
            }
            catch (Exception ex)
            {
                Message.Error2(ex);
            }
        }

        private void bReset_Click(object sender, EventArgs e)
        {
            Settings.Appearance.Reset();
            Settings.ImageProcessing.Reset();
            load_settings();
        }

        private void About_Click(object sender, EventArgs e)
        {
            AboutBox ab = new AboutBox();
            ab.ShowDialog();
        }
        
        private void SelectionBoxColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = Settings.Appearance.SelectionBoxColor;
            if (cd.ShowDialog() == DialogResult.OK)
                SelectionBoxColor.ForeColor = cd.Color;
        }

        private void FloatingAnchorMasterBoxColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = Settings.Appearance.FloatingAnchorMasterBoxColor;
            if (cd.ShowDialog() == DialogResult.OK)
                FloatingAnchorMasterBoxColor.ForeColor = cd.Color;
        }

        private void FloatingAnchorSecondaryBoxColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = Settings.Appearance.FloatingAnchorSecondaryBoxColor;
            if (cd.ShowDialog() == DialogResult.OK)
                FloatingAnchorSecondaryBoxColor.ForeColor = cd.Color;

        }
    }
}