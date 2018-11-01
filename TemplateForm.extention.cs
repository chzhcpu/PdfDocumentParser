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
        enum statuses
        {
            SUCCESS,
            NEUTRAL,
            WARNING,
            ERROR,
        }

        void setRowStatus(statuses s, DataGridViewRow r, string m)
        {
            r.HeaderCell.Value = m;
            switch (s)
            {
                case statuses.SUCCESS:
                    r.HeaderCell.Style.BackColor = Color.LightGreen;
                    break;
                case statuses.ERROR:
                    r.HeaderCell.Style.BackColor = Color.Pink;
                    break;
                case statuses.WARNING:
                    r.HeaderCell.Style.BackColor = Color.Yellow;
                    break;
                case statuses.NEUTRAL:
                    r.HeaderCell.Style.BackColor = SystemColors.Control;
                    break;
                default:
                    throw new Exception("Unknown option: " + s);
            }
        }

        void setUIFromTemplate(Template t)
        {
            try
            {
                loadingTemplate = true;

                name.Text = t.Name;

                //imageResolution.Value = template.ImageResolution;

                pageRotation.SelectedIndex = (int)t.PageRotation;
                autoDeskew.Checked = t.AutoDeskew;
                autoDeskewThreshold.Value = t.AutoDeskewThreshold;

                anchors.Rows.Clear();
                if (t.Anchors != null)
                {
                    foreach (Template.Anchor a in t.Anchors)
                    {
                        int i = anchors.Rows.Add();
                        var row = anchors.Rows[i];
                        setAnchorRow(row, a);
                    }
                    onAnchorsChanged();

                    foreach (DataGridViewRow r in anchors.Rows)
                        setAnchorParentAnchorIdList(r);
                }

                fields.Rows.Clear();
                if (t.Fields != null)
                {
                    foreach (Template.Field f in t.Fields)
                    {
                        int i = fields.Rows.Add();
                        var row = fields.Rows[i];
                        setFieldRow(row, f);
                    }
                }

                pictureScale.Value = t.Editor.TestPictureScale > 0 ? t.Editor.TestPictureScale : 1;

                ExtractFieldsAutomaticallyWhenPageChanged.Checked = t.Editor.ExtractFieldsAutomaticallyWhenPageChanged;
                CheckConditionsAutomaticallyWhenPageChanged.Checked = t.Editor.CheckConditionsAutomaticallyWhenPageChanged;

                if (t.Editor.TestFile != null && File.Exists(t.Editor.TestFile))
                    testFile.Text = t.Editor.TestFile;
                else
                {
                    if (templateManager.LastTestFile != null && File.Exists(templateManager.LastTestFile))
                        testFile.Text = templateManager.LastTestFile;
                }
            }
            finally
            {
                loadingTemplate = false;
            }
        }
        bool loadingTemplate = false;

        private void cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void SaveAsInitialTemplate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                templateManager.Template = getTemplateFromUI(true);
                templateManager.SaveAsInitialTemplate();
                Message.Inform("Saved");
            }
            catch (Exception ex)
            {
                Message.Error2(ex);
            }
        }

        private void About_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            AboutBox ab = new AboutBox();
            ab.ShowDialog();
        }

        private void Configure_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            SettingsForm sf = new SettingsForm();
            sf.ShowDialog();
        }

        private void ShowPdfText_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (pages == null)
                return;
            TextForm tf = new TextForm("Pdf Entity Text", PdfTextExtractor.GetTextFromPage(pages.PdfReader, currentPage), false);
            tf.ShowDialog();
        }

        private void ShowOcrText_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (pages == null)
                return;
            //TextForm tf = new TextForm("OCR Text", PdfDocumentParser.Ocr.This.GetHtml(pages[currentPage].Bitmap), true);
            TextForm tf = new TextForm("OCR Text", PdfDocumentParser.Ocr.GetText(pages[currentPage].ActiveTemplateOcrCharBoxs), false);
            tf.ShowDialog();
        }

        private void Help_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            templateManager.HelpRequest();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            try
            {
                //NewTemplate = getTemplateFromUI(true);
                templateManager.Template = getTemplateFromUI(true);
                templateManager.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Message.Error2(ex);
            }
        }

        Template getTemplateFromUI(bool saving)
        {
            Template t = new Template();

            if (saving && string.IsNullOrWhiteSpace(name.Text))
                throw new Exception("Name is empty!");

            t.Name = name.Text.Trim();

            t.PageRotation = (Template.PageRotations)pageRotation.SelectedIndex;
            t.AutoDeskew = autoDeskew.Checked;
            t.AutoDeskewThreshold = (int)autoDeskewThreshold.Value;

            bool? removeNotLinkedAnchors = null;
            t.Anchors = new List<Template.Anchor>();
            foreach (DataGridViewRow r in anchors.Rows)
            {
                Template.Anchor a = (Template.Anchor)r.Tag;
                if (a == null)
                    continue;

                if (saving)
                {
                    if (!a.IsSet())
                        throw new Exception("Anchor[Id=" + a.Id + "] is not set!");

                    bool engaged = false;
                    if (!string.IsNullOrWhiteSpace(a.Condition))
                        engaged = true;
                    if (!engaged)
                        foreach (DataGridViewRow rr in anchors.Rows)
                        {
                            Template.Anchor a_ = (Template.Anchor)rr.Tag;
                            if (a_ == null)
                                continue;
                            if (a_.ParentAnchorId == a.Id)
                            {
                                engaged = true;
                                break;
                            }
                        }
                    if (!engaged)
                        foreach (DataGridViewRow rr in fields.Rows)
                        {
                            Template.Field m = (Template.Field)rr.Tag;
                            if (m != null && m.AnchorId == a.Id)
                            {
                                engaged = true;
                                break;
                            }
                        }
                    if (!engaged)
                    {
                        if (removeNotLinkedAnchors == null)
                            removeNotLinkedAnchors = Message.YesNo("The template contains not linked anchor[s]. Should they be removed?");
                        if (removeNotLinkedAnchors == true)
                            continue;
                    }
                }

                t.Anchors.Add(a);
            }
            t.Anchors = t.Anchors.OrderBy(a => a.Id).ToList();

            //t.Marks = new List<Template.Mark>();
            //foreach (DataGridViewRow r in marks.Rows)
            //{
            //    Template.Mark m = (Template.Mark)r.Tag;
            //    if (m == null)
            //        continue;
            //    if (saving && !m.IsSet())
            //        throw new Exception("Mark[" + r.Index + "] is not set!");
            //    if (m.AnchorId != null && t.Anchors.FirstOrDefault(x => x.Id == m.AnchorId) == null)
            //        throw new Exception("There is no Anchor with Id=" + m.AnchorId);
            //    t.Marks.Add(m);
            //}
            //if (saving && t.Marks.Count < 1)
            //    throw new Exception("Marks is empty!");

            t.Fields = new List<Template.Field>();
            foreach (DataGridViewRow r in fields.Rows)
            {
                Template.Field f = (Template.Field)r.Tag;
                if (f == null)
                    continue;
                if (saving && !f.IsSet())
                    throw new Exception("Field[" + r.Index + "] is not set!");
                if (f.AnchorId != null && t.Anchors.FirstOrDefault(x => x.Id == f.AnchorId) == null)
                    throw new Exception("There is no Anchor with Id=" + f.AnchorId);
                t.Fields.Add(f);
            }
            if (saving && t.Fields.Count < 1)
                throw new Exception("Fields is empty!");

            if (saving)
            {
                t.Editor = new Template.EditorSettings
                {
                    TestFile = testFile.Text,
                    TestPictureScale = pictureScale.Value,
                    ExtractFieldsAutomaticallyWhenPageChanged = ExtractFieldsAutomaticallyWhenPageChanged.Checked,
                    CheckConditionsAutomaticallyWhenPageChanged = CheckConditionsAutomaticallyWhenPageChanged.Checked,
                };
            }

            return t;
        }
    }
}