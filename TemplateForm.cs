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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.IO;

namespace Cliver.PdfDocumentParser
{
    /// <summary>
    /// template editor GUI
    /// </summary>
    public partial class TemplateForm : Form
    {
        public abstract class TemplateManager
        {
            public Template Template;
            abstract public Template New();
            abstract public void ReplaceWith(Template newTemplate);
            abstract public void SaveAsInitialTemplate(Template template);
            public string LastTestFile;
            public void HelpRequest()
            {
                try
                {
                    System.Diagnostics.Process.Start(Settings.Constants.HelpFile);
                }
                catch (Exception ex)
                {
                    LogMessage.Error(ex);
                }
            }
        }

        public TemplateForm(TemplateManager templateManager, string testFileDefaultFolder)
        {
            InitializeComponent();

            Icon = AssemblyRoutines.GetAppIcon();
            Text = AboutBox.AssemblyProduct + ": Template Editor";

            this.templateManager = templateManager;

            setTableHandlers();

            picture.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (pages == null)
                    return;
                drawingSelectionBox = true;
                selectionBoxPoint0 = new Point((int)(e.X / (float)pictureScale.Value), (int)(e.Y / (float)pictureScale.Value));
                selectionBoxPoint1 = new Point(selectionBoxPoint0.X, selectionBoxPoint0.Y);
                selectionBoxPoint2 = new Point(selectionBoxPoint0.X, selectionBoxPoint0.Y);
                selectionCoordinates.Text = selectionBoxPoint1.ToString();
            };

            picture.MouseMove += delegate (object sender, MouseEventArgs e)
            {
                if (pages == null)
                    return;

                Point p = new Point((int)(e.X / (float)pictureScale.Value), (int)(e.Y / (float)pictureScale.Value));

                if (!drawingSelectionBox)
                {
                    selectionCoordinates.Text = p.ToString();
                    return;
                }

                if (selectionBoxPoint0.X < p.X)
                {
                    selectionBoxPoint1.X = selectionBoxPoint0.X;
                    selectionBoxPoint2.X = p.X;
                }
                else
                {
                    selectionBoxPoint1.X = p.X;
                    selectionBoxPoint2.X = selectionBoxPoint0.X;
                }
                if (selectionBoxPoint0.Y < p.Y)
                {
                    selectionBoxPoint1.Y = selectionBoxPoint0.Y;
                    selectionBoxPoint2.Y = p.Y;
                }
                else
                {
                    selectionBoxPoint1.Y = p.Y;
                    selectionBoxPoint2.Y = selectionBoxPoint0.Y;
                }
                selectionCoordinates.Text = selectionBoxPoint1.ToString() + ":" + selectionBoxPoint2.ToString();

                RectangleF r = new RectangleF(selectionBoxPoint1.X, selectionBoxPoint1.Y, selectionBoxPoint2.X - selectionBoxPoint1.X, selectionBoxPoint2.Y - selectionBoxPoint1.Y);
                drawBoxes(Settings.Appearance.SelectionBoxColor, new List<System.Drawing.RectangleF> { r }, true);
            };

            picture.MouseUp += delegate (object sender, MouseEventArgs e)
            {
                try
                {
                    if (pages == null)
                        return;

                    if (!drawingSelectionBox)
                        return;
                    drawingSelectionBox = false;

                    Template.RectangleF r = new Template.RectangleF(selectionBoxPoint1.X, selectionBoxPoint1.Y, selectionBoxPoint2.X - selectionBoxPoint1.X, selectionBoxPoint2.Y - selectionBoxPoint1.Y);

                    switch (mode)
                    {
                        case Modes.SetFloatingAnchor:
                            {
                                if (floatingAnchors.SelectedRows.Count < 1)
                                    break;

                                RectangleF selectedR = new RectangleF(selectionBoxPoint1, new SizeF(selectionBoxPoint2.X - selectionBoxPoint1.X, selectionBoxPoint2.Y - selectionBoxPoint1.Y));
                                Template.ValueTypes vt = (Template.ValueTypes)floatingAnchors.SelectedRows[0].Cells["ValueType3"].Value;
                                switch (vt)
                                {
                                    case Template.ValueTypes.PdfText:
                                        if (selectedPdfCharBoxs == null/* || (ModifierKeys & Keys.Control) != Keys.Control*/)
                                            selectedPdfCharBoxs = new List<Pdf.CharBox>();
                                        selectedPdfCharBoxs.AddRange(Pdf.GetCharBoxsSurroundedByRectangle(pages[currentPage].PdfCharBoxs, selectedR, true));
                                        break;
                                    case Template.ValueTypes.OcrText:
                                        //{
                                        //    if (selectedOcrTextValue == null)
                                        //        selectedOcrTextValue = new Settings.Template.FloatingAnchor.OcrTextValue
                                        //        {
                                        //            TextBoxs = new List<Settings.Template.FloatingAnchor.OcrTextValue.TextBox>()
                                        //        };
                                        //    string error;
                                        //    pages.ActiveTemplate = getTemplateFromUI(false);
                                        //    selectedOcrTextValue.TextBoxs.Add(new Settings.Template.FloatingAnchor.OcrTextValue.TextBox
                                        //    {
                                        //        Rectangle = Settings.Template.RectangleF.GetFromSystemRectangleF(selectedR),
                                        //        Text = (string)pages[currentPage].GetValue(null, Settings.Template.RectangleF.GetFromSystemRectangleF(selectedR), Settings.Template.ValueTypes.OcrText, out error)
                                        //    });
                                        //}
                                        if (selectedOcrCharBoxs == null/* || (ModifierKeys & Keys.Control) != Keys.Control*/)
                                            selectedOcrCharBoxs = new List<Ocr.CharBox>();
                                        selectedOcrCharBoxs.AddRange(PdfDocumentParser.Ocr.GetCharBoxsSurroundedByRectangle(pages[currentPage].ActiveTemplateOcrCharBoxs, selectedR));
                                        break;
                                    case Template.ValueTypes.ImageData:
                                        {
                                            if (selectedImageDataValue == null)
                                            {
                                                selectedImageDataValue = new Template.FloatingAnchor.ImageDataValue();
                                                //if (currentFloatingAnchorControl!=null)
                                                {
                                                    FloatingAnchorImageDataControl c = (FloatingAnchorImageDataControl)currentFloatingAnchorControl;
                                                    selectedImageDataValue.FindBestImageMatch = c.FindBestImageMatch.Checked;
                                                    selectedImageDataValue.BrightnessTolerance = (float)c.BrightnessTolerance.Value;
                                                    selectedImageDataValue.DifferentPixelNumberTolerance = (float)c.DifferentPixelNumberTolerance.Value;
                                                }
                                            }
                                            string error;
                                            selectedImageDataValue.ImageBoxs.Add(new Template.FloatingAnchor.ImageDataValue.ImageBox
                                            {
                                                Rectangle = Template.RectangleF.GetFromSystemRectangleF(selectedR),
                                                ImageData = (ImageData)pages[currentPage].GetValue(null, Template.RectangleF.GetFromSystemRectangleF(selectedR), Template.ValueTypes.ImageData, out error)
                                            });
                                        }
                                        break;
                                    default:
                                        throw new Exception("Unknown option: " + vt);
                                }

                                if ((ModifierKeys & Keys.Control) != Keys.Control)
                                    setFloatingAnchorFromSelectedElements();
                            }
                            break;
                        case Modes.SetDocumentFirstPageRecognitionTextMarks:
                            {
                                if (documentFirstPageRecognitionMarks.SelectedRows.Count < 1)
                                    break;
                             DataGridViewRow row=   documentFirstPageRecognitionMarks.SelectedRows[0];
                                var cs = row.Cells;
                                int? fai = (int?)cs["FloatingAnchorId2"].Value;
                                if (fai != null)
                                {
                                    pages.ActiveTemplate = getTemplateFromUI(false);
                                    PointF? p = pages[currentPage].GetFloatingAnchorPoint0((int)fai);
                                    if (p == null)
                                        throw new Exception("Could not find FloatingAnchor[" + fai + "] in the page");
                                    r.X -= ((PointF)p).X;
                                    r.Y -= ((PointF)p).Y;
                                }
                                setMarkRectangle(row, r);
                            }
                            break;
                        case Modes.SetFieldRectangle:
                            {
                                if (fields.SelectedRows.Count < 1)
                                    break;
                                var cs = fields.SelectedRows[0].Cells;
                                int? fai = (int?)cs["FloatingAnchorId"].Value;
                                if (fai != null)
                                {
                                    pages.ActiveTemplate = getTemplateFromUI(false);
                                    PointF? p = pages[currentPage].GetFloatingAnchorPoint0((int)fai);
                                    if (p == null)
                                        throw new Exception("Could not find FloatingAnchor[" + fai + "] in the page");
                                    r.X -= ((PointF)p).X;
                                    r.Y -= ((PointF)p).Y;
                                }
                                cs["Rectangle"].Value = SerializationRoutines.Json.Serialize(r);
                                //fields.EndEdit();
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Message.Error2(ex);
                }
            };

            Shown += delegate
            {
                Application.DoEvents();//make form be drawn completely
                setUIFromTemplate(templateManager.Template);
            };

            FormClosed += delegate
            {
                if (scaledCurrentPageBitmap != null)
                {
                    scaledCurrentPageBitmap.Dispose();
                    scaledCurrentPageBitmap = null;
                }
                if (pages != null)
                {
                    pages.Dispose();
                    pages = null;
                }

                templateManager.LastTestFile = testFile.Text;
            };

            this.EnumControls((Control c) =>
            {
                SplitContainer s = c as SplitContainer;
                if (s != null)
                {
                    s.Panel1.BackColor = Color.Blue;
                    s.Panel2.BackColor = Color.Blue;
                    s.BackColor = Color.DarkGray;
                    s.SplitterWidth = 2;
                    s.Panel1.BackColor = SystemColors.Control;
                    s.Panel2.BackColor = SystemColors.Control;
                }
            }, true);

            testFile.TextChanged += delegate
            {
                try
                {
                    if (picture.Image != null)
                    {
                        picture.Image.Dispose();
                        picture.Image = null;
                    }
                    if (scaledCurrentPageBitmap != null)
                    {
                        scaledCurrentPageBitmap.Dispose();
                        scaledCurrentPageBitmap = null;
                    }
                    if (pages != null)
                    {
                        pages.Dispose();
                        pages = null;
                    }

                    if (string.IsNullOrWhiteSpace(testFile.Text))
                        return;

                    testFile.SelectionStart = testFile.Text.Length;
                    testFile.ScrollToCaret();

                    if (!File.Exists(testFile.Text))
                    {
                        LogMessage.Error("File '" + testFile.Text + "' does not exist!");
                        return;
                    }

                    pages = new PageCollection(testFile.Text);
                    totalPageNumber = pages.PdfReader.NumberOfPages;
                    lTotalPages.Text = " / " + totalPageNumber;
                    showPage(1);
                }
                catch (Exception ex)
                {
                    LogMessage.Error(ex);
                }
            };

            pictureScale.ValueChanged += delegate
            {
                if (!loadingTemplate)
                    setScaledImage();
            };

            pageRotation.SelectedIndexChanged += delegate
            {
                reloadPageBitmaps();
                //showPage(currentPage);
            };

            autoDeskew.CheckedChanged += delegate
            {
                reloadPageBitmaps();
                //showPage(currentPage);
            };

            Load += delegate
            {
                //if (documentFirstPageRecognitionMarks.Rows.Count > 0 && !documentFirstPageRecognitionMarks.Rows[0].IsNewRow)
                //    documentFirstPageRecognitionMarks.Rows[0].Selected = true;
            };

            save.Click += Save_Click;
            Help.LinkClicked += Help_LinkClicked;
            SaveAsInitialTemplate.LinkClicked += SaveAsInitialTemplate_LinkClicked;
            Configure.LinkClicked += Configure_LinkClicked;
            cancel.Click += delegate { Close(); };

            bTestFile.Click += delegate (object sender, EventArgs e)
         {
             OpenFileDialog d = new OpenFileDialog();
             if (!string.IsNullOrWhiteSpace(testFile.Text))
                 d.InitialDirectory = PathRoutines.GetDirFromPath(testFile.Text);
             else
                if (!string.IsNullOrWhiteSpace(testFileDefaultFolder))
                 d.InitialDirectory = testFileDefaultFolder;

             d.Filter = "PDF|*.pdf|"
                + "All files (*.*)|*.*";
             if (d.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                 return;
             testFile.Text = d.FileName;
         };
        }
        TemplateManager templateManager;

        enum Modes
        {
            NULL,
            SetFloatingAnchor,
            SetDocumentFirstPageRecognitionTextMarks,
            SetFieldRectangle,
        }
        Modes mode
        {
            get
            {
                if (floatingAnchors.SelectedRows.Count > 0)
                    return Modes.SetFloatingAnchor;
                if (documentFirstPageRecognitionMarks.SelectedRows.Count > 0)
                    return Modes.SetDocumentFirstPageRecognitionTextMarks;
                if (fields.SelectedRows.Count > 0)
                    return Modes.SetFieldRectangle;
                //foreach (DataGridViewRow r in fields.Rows)
                //{
                //    if ((bool?)r.Cells["cPageRecognitionTextMarks"].Value == true)
                //        return Modes.SetPageRecognitionTextMarks;
                //}
                return Modes.NULL;
            }
        }
    }
}