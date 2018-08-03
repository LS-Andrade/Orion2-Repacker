﻿/*
 *      This file is part of Orion2, a MapleStory2 Packaging Library Project.
 *      Copyright (C) 2018 Eric Smith <notericsoft@gmail.com>
 * 
 *      This program is free software: you can redistribute it and/or modify
 *      it under the terms of the GNU General Public License as published by
 *      the Free Software Foundation, either version 3 of the License, or
 *      (at your option) any later version.
 * 
 *      This program is distributed in the hope that it will be useful,
 *      but WITHOUT ANY WARRANTY; without even the implied warranty of
 *      MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *      GNU General Public License for more details.
 * 
 *      You should have received a copy of the GNU General Public License
 */

using Orion.Crypto;
using Orion.Crypto.Common;
using Orion.Crypto.Stream;
using Orion.Crypto.Stream.DDS;
using Orion.Window.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Windows.Forms;
using static Orion.Crypto.CryptoMan;

namespace Orion.Window
{
    public partial class MainWindow : Form
    {
        private string sHeaderUOL;
        private PackNodeList pNodeList;
        private MemoryMappedFile pDataMappedMemFile;
        private ProgressWindow pProgress;

        public MainWindow()
        {
            InitializeComponent();

            this.pImagePanel.AutoScroll = true;

            this.pImageData.BorderStyle = BorderStyle.None;
            this.pImageData.Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right);

            this.pMenuStrip.Renderer = new MenuRenderer();

            this.pPrevSize = this.Size;

            this.sHeaderUOL = "";

            this.pNodeList = null;
            this.pDataMappedMemFile = null;
            this.pProgress = null;

            this.UpdatePanel("", null);
        }

        private void AddFileEntry(PackFileEntry pEntry)
        {
            PackNode pRoot = this.pTreeView.Nodes[0] as PackNode;

            if (pRoot != null)
            {
                PackStreamVerBase pStream = pRoot.Tag as PackStreamVerBase;

                if (pStream != null)
                {
                    pStream.GetFileList().Add(pEntry);
                }
            }
        }

        private void InitializeTree(PackStreamVerBase pStream)
        {
            // Insert the root node (file)
            string[] aPath = this.sHeaderUOL.Replace(".m2h", "").Split('/');
            this.pTreeView.Nodes.Add(new PackNode(pStream, aPath[aPath.Length - 1]));

            if (this.pNodeList != null)
            {
                this.pNodeList.InternalRelease();
            }
            this.pNodeList = new PackNodeList("/");

            foreach (PackFileEntry pEntry in pStream.GetFileList())
            {
                if (pEntry.Name.Contains("/"))
                {
                    string sPath = pEntry.Name;
                    PackNodeList pCurList = pNodeList;

                    while (sPath.Contains("/"))
                    {
                        string sDir = sPath.Substring(0, sPath.IndexOf('/') + 1);
                        if (!pCurList.Children.ContainsKey(sDir))
                        {
                            pCurList.Children.Add(sDir, new PackNodeList(sDir));
                            if (pCurList == pNodeList)
                            {
                                this.pTreeView.Nodes[0].Nodes.Add(new PackNode(pCurList.Children[sDir], sDir));
                            }
                        }
                        pCurList = pCurList.Children[sDir];

                        sPath = sPath.Substring(sPath.IndexOf('/') + 1);
                    }

                    pEntry.TreeName = sPath;
                    pCurList.Entries.Add(sPath, pEntry);
                } else
                {
                    pEntry.TreeName = pEntry.Name;

                    this.pNodeList.Entries.Add(pEntry.Name, pEntry);
                    this.pTreeView.Nodes[0].Nodes.Add(new PackNode(pEntry, pEntry.Name));
                }
            }

            // Sort all nodes
            this.pTreeView.Sort();
        }

        private void NotifyMessage(string sText, MessageBoxIcon eIcon = MessageBoxIcon.None)
        {
            MessageBox.Show(this, sText, this.Text, MessageBoxButtons.OK, eIcon);
        }

        private void OnAbout(object sender, EventArgs e)
        {
            About pAbout = new About
            {
                Owner = this
            };

            pAbout.ShowDialog();
        }

        private void OnChangeImage(object sender, EventArgs e)
        {
            if (!this.pChangeImageBtn.Visible)
            {
                return;
            }

            PackNode pNode = this.pTreeView.SelectedNode as PackNode;
            if (pNode != null && pNode.Data != null)
            {
                PackFileEntry pEntry = pNode.Tag as PackFileEntry;

                if (pEntry != null)
                {
                    string sExtension = pEntry.TreeName.Split('.')[1];

                    OpenFileDialog pDialog = new OpenFileDialog
                    {
                        Title = "Select the new image",
                        Filter = string.Format("{0} Image|*.{0}", sExtension.ToUpper()),
                        Multiselect = false
                    };

                    if (pDialog.ShowDialog() == DialogResult.OK)
                    {
                        byte[] pData = File.ReadAllBytes(pDialog.FileName);

                        if (pNode.Data != pData)
                        {
                            pEntry.Data = pData;
                            pEntry.Changed = true;

                            UpdatePanel(sExtension, pData);
                        }
                    }
                }
            }
        }

        private void OnChangeWindowSize(object sender, EventArgs e)
        {
            int nHeight = (this.Size.Height - this.pPrevSize.Height);
            int nWidth = (this.Size.Width - this.pPrevSize.Width);

            this.pImagePanel.Size = new Size
            {
                Height = this.pImagePanel.Height + nHeight,
                Width = this.pImagePanel.Width + nWidth
            };

            this.pTreeView.Size = new Size
            {
                Height = this.pTreeView.Height + nHeight,
                Width = this.pTreeView.Width
            };

            this.pPrevSize = this.Size;
            this.pImageData.Size = this.pImagePanel.Size;

            RenderImageData(true);
        }

        private void OnCollapseNodes(object sender, EventArgs e)
        {
            this.pTreeView.CollapseAll();
        }

        private void OnCopyNode(object sender, EventArgs e)
        {
            PackNode pNode = this.pTreeView.SelectedNode as PackNode;

            if (pNode != null)
            {
                PackFileEntry pEntry = pNode.Tag as PackFileEntry;

                if (pEntry != null)
                {
                    // Clear any current data from clipboard.
                    Clipboard.Clear();
                    // Copy the new copied entry object to clipboard.
                    Clipboard.SetData(PackFileEntry.DATA_FORMAT, pEntry.CreateCopy());
                } else
                {
                    PackNodeList pList = pNode.Tag as PackNodeList;

                    if (pList != null)
                    {
                        // Currently, for memory effieciency's sake, we will only copy
                        // the entries of this directory, and not recursively copy all
                        // sub-directories and their own entries.

                        PackNodeList pListCopy = new PackNodeList(pList.Directory);
                        foreach (PackFileEntry pChild in pList.Entries.Values)
                        {
                            // Decrypt the data because we could potentially be moving
                            // the object across repacker instances.
                            byte[] pBlock = CryptoMan.DecryptData(pChild.FileHeader, this.pDataMappedMemFile);

                            // Add a completely cloned reference of this entry, with a
                            // new decrypted data block assigned and changed marked true.
                            pListCopy.Entries.Add(pChild.TreeName, pChild.CreateCopy(pBlock));
                        }

                        // Finally, copy the new node list to clipboard.
                        Clipboard.Clear();
                        Clipboard.SetData(PackNodeList.DATA_FORMAT, pListCopy);
                    }
                }
            }
        }

        private void OnDoubleClickNode(object sender, TreeNodeMouseClickEventArgs e)
        {
            PackNode pNode = pTreeView.SelectedNode as PackNode;
            if (pNode == null || pNode.Nodes.Count != 0)
                return;
            object pObj = pNode.Tag;

            if (pObj is PackNodeList)
            {
                PackNodeList pList = pObj as PackNodeList;

                // Iterate all further directories within the list
                foreach (KeyValuePair<string, PackNodeList> pChild in pList.Children)
                {
                    pNode.Nodes.Add(new PackNode(pChild.Value, pChild.Key));
                }

                // Iterate entries
                foreach (PackFileEntry pEntry in pList.Entries.Values)
                {
                    pNode.Nodes.Add(new PackNode(pEntry, pEntry.TreeName));
                }

                pNode.Expand();
            }
            /*else if (pObj is PackFileEntry)
            {
                PackFileEntry pEntry = pObj as PackFileEntry;
                PackFileHeaderVerBase pFileHeader = pEntry.FileHeader;

                if (pFileHeader != null)
                {
                    byte[] pBuffer = CryptoMan.DecryptData(pFileHeader, this.pDataMappedMemFile);

                    UpdatePanel(pEntry.TreeName.Split('.')[1].ToLower(), pBuffer);
                }
            }*/
        }

        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OnExpandNodes(object sender, EventArgs e)
        {
            this.pTreeView.ExpandAll();
        }

        private void OnExport(object sender, EventArgs e)
        {
            PackNode pNode = this.pTreeView.SelectedNode as PackNode;

            if (pNode != null)
            {
                byte[] pData = pNode.Data;

                if (pData != null)
                {
                    PackFileEntry pEntry = pNode.Tag as PackFileEntry;
                    if (pEntry != null)
                    {
                        string sName = pEntry.TreeName.Split('.')[0];
                        string sExtension = pEntry.TreeName.Split('.')[1];

                        SaveFileDialog pDialog = new SaveFileDialog
                        {
                            Title = "Select the destination to export the file",
                            FileName = sName,
                            Filter = string.Format("{0} File|*.{1}", sExtension.ToUpper(), sExtension)
                        };

                        if (pDialog.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllBytes(pDialog.FileName, pData);

                            NotifyMessage(string.Format("Successfully exported to {0}", pDialog.FileName), MessageBoxIcon.Information);
                        }
                    }
                }
            } else
            {
                NotifyMessage("Please select a file to export.", MessageBoxIcon.Asterisk);
            }
        }

        private void OnLoadFile(object sender, EventArgs e)
        {
            if (this.pNodeList != null)
            {
                NotifyMessage("Please unload the current file first.", MessageBoxIcon.Information);
                return;
            }

            OpenFileDialog pDialog = new OpenFileDialog()
            {
                Title = "Select the MS2 file to load",
                Filter = "MapleStory2 Files|*.m2d",
                Multiselect = false
            };

            if (pDialog.ShowDialog() == DialogResult.OK)
            {
                string sDataUOL = Dir_BackSlashToSlash(pDialog.FileName);
                this.sHeaderUOL = sDataUOL.Replace(".m2d", ".m2h");

                if (!File.Exists(this.sHeaderUOL))
                {
                    string sHeaderName = this.sHeaderUOL.Substring(this.sHeaderUOL.LastIndexOf('/') + 1);
                    NotifyMessage(string.Format("Unable to load the {0} file.\r\nPlease make sure it exists and is not being used.", sHeaderName), MessageBoxIcon.Error);
                    return;
                }

                PackStreamVerBase pStream;
                using (BinaryReader pHeader = new BinaryReader(File.OpenRead(this.sHeaderUOL)))
                {
                    // Construct a new packed stream from the header data
                    pStream = PackVer.CreatePackVer(pHeader);

                    // Insert a collection containing the file list information [index,hash,name]
                    pStream.GetFileList().Clear();
                    pStream.GetFileList().AddRange(PackFileEntry.CreateFileList(Encoding.UTF8.GetString(CryptoMan.DecryptFileString(pStream, pHeader.BaseStream))));
                    // Make the collection of files sorted by their FileIndex for easy fetching
                    pStream.GetFileList().Sort();
                    
                    // Load the file allocation table and assign each file header to the entry within the list
                    byte[] pFileTable = CryptoMan.DecryptFileTable(pStream, pHeader.BaseStream);
                    using (MemoryStream pTableStream = new MemoryStream(pFileTable))
                    {
                        using (BinaryReader pReader = new BinaryReader(pTableStream))
                        {
                            PackFileHeaderVerBase pFileHeader;

                            switch (pStream.GetVer())
                            {
                                case PackVer.MS2F:
                                    for (ulong i = 0; i < pStream.GetFileListCount(); i++)
                                    {
                                        pFileHeader = new PackFileHeaderVer1(pReader);
                                        pStream.GetFileList()[pFileHeader.GetFileIndex() - 1].FileHeader = pFileHeader;
                                    }
                                    break;
                                case PackVer.NS2F:
                                    for (ulong i = 0; i < pStream.GetFileListCount(); i++)
                                    {
                                        pFileHeader = new PackFileHeaderVer2(pReader);
                                        pStream.GetFileList()[pFileHeader.GetFileIndex() - 1].FileHeader = pFileHeader;
                                    }
                                    break;
                                case PackVer.OS2F:
                                case PackVer.PS2F:
                                    for (ulong i = 0; i < pStream.GetFileListCount(); i++)
                                    {
                                        pFileHeader = new PackFileHeaderVer3(pStream.GetVer(), pReader);
                                        pStream.GetFileList()[pFileHeader.GetFileIndex() - 1].FileHeader = pFileHeader;
                                    }
                                    break;
                            }
                        }
                    }
                }

                this.pDataMappedMemFile = MemoryMappedFile.CreateFromFile(sDataUOL);

                InitializeTree(pStream);
            }
        }

        private void OnPasteNode(object sender, EventArgs e)
        {
            IDataObject pData = Clipboard.GetDataObject();
            if (pData == null)
            {
                return;
            }

            PackNode pNode = this.pTreeView.SelectedNode as PackNode;
            if (pNode != null)
            {
                if (pNode.Tag is PackFileEntry) //wtf are they thinking?
                {
                    NotifyMessage("Please select a directory to paste into!", MessageBoxIcon.Exclamation);
                    return;
                }

                object pObj;
                if (pData.GetDataPresent(PackFileEntry.DATA_FORMAT))
                {
                    pObj = (PackFileEntry)pData.GetData(PackFileEntry.DATA_FORMAT);
                }
                else if (pData.GetDataPresent(PackNodeList.DATA_FORMAT))
                {
                    pObj = (PackNodeList)pData.GetData(PackNodeList.DATA_FORMAT);
                } else
                {
                    NotifyMessage("No files or directories are currently copied to clipboard.", MessageBoxIcon.Exclamation);
                    return;
                }

                PackNodeList pList;
                if (pNode.Level == 0)
                {
                    // If they're trying to add to the root of the file,
                    // then just use the root node list of this tree.
                    pList = this.pNodeList;
                } else
                {
                    pList = pNode.Tag as PackNodeList;
                }

                if (pList != null && pObj != null)
                {
                    if (pObj is PackFileEntry)
                    {
                        PackFileEntry pEntry = pObj as PackFileEntry;

                        AddFileEntry(pEntry);
                        pList.Entries.Add(pEntry.TreeName, pEntry);

                        PackNode pChild = new PackNode(pEntry, pEntry.TreeName);
                        pNode.Nodes.Add(pChild);

                        pEntry.Name = pChild.Path;
                    } else if (pObj is PackNodeList)
                    {
                        PackNodeList pChildList = pObj as PackNodeList;

                        PackNode pChild = new PackNode(pChildList, pChildList.Directory);
                        pList.Children.Add(pChildList.Directory, pChildList);
                        pNode.Nodes.Add(pChild);

                        foreach (PackFileEntry pEntry in pChildList.Entries.Values)
                        {
                            AddFileEntry(pEntry);
                            PackNode pListNode = new PackNode(pEntry, pEntry.TreeName);
                            pChild.Nodes.Add(pListNode);

                            pEntry.Name = pListNode.Path;
                        }
                    }
                }
            }
        }

        private void OnReloadFile(object sender, EventArgs e)
        {
            if (this.pNodeList != null)
            {
                PackStreamVerBase pStream;

                if (this.pTreeView.Nodes.Count > 0)
                {
                    pStream = this.pTreeView.Nodes[0].Tag as PackStreamVerBase;
                    if (pStream == null)
                    {
                        return;
                    }

                    this.pTreeView.Nodes.Clear();
                    this.pTreeView.Refresh();

                    InitializeTree(pStream);
                    UpdatePanel("", null);

                    this.pEntryValue.Text = "Empty";
                }
            } else
            {
                NotifyMessage("There is no package to be reloaded.", MessageBoxIcon.Warning);
            }
        }

        private void OnRemoveFile(object sender, EventArgs e)
        {
            PackNode pNode = this.pTreeView.SelectedNode as PackNode;
            if (pNode != null)
            {
                PackNode pRoot = this.pTreeView.Nodes[0] as PackNode;
                if (pRoot != null && pNode != pRoot)
                {
                    PackStreamVerBase pStream = pRoot.Tag as PackStreamVerBase;
                    if (pStream != null)
                    {
                        PackFileEntry pEntry = pNode.Tag as PackFileEntry;
                        if (pEntry != null)
                        {
                            pStream.GetFileList().Remove(pEntry);

                            if (pNode.Parent == pRoot as TreeNode)
                            {
                                this.pNodeList.Entries.Remove(pEntry.TreeName);
                            } else
                            {
                                (pNode.Parent.Tag as PackNodeList).Entries.Remove(pEntry.TreeName);
                            }
                            pNode.Parent.Nodes.Remove(pNode);
                        } else if (pNode.Tag is PackNodeList)
                        {
                            string sWarning = "WARNING: You are about to delete an entire directory!"
                                    + "\r\nBy deleting this directory, all inner directories and entries will also be removed."
                                    + "\r\n\r\nAre you sure you want to continue?";
                            if (MessageBox.Show(this, sWarning, this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                            {
                                // Recursively remove all inner directories and entries.
                                RemoveDirectory(pNode, pStream);
                                // Remove the entire node list child from the parent list.
                                if (pNode.Parent == pRoot as TreeNode)
                                {
                                    this.pNodeList.Children.Remove(pNode.Name);
                                } else
                                {
                                    (pNode.Parent.Tag as PackNodeList).Children.Remove(pNode.Name);
                                }
                                // Remove the node and all of its children from tree.
                                pNode.Remove();
                            }
                        }
                    }
                }
            }
        }

        private void OnSaveBegin(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker pWorker = sender as BackgroundWorker;

            if (pWorker != null)
            {
                PackStreamVerBase pStream = this.pProgress.Stream;
                if (pStream == null)
                {
                    return;
                }
                // Start the elapsed time progress stopwatch
                this.pProgress.Start();

                // Re-calculate the file list in case of index removal
                pStream.GetFileList().Sort();

                // Save the data blocks to file and re-calculate all entries
                SaveData(this.pProgress.Path, pStream.GetFileList());

                // Declare the new file count (header update)
                uint dwFileCount = (uint)pStream.GetFileList().Count;

                // Construct a raw string containing the new file list information
                StringBuilder sFileString = new StringBuilder();
                foreach (PackFileEntry pEntry in pStream.GetFileList())
                {
                    sFileString.Append(pEntry.ToString());
                }
                this.pSaveWorkerThread.ReportProgress(96);

                // Encrypt the file list and output the new header sizes (header update)
                byte[] pFileString = Encoding.UTF8.GetBytes(sFileString.ToString().ToCharArray());
                byte[] pHeader = CryptoMan.Encrypt(pStream.GetVer(), pFileString, BufferManipulation.AES_ZLIB, out uint uHeaderLen, out uint uCompressedHeaderLen, out uint uEncodedHeaderLen);
                this.pSaveWorkerThread.ReportProgress(97);

                // Construct a new file allocation table
                byte[] pFileTable;
                using (MemoryStream pOutStream = new MemoryStream())
                {
                    using (BinaryWriter pWriter = new BinaryWriter(pOutStream))
                    {
                        foreach (PackFileEntry pEntry in pStream.GetFileList())
                        {
                            pEntry.FileHeader.Encode(pWriter);
                        }
                    }
                    pFileTable = pOutStream.ToArray();
                }
                this.pSaveWorkerThread.ReportProgress(98);

                // Encrypt the file table and output the new data sizes (header update)
                pFileTable = CryptoMan.Encrypt(pStream.GetVer(), pFileTable, BufferManipulation.AES_ZLIB, out uint uDataLen, out uint uCompressedDataLen, out uint uEncodedDataLen);
                this.pSaveWorkerThread.ReportProgress(99);

                // Update all header sizes to the new file list information
                pStream.SetFileListCount(dwFileCount);
                pStream.SetHeaderSize(uHeaderLen);
                pStream.SetCompressedHeaderSize(uCompressedHeaderLen);
                pStream.SetEncodedHeaderSize(uEncodedHeaderLen);
                pStream.SetDataSize(uDataLen);
                pStream.SetCompressedDataSize(uCompressedDataLen);
                pStream.SetEncodedDataSize(uEncodedDataLen);

                // Write the new header data to stream
                using (BinaryWriter pWriter = new BinaryWriter(File.Create(this.pProgress.Path.Replace(".m2d", ".m2h"))))
                {
                    // Encode the file version (MS2F,NS2F,etc)
                    pWriter.Write(pStream.GetVer());

                    // Encode the stream header information
                    pStream.Encode(pWriter);

                    // Encode the encrypted header and file table buffers
                    pWriter.Write(pHeader);
                    pWriter.Write(pFileTable);
                }
                this.pSaveWorkerThread.ReportProgress(100);
            }
        }

        private void OnSaveChanges(object sender, EventArgs e)
        {
            if (!this.pUpdateDataBtn.Visible)
            {
                return;
            }

            PackNode pNode = this.pTreeView.SelectedNode as PackNode;
            if (pNode != null && pNode.Data != null)
            {
                PackFileEntry pEntry = pNode.Tag as PackFileEntry;
                
                if (pEntry != null)
                {
                    string sData = this.pTextData.Text;
                    byte[] pData = Encoding.UTF8.GetBytes(sData.ToCharArray());

                    if (pNode.Data != pData)
                    {
                        pEntry.Data = pData;
                        pEntry.Changed = true;
                    }
                }
            }
        }

        private void OnSaveComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show(this.pProgress, e.Error.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            this.pProgress.Finish();
            this.pProgress.Close();
            
            TimeSpan pInterval = TimeSpan.FromMilliseconds(this.pProgress.ElapsedTime);
            NotifyMessage(string.Format("Successfully saved in {0} minutes and {1} seconds!", pInterval.Minutes, pInterval.Seconds), MessageBoxIcon.Information);

            // Perform heavy cleanup
            System.GC.Collect();
        }

        private void OnSaveFile(object sender, EventArgs e)
        {
            PackNode pNode = this.pTreeView.SelectedNode as PackNode;

            if (pNode != null && pNode.Tag is PackStreamVerBase)
            {
                SaveFileDialog pDialog = new SaveFileDialog
                {
                    Title = "Select the destination to save the file",
                    Filter = "MapleStory2 Files|*.m2d"
                };

                if (pDialog.ShowDialog() == DialogResult.OK)
                {
                    string sPath = Dir_BackSlashToSlash(pDialog.FileName);

                    if (!pSaveWorkerThread.IsBusy)
                    {
                        this.pProgress = new ProgressWindow
                        {
                            Path = sPath,
                            Stream = (pNode.Tag as PackStreamVerBase)
                        };
                        this.pProgress.Show(this);
                        // Why do you make this so complicated C#? 
                        int x = this.DesktopBounds.Left + (this.Width - this.pProgress.Width) / 2;
                        int y = this.DesktopBounds.Top + (this.Height - this.pProgress.Height) / 2;
                        this.pProgress.SetDesktopLocation(x, y);

                        this.pSaveWorkerThread.RunWorkerAsync();
                    }
                }
            } else
            {
                NotifyMessage("Please select a Packed Data File file to save.", MessageBoxIcon.Information);
            }
        }

        private void OnSaveProgress(object sender, ProgressChangedEventArgs e)
        {
            this.pProgress.UpdateProgressBar(e.ProgressPercentage);
        }

        private void OnSelectNode(object sender, TreeViewEventArgs e)
        {
            PackNode pNode = this.pTreeView.SelectedNode as PackNode;

            if (pNode != null)
            {
                object pObj = pNode.Tag;

                if (pObj is PackNodeList)
                {
                    this.pEntryValue.Text = "Packed Directory";
                } else if (pObj is PackFileEntry)
                {
                    PackFileEntry pEntry = pObj as PackFileEntry;
                    PackFileHeaderVerBase pFileHeader = pEntry.FileHeader;

                    if (pFileHeader != null)
                    {
                        if (pNode.Data == null)
                        {
                            // TODO: Improve memory efficiency here and dispose of the data if
                            // it's unchanged once they select a different node in the tree.
                            pNode.Data = CryptoMan.DecryptData(pFileHeader, this.pDataMappedMemFile);
                        }
                    }

                    UpdatePanel(pEntry.TreeName.Split('.')[1].ToLower(), pNode.Data);
                }
                else if (pObj is PackStreamVerBase)
                {
                    this.pEntryValue.Text = "Packed Data File";
                } else
                {
                    this.pEntryValue.Text = "Empty";
                }
            }
        }

        private void OnUnloadFile(object sender, EventArgs e)
        {
            if (this.pNodeList != null)
            {
                pTreeView.Nodes.Clear();

                this.pNodeList.InternalRelease();
                this.pNodeList = null;

                this.sHeaderUOL = "";

                if (this.pDataMappedMemFile != null)
                {
                    this.pDataMappedMemFile.Dispose();
                    this.pDataMappedMemFile = null;
                }

                this.UpdatePanel("", null);

                System.GC.Collect();
            } else
            {
                NotifyMessage("There is no package to be unloaded.", MessageBoxIcon.Warning);
            }
        }

        private void OnWindowClosing(object sender, FormClosingEventArgs e)
        {
            // Only ask for confirmation when the user has files open.
            if (this.pTreeView.Nodes.Count > 0)
            {
                if (MessageBox.Show(this, "Are you sure you want to exit?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void RemoveDirectory(PackNode pNode, PackStreamVerBase pStream)
        {
            if (pNode.Nodes.Count == 0)
            {
                if (pNode.Tag is PackNodeList)
                {
                    PackNodeList pList = pNode.Tag as PackNodeList;

                    foreach (KeyValuePair<string, PackNodeList> pChild in pList.Children)
                    {
                        pNode.Nodes.Add(new PackNode(pChild.Value, pChild.Key));
                    }

                    foreach (PackFileEntry pEntry in pList.Entries.Values)
                    {
                        pNode.Nodes.Add(new PackNode(pEntry, pEntry.TreeName));
                    }

                    pList.Children.Clear();
                    pList.Entries.Clear();
                }
            }

            foreach (PackNode pChild in pNode.Nodes)
            {
                RemoveDirectory(pChild, pStream);
            }

            if (pNode.Tag is PackFileEntry)
            {
                pStream.GetFileList().Remove(pNode.Tag as PackFileEntry);
            }
        }

        private void RenderImageData(bool bChange)
        {
            this.pImageData.Visible = this.pImagePanel.Visible;

            if (this.pImageData.Visible)
            {
                // If the size of the bitmap image is bigger than the actual panel,
                // then we adjust the image sizing mode to zoom the image in order
                // to fit the full image within the current size of the panel.
                if (this.pImageData.Image.Size.Height > this.pImagePanel.Size.Height || this.pImageData.Image.Size.Width > this.pImagePanel.Size.Width)
                {
                    // If we went from selecting a small image to selecting a big image,
                    // then adjust the panel and data to fit the size of the new bitmap.
                    if (!bChange)
                    {
                        this.OnChangeWindowSize(null, null);
                    }

                    // Since the image is too big, scale it in zoom mode to fit it.
                    this.pImageData.SizeMode = PictureBoxSizeMode.Zoom;
                } else
                {
                    // Since the image is less than or equal to the size of the panel,
                    // we are able to render the image as-is with no additional scaling.
                    this.pImageData.SizeMode = PictureBoxSizeMode.Normal;
                }

                // Render the new size changes.
                this.pImageData.Update();
            }
        }

        private void SaveData(string sDataPath, List<PackFileEntry> aEntry)
        {
            List<PackFileEntry> aNewEntry = new List<PackFileEntry>();

            // Declare MS2F as the initial version until specified.
            uint uVer = PackVer.MS2F;
            // Re-calculate all file offsets from start to finish
            ulong uOffset = 0;
            // Re-calculate all file indexes from start to finish
            int nCurIndex = 1;

            using (BinaryWriter pWriter = new BinaryWriter(File.Create(sDataPath)))
            {
                // Iterate all file entries that exist
                foreach (PackFileEntry pEntry in aEntry)
                {
                    PackFileHeaderVerBase pHeader = pEntry.FileHeader;

                    // If the entry was modified, or is new, write the modified data block
                    if (pEntry.Changed)
                    {
                        // If the header is null (new entry), then create one
                        if (pHeader == null)
                        {
                            // Hacky way of doing this, but this follows Nexon's current conventions.
                            uint dwBufferFlag;
                            if (pEntry.Name.EndsWith(".usm"))
                            {
                                dwBufferFlag = BufferManipulation.XOR;
                            } else if (pEntry.Name.EndsWith(".png"))
                            {
                                dwBufferFlag = BufferManipulation.AES;
                            } else
                            {
                                dwBufferFlag = BufferManipulation.AES_ZLIB;
                            }

                            switch (uVer)
                            {
                                case PackVer.MS2F:
                                    pHeader = PackFileHeaderVer1.CreateHeader(nCurIndex, dwBufferFlag, uOffset, pEntry.Data);
                                    break;
                                case PackVer.NS2F:
                                    pHeader = PackFileHeaderVer2.CreateHeader(nCurIndex, dwBufferFlag, uOffset, pEntry.Data);
                                    break;
                                case PackVer.OS2F:
                                case PackVer.PS2F:
                                    pHeader = PackFileHeaderVer3.CreateHeader(uVer, nCurIndex, dwBufferFlag, uOffset, pEntry.Data);
                                    break;
                            }
                            // Update the entry's file header to the newly created one
                            pEntry.FileHeader = pHeader;
                        }
                        else
                        {
                            // If the header existed already, re-calculate the file index and offset.
                            pHeader.SetFileIndex(nCurIndex);
                            pHeader.SetOffset(uOffset);
                        }

                        // Encrypt the new data block and output the header size data
                        pWriter.Write(CryptoMan.Encrypt(uVer, pEntry.Data, pEntry.FileHeader.GetBufferFlag(), out uint uLen, out uint uCompressed, out uint uEncoded));

                        // Apply the file size changes from the new buffer
                        pHeader.SetFileSize(uLen);
                        pHeader.SetCompressedFileSize(uCompressed);
                        pHeader.SetEncodedFileSize(uEncoded);

                        // Update the Entry's index to the new current index
                        pEntry.Index = nCurIndex;

                        nCurIndex++;
                        uOffset += pHeader.GetEncodedFileSize();
                    }
                    // If the entry is unchanged, parse the block from the original offsets
                    else
                    {
                        // Make sure the entry has a parsed file header from load
                        if (pHeader != null)
                        {
                            // Update the initial versioning before any future crypto calls
                            if (pHeader.GetVer() != uVer)
                            {
                                uVer = pHeader.GetVer();
                            }

                            // Access the current encrypted block data from the memory map initially loaded
                            using (MemoryMappedViewStream pBuffer = this.pDataMappedMemFile.CreateViewStream((long)pHeader.GetOffset(), (long)pHeader.GetEncodedFileSize()))
                            {
                                byte[] pSrc = new byte[pHeader.GetEncodedFileSize()];

                                if (pBuffer.Read(pSrc, 0, (int)pHeader.GetEncodedFileSize()) == pHeader.GetEncodedFileSize())
                                {
                                    // Modify the header's file index to the updated offset after entry changes
                                    pHeader.SetFileIndex(nCurIndex);
                                    // Modify the header's offset to the updated offset after entry changes
                                    pHeader.SetOffset(uOffset);
                                    // Write the original (completely encrypted) block of data to file
                                    pWriter.Write(pSrc);

                                    // Update the Entry's index to the new current index
                                    pEntry.Index = nCurIndex;

                                    nCurIndex++;
                                    uOffset += pHeader.GetEncodedFileSize();
                                }
                            }
                        }
                    }
                    // Allow the remaining 5% for header file write progression
                    this.pSaveWorkerThread.ReportProgress((int)(((double)(nCurIndex - 1) / (double)aEntry.Count) * 95.0d));
                }
            }
        }

        private void UpdatePanel(string sExtension, byte[] pBuffer)
        {
            if (!string.IsNullOrEmpty(sExtension))
                this.pEntryValue.Text = string.Format("{0} File", sExtension.ToUpper());
            else
                this.pEntryValue.Text = "Empty";

            this.pTextData.Visible = (sExtension.Equals("ini") || sExtension.Equals("nt") || sExtension.Equals("lua")
                || sExtension.Equals("xml") || sExtension.Equals("flat") || sExtension.Equals("xblock") 
                || sExtension.Equals("diagram") || sExtension.Equals("preset") || sExtension.Equals("emtproj"));
            this.pUpdateDataBtn.Visible = this.pTextData.Visible;

            this.pImagePanel.Visible = (sExtension.Equals("png") || sExtension.Equals("dds"));
            this.pChangeImageBtn.Visible = this.pImagePanel.Visible;

            if (sExtension.Equals("ini") || sExtension.Equals("nt") || sExtension.Equals("lua"))
            {
                this.pTextData.Text = Encoding.UTF8.GetString(pBuffer);
            } else if (sExtension.Equals("xml") || sExtension.Equals("flat") || sExtension.Equals("xblock") 
                || sExtension.Equals("diagram") || sExtension.Equals("preset") || sExtension.Equals("emtproj"))
            {
                string sOutput = Encoding.UTF8.GetString(pBuffer);

                try
                {
                    this.pTextData.Text = System.Xml.Linq.XDocument.Parse(sOutput).ToString();
                } catch (Exception)
                {
                    this.pTextData.Text = sOutput;
                }
            } else if (sExtension.Equals("png")) {
                Bitmap pImage;
                using (MemoryStream pStream = new MemoryStream(pBuffer))
                {
                    pImage = new Bitmap(pStream);
                }

                this.pImageData.Image = pImage;
            } else if (sExtension.Equals("dds")) {
                this.pImageData.Image = DDS.LoadImage(pBuffer);
            } else
            {
                this.pTextData.Visible = false;

                /*
                 * TODO:
                 * *.nif, *.kf, and *.kfm files
                 * Shaders/*.fxo - directx shader files?
                 * PrecomputedTerrain/*.tok - mesh3d files? token files?
                 * Gfx/*.gfx - graphics gen files?
                 * Precompiled/luapack.o - object files?
                */
            }

            RenderImageData(false);
        }

        private static string Dir_BackSlashToSlash(string sDir)
        {
            while (sDir.Contains("\\"))
            {
                sDir = sDir.Replace("\\", "/");
            }
            return sDir;
        }
    }
}
