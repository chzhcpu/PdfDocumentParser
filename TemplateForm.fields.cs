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
using System.IO;

namespace Cliver.PdfDocumentParser
{
    /// <summary>
    /// template editor GUI
    /// </summary>
    public partial class TemplateForm : Form
    {
        void initializeFieldsTable()
        {
            LeftAnchorId.ValueType = typeof(int);
            LeftAnchorId.ValueMember = "Id";
            LeftAnchorId.DisplayMember = "Name";

            TopAnchorId.ValueType = typeof(int);
            TopAnchorId.ValueMember = "Id";
            TopAnchorId.DisplayMember = "Name";

            RightAnchorId.ValueType = typeof(int);
            RightAnchorId.ValueMember = "Id";
            RightAnchorId.DisplayMember = "Name";

            BottomAnchorId.ValueType = typeof(int);
            BottomAnchorId.ValueMember = "Id";
            BottomAnchorId.DisplayMember = "Name";

            Type.ValueType = typeof(Template.Field.Types);
            Type.DataSource = Enum.GetValues(typeof(Template.Field.Types));

            fields.EnableHeadersVisualStyles = false;//needed to set row headers

            fields.DataError += delegate (object sender, DataGridViewDataErrorEventArgs e)
            {
                DataGridViewRow r = fields.Rows[e.RowIndex];
                Message.Error("fields[" + r.Index + "] has unacceptable value of " + fields.Columns[e.ColumnIndex].HeaderText + ":\r\n" + e.Exception.Message);
            };

            fields.UserDeletingRow += delegate (object sender, DataGridViewRowCancelEventArgs e)
            {
                if (fields.Rows.Count < 3 && fields.SelectedRows.Count > 0)
                    fields.SelectedRows[0].Selected = false;//to avoid auto-creating row
            };

            fields.RowsAdded += delegate (object sender, DataGridViewRowsAddedEventArgs e)
            {
            };

            fields.CellValueChanged += delegate (object sender, DataGridViewCellEventArgs e)
            {
                try
                {
                    if (loadingTemplate)
                        return;
                    if (e.ColumnIndex < 0)//row's header
                        return;
                    DataGridViewRow row = fields.Rows[e.RowIndex];
                    var cs = row.Cells;
                    Template.Field f = (Template.Field)row.Tag;
                    switch (fields.Columns[e.ColumnIndex].Name)
                    {
                        //case "Rectangle":
                        //    {
                        //        cs["Value"].Value = extractValueAndDrawSelectionBox(f.AnchorId, f.Rectangle, f.Type);
                        //        break;
                        //    }
                        case "Type":
                            {
                                Template.Field.Types t2 = (Template.Field.Types)row.Cells["Type"].Value;
                                if (t2 == f.Type)
                                    break;
                                Template.Field f2;
                                switch (t2)
                                {
                                    case Template.Field.Types.PdfText:
                                        f2 = new Template.Field.PdfText();
                                        break;
                                    case Template.Field.Types.OcrText:
                                        f2 = new Template.Field.OcrText();
                                        break;
                                    case Template.Field.Types.ImageData:
                                        f2 = new Template.Field.ImageData();
                                        break;
                                    default:
                                        throw new Exception("Unknown option: " + t2);
                                }
                                f2.Name = f.Name;
                                f2.LeftAnchorId = f.LeftAnchorId;
                                f2.TopAnchorId = f.TopAnchorId;
                                f2.RightAnchorId = f.RightAnchorId;
                                f2.BottomAnchorId = f.BottomAnchorId;
                                f2.Rectangle = f.Rectangle;
                                f = f2;
                                setFieldRow(row, f);
                                break;
                            }
                        case "LeftAnchorId":
                        case "TopAnchorId":
                        case "RightAnchorId":
                        case "BottomAnchorId":
                            {
                                f.LeftAnchorId = (int?)cs["LeftAnchorId"].Value;
                                f.TopAnchorId = (int?)cs["TopAnchorId"].Value;
                                f.RightAnchorId = (int?)cs["RightAnchorId"].Value;
                                f.BottomAnchorId = (int?)cs["BottomAnchorId"].Value;
                                setFieldRow(row, f);
                                break;
                            }
                        case "Name_":
                            f.Name = (string)row.Cells["Name_"].Value;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Message.Error2(ex);
                }
            };

            fields.CurrentCellDirtyStateChanged += delegate
            {
                if (fields.IsCurrentCellDirty)
                    fields.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            fields.RowValidating += delegate (object sender, DataGridViewCellCancelEventArgs e)
            {
                DataGridViewRow r = fields.Rows[e.RowIndex];
                try
                {
                    if (r.Tag != null)
                    {
                        string n = FieldPreparation.Normalize((string)r.Cells["Name_"].Value);
                        if (string.IsNullOrWhiteSpace(n))
                            throw new Exception("Name cannot be empty!");
                        //foreach (DataGridViewRow rr in fields.Rows)
                        //{
                        //    if (r == rr)
                        //        continue;
                        //    Template.Field f = (Template.Field)rr.Tag;
                        //    if (f != null && n == f.Name)
                        //        throw new Exception("Name '" + n + "' is duplicated!");
                        //}
                        r.Cells["Name_"].Value = n;
                    }
                }
                catch (Exception ex)
                {
                    //LogMessage.Error("Name", ex);
                    Message.Error2(ex);
                    e.Cancel = true;
                }
            };

            fields.DefaultValuesNeeded += delegate (object sender, DataGridViewRowEventArgs e)
            {
            };

            fields.CellContentClick += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (e.ColumnIndex < 0)//row's header
                    return;
                switch (fields.Columns[e.ColumnIndex].Name)
                {
                    case "Ocr":
                        fields.EndEdit();
                        break;
                }
            };

            fields.SelectionChanged += delegate (object sender, EventArgs e)
            {
                try
                {
                    if (loadingTemplate)
                        return;

                    if (settingCurrentFieldRow)
                        return;

                    if (fields.SelectedRows.Count < 1)
                        return;
                    DataGridViewRow row = fields.SelectedRows[0];
                    Template.Field f = (Template.Field)row.Tag;
                    if (f == null)//hacky forcing commit a newly added row and display the blank row
                    {
                        int i = fields.Rows.Add();
                        row = fields.Rows[i];
                        f = templateManager.CreateDefaultField();
                        setFieldRow(row, f);
                        row.Selected = true;
                        return;
                    }
                    setCurrentFieldRow(row);
                }
                catch (Exception ex)
                {
                    LogMessage.Error(ex);
                }
            };

            fields.KeyPress += delegate (object sender, KeyPressEventArgs e)
              {
                  switch (e.KeyChar)
                  {
                      case '+':
                          addElseRemoveRow(true);
                          break;
                      case '-':
                          addElseRemoveRow(false);
                          break;
                      case 'c':
                      case 'C':
                          DataGridViewRow r = fields.SelectedRows[fields.SelectedRows.Count - 1];
                          if (r.Tag == null)
                              return;
                          Template.Field f = (Template.Field)r.Tag;
                          object o = pages[currentPageI].GetValue(f.Name);
                          switch (f.Type)
                          {
                              case Template.Field.Types.ImageData:
                                  Clipboard.SetData(f.Type.ToString(), (Image)o);
                                  break;
                              case Template.Field.Types.PdfText:
                              case Template.Field.Types.OcrText:
                                  Clipboard.SetText((string)o);
                                  break;
                              default:
                                  throw new Exception("Unknown option: " + f.Type);
                          }
                          break;
                  }
              };

            fields.KeyDown += delegate (object sender, KeyEventArgs e)
             {
                 if (e.KeyCode == Keys.ControlKey)
                     addElseRemoveRow(true);
                 else if (e.KeyCode == Keys.Delete)
                     addElseRemoveRow(false);
             };
        }

        void addElseRemoveRow(bool add)
        {
            if (add)
            {
                if (fields.SelectedRows.Count < 1)
                    return;
                DataGridViewRow r0 = fields.SelectedRows[fields.SelectedRows.Count - 1];
                if (r0.Tag == null)
                    return;
                int i = fields.Rows.Add();
                DataGridViewRow row = fields.Rows[i];
                //fields.Rows.Remove(row);
                //fields.Rows.Insert(r0.Index, row);
                Template.Field f = (Template.Field)Serialization.Json.Clone(((Template.Field)r0.Tag).GetType(), r0.Tag);
                setFieldRow(row, f);
                row.Selected = true;
            }
            else
            {
                if (fields.SelectedRows.Count < 1)
                    return;
                DataGridViewRow r = fields.SelectedRows[fields.SelectedRows.Count - 1];
                if (r.Tag == null)
                    return;
                bool unique = true;
                foreach (DataGridViewRow rr in fields.Rows)
                    if (rr != r && rr.Tag != null && ((Template.Field)rr.Tag).Name == ((Template.Field)r.Tag).Name)
                    {
                        unique = false;
                        break;
                    }
                if (!unique)
                    fields.Rows.Remove(r);
            }
        }

        void setCurrentFieldRow(DataGridViewRow row)
        {
            if (settingCurrentFieldRow)
                return;
            try
            {
                settingCurrentFieldRow = true;
                //if (row == currentFieldRow)
                //    return;
                currentFieldRow = row;

                if (row == null)
                {
                    fields.ClearSelection();
                    fields.CurrentCell = null;
                    return;
                }

                fields.CurrentCell = fields[0, row.Index];
                Template.Field f = (Template.Field)row.Tag;
                //setCurrentAnchorRow(f.LeftAnchorId, true);
                //setCurrentAnchorRow(f.TopAnchorId, false);
                //setCurrentAnchorRow(f.RightAnchorId, false);
                //setCurrentAnchorRow(f.BottomAnchorId, false);
                setCurrentAnchorRow(null, true);
                setCurrentConditionRow(null);
                setFieldRowValue(row, false);
            }
            finally
            {
                settingCurrentFieldRow = false;
            }
        }
        bool settingCurrentFieldRow = false;
        DataGridViewRow currentFieldRow = null;

        bool setFieldRowValue(DataGridViewRow row, bool setEmpty)
        {
            Template.Field f = (Template.Field)row.Tag;
            if (f == null)
                return false;
            if (!f.IsSet())
            {
                setRowStatus(statuses.WARNING, row, "Not set");
                return false;
            }
            DataGridViewCell c = row.Cells["Value"];
            if (c is DataGridViewImageCell && c.Value != null)
                ((Bitmap)c.Value).Dispose();
            if (setEmpty)
            {
                c.Value = null;
                setRowStatus(statuses.NEUTRAL, row, "");
                return false;
            }
            clearImageFromBoxes();
            object v = extractFieldAndDrawSelectionBox(f);
            if (f.Type == Template.Field.Types.ImageData)
            {
                if (!(c is DataGridViewImageCell))
                {
                    c.Dispose();
                    c = new DataGridViewImageCell();
                    row.Cells["Value"] = c;
                }
            }
            else
            {
                if (c is DataGridViewImageCell)
                {
                    c.Dispose();
                    c = new DataGridViewTextBoxCell();
                    row.Cells["Value"] = c;
                }
            }
            c.Value = v;
            if (c.Value != null)
                setRowStatus(statuses.SUCCESS, row, "Found");
            else
                setRowStatus(statuses.ERROR, row, "Error");
            return v != null;
        }

        void setFieldRow(DataGridViewRow row, Template.Field f)
        {
            row.Tag = f;
            row.Cells["Name_"].Value = f.Name;
            row.Cells["Rectangle"].Value = Serialization.Json.Serialize(f.Rectangle);
            switch (f.Type)
            {
                case Template.Field.Types.PdfText:
                    row.Cells["Type"].Value = Template.Field.Types.PdfText;
                    break;
                case Template.Field.Types.OcrText:
                    row.Cells["Type"].Value = Template.Field.Types.OcrText;
                    break;
                case Template.Field.Types.ImageData:
                    row.Cells["Type"].Value = Template.Field.Types.ImageData;
                    break;
                default:
                    throw new Exception("Unknown option: " + f.Type);
            }
            row.Cells["LeftAnchorId"].Value = f.LeftAnchorId;
            row.Cells["TopAnchorId"].Value = f.TopAnchorId;
            row.Cells["RightAnchorId"].Value = f.RightAnchorId;
            row.Cells["BottomAnchorId"].Value = f.BottomAnchorId;

            if (loadingTemplate)
                return;

            if (row == currentFieldRow)
                setCurrentFieldRow(row);
        }
    }
}