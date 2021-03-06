﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DatabaseMigration.Core;
using DatabaseMigration.Profile;

namespace DatabaseMigration
{
    public partial class frmMain : Form, IObserver<FeedbackInfo>
    {
        private const string DONE = "Done";
        private ConnectionInfo sourceDbConnectionInfo;
        private ConnectionInfo targetDbConnectionInfo;
        private StringBuilder sbFeedback = new StringBuilder();

        public frmMain()
        {
            InitializeComponent();
            ComboBox.CheckForIllegalCrossThreadCalls = false;
            CheckBox.CheckForIllegalCrossThreadCalls = false;
            TextBox.CheckForIllegalCrossThreadCalls = false;
            TreeView.CheckForIllegalCrossThreadCalls = false;

            this.sourceScriptBackgroundWorker.WorkerSupportsCancellation = true;
            this.convertorBackgroundWorker.WorkerSupportsCancellation = true;
            this.sourceScriptBackgroundWorker.DoWork += SourceScriptBackgroundWorker_DoWork;
            this.convertorBackgroundWorker.DoWork += async (sender, e) =>
            {
                await ConvertorBackgroundWorker_DoWork(sender, e);
            };
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            this.LoadDbTypes();
        }

        private void LoadDbTypes()
        {
            var values = Enum.GetValues(typeof(DatabaseType));
            foreach (var value in values)
            {
                this.cboSourceDB.Items.Add(value.ToString());
                this.cboTargetDB.Items.Add(value.ToString());
            }
        }

        private void btnAddSource_Click(object sender, EventArgs e)
        {
            this.AddConnection(true, this.cboSourceDB.Text);
        }

        private void btnAddTarget_Click(object sender, EventArgs e)
        {
            this.AddConnection(false, this.cboTargetDB.Text);
        }

        private void AddConnection(bool isSource, string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                MessageBox.Show("Please select database type.");
                return;
            }

            DatabaseType dbType = this.GetDatabaseType(type);
            frmDbConnect frmDbConnect = new frmDbConnect(dbType);
            if (this.SetConnectionInfo(isSource, frmDbConnect))
            {
                this.LoadProfileNames(isSource, frmDbConnect.ProflieName);
            }
        }

        private void ConfigConnection(bool isSource, string type, object selectedItem, bool requriePassword = false)
        {
            string profileName = selectedItem == null ? string.Empty : (selectedItem as ConnectionInfoProfile)?.Name;
            if (string.IsNullOrEmpty(type))
            {
                MessageBox.Show("Please select database type.");
                return;
            }

            if (string.IsNullOrEmpty(profileName))
            {
                MessageBox.Show("Please select a profile.");
                return;
            }

            DatabaseType dbType = this.GetDatabaseType(type);
            frmDbConnect frmDbConnect = new frmDbConnect(dbType, profileName, requriePassword);
            this.SetConnectionInfo(isSource, frmDbConnect);
        }

        private bool SetConnectionInfo(bool isSource, frmDbConnect frmDbConnect)
        {
            DialogResult dialogResult = frmDbConnect.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                ConnectionInfo connectionInfo = frmDbConnect.ConnectionInfo;
                if (isSource)
                {
                    this.sourceDbConnectionInfo = connectionInfo;
                }
                else
                {
                    this.targetDbConnectionInfo = connectionInfo;
                }
                return true;
            }
            return false;
        }

        private DatabaseType GetDatabaseType(string dbType)
        {
            return (DatabaseType)Enum.Parse(typeof(DatabaseType), dbType);
        }

        private void btnConfigSource_Click(object sender, EventArgs e)
        {
            this.ConfigConnection(true, this.cboSourceDB.Text, this.cboSourceProfile.SelectedItem);
        }

        private void btnConfigTarget_Click(object sender, EventArgs e)
        {
            this.ConfigConnection(false, this.cboTargetDB.Text, this.cboTargetProfile.SelectedItem);
        }

        private void cboSourceDB_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.LoadProfileNames(true);
        }

        private void cboTargetDB_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.LoadProfileNames(false);
        }

        private void LoadProfileNames(bool isSource, string defaultValue = null)
        {
            ComboBox dbTypeControl = isSource ? this.cboSourceDB : this.cboTargetDB;
            ComboBox profileControl = isSource ? this.cboSourceProfile : this.cboTargetProfile;
            string type = dbTypeControl.Text;

            if (type != "")
            {
                DatabaseType dbType = this.GetDatabaseType(type);
                List<ConnectionInfoProfile> profiles = ConnectionInfoProfileManager.GetProfiles(dbType);

                List<string> names = profiles.Select(item => item.Name).ToList();

                profileControl.DataSource = profiles;
                profileControl.DisplayMember = nameof(ConnectionInfoProfile.Description);
                profileControl.ValueMember = nameof(ConnectionInfoProfile.Name);

                if (string.IsNullOrEmpty(defaultValue))
                {
                    if (profiles.Count > 0)
                    {
                        profileControl.SelectedIndex = 0;
                    }
                }
                else
                {
                    if (names.Contains(defaultValue))
                    {
                        profileControl.Text = defaultValue;
                    }
                }

                bool selected = profileControl.Text.Length > 0;
                if (isSource)
                {
                    this.btnConfigSource.Visible = this.btnRemoveSource.Visible = selected;
                }
                else
                {
                    this.btnConfigTarget.Visible = this.btnRemoveTarget.Visible = selected;
                }
            }
        }

        private void LoadSourceDbSchemaInfo()
        {
            this.tvSource.Nodes.Clear();

            DatabaseType dbType = this.GetDatabaseType(this.cboSourceDB.Text);
            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, this.sourceDbConnectionInfo, new GenerateScriptOption());

            if (dbInterpreter is SqlServerInterpreter)
            {
                TreeNode userDefinedRootNode = new TreeNode("User Defined Types");
                userDefinedRootNode.Name = nameof(UserDefinedType);
                this.tvSource.Nodes.Add(userDefinedRootNode);

                List<UserDefinedType> userDefinedTypes = dbInterpreter.GetUserDefinedTypes();
                foreach (UserDefinedType userDefinedType in userDefinedTypes)
                {
                    TreeNode node = new TreeNode();
                    node.Tag = userDefinedType;
                    node.Text = $"{userDefinedType.Owner}.{userDefinedType.Name}";
                    userDefinedRootNode.Nodes.Add(node);
                }
            }

            TreeNode tableRootNode = new TreeNode("Tables");
            tableRootNode.Name = nameof(Table);
            this.tvSource.Nodes.Add(tableRootNode);

            List<Table> tables = dbInterpreter.GetTables();
            foreach (Table table in tables)
            {
                TreeNode tableNode = new TreeNode();
                tableNode.Tag = table;
                tableNode.Text = dbInterpreter.GetDisplayTableName(table, false);
                tableRootNode.Nodes.Add(tableNode);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.cboSourceDB.Text))
            {
                MessageBox.Show("Please select a source database type.");
                return;
            }

            if (string.IsNullOrEmpty(this.cboSourceProfile.Text))
            {
                MessageBox.Show("Please select a source database profile.");
                return;
            }

            if (!this.sourceDbConnectionInfo.IntegratedSecurity && string.IsNullOrEmpty(this.sourceDbConnectionInfo.Password))
            {
                MessageBox.Show("Please specify password of the source database.");
                this.ConfigConnection(true, this.cboSourceDB.Text, this.cboSourceProfile.Text, true);
                return;
            }

            this.Invoke(new Action(() =>
            {
                this.btnConnect.Text = "...";

                try
                {
                    this.LoadSourceDbSchemaInfo();
                    this.btnSourceScript.Enabled = true;
                    this.btnExecute.Enabled = true;
                }
                catch (Exception ex)
                {
                    this.tvSource.Nodes.Clear();
                    MessageBox.Show("Error:" + ex.Message);
                }

                this.btnConnect.Text = "Connect";
            }));
        }

        private void cboSourceProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.GetConnectionInfoByProfile(true);
        }

        private void cboTargetProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.GetConnectionInfoByProfile(false);
        }

        private void GetConnectionInfoByProfile(bool isSource)
        {
            DatabaseType dbType = this.GetDatabaseType(isSource ? this.cboSourceDB.Text : this.cboTargetDB.Text);
            string profileName = ((isSource ? this.cboSourceProfile : this.cboTargetProfile).SelectedItem as ConnectionInfoProfile)?.Name;
            ConnectionInfo connectionInfo = ConnectionInfoProfileManager.GetConnectionInfo(dbType, profileName);

            if (connectionInfo != null)
            {
                if (isSource && (this.sourceDbConnectionInfo == null || (this.sourceDbConnectionInfo.Database != connectionInfo.Database)))
                {
                    this.sourceDbConnectionInfo = connectionInfo;
                }
                else if (!isSource && (this.targetDbConnectionInfo == null || (this.targetDbConnectionInfo.Database != connectionInfo.Database)))
                {
                    this.targetDbConnectionInfo = connectionInfo;
                }
            }

            if (!isSource)
            {
                if (dbType == DatabaseType.SqlServer)
                {
                    if (string.IsNullOrEmpty(this.txtTargetDbOwner.Text.Trim()))
                    {
                        this.txtTargetDbOwner.Text = "dbo";
                    }
                }
                else
                {
                    this.txtTargetDbOwner.Text = "";
                }
            }
        }

        private void tvSource_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Nodes.Count > 0)
            {
                foreach (TreeNode node in e.Node.Nodes)
                {
                    node.Checked = e.Node.Checked;
                }
            }
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            if (!this.convertorBackgroundWorker.IsBusy)
            {
                this.txtMessage.ForeColor = Color.Black;
                this.txtMessage.Text = "";

                this.convertorBackgroundWorker.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show("The worker is busy now.");
            }
        }

        private SchemaInfo GetSourceTreeSchemaInfo()
        {
            SchemaInfo schemaInfo = new SchemaInfo();
            foreach (TreeNode node in this.tvSource.Nodes)
            {
                foreach (TreeNode item in node.Nodes)
                {
                    if (item.Checked)
                    {
                        switch (node.Name)
                        {
                            case nameof(UserDefinedType):
                                schemaInfo.UserDefinedTypes.Add(item.Tag as UserDefinedType);
                                break;
                            case nameof(Table):
                                schemaInfo.Tables.Add(item.Tag as Table);
                                break;
                        }
                    }
                }
            }
            return schemaInfo;
        }

        private bool ValidateSource(SchemaInfo schemaInfo)
        {
            if (schemaInfo.UserDefinedTypes.Count == 0 && schemaInfo.Tables.Count == 0)
            {
                MessageBox.Show("Please select objects from tree.");
                return false;
            }

            if (this.sourceDbConnectionInfo == null)
            {
                MessageBox.Show("Source connection is null.");
                return false;
            }

            return true;
        }

        private bool SetGenerateScriptOption(params GenerateScriptOption[] options)
        {
            if (options != null)
            {
                string outputFolder = this.txtOutputFolder.Text.Trim();
                foreach (GenerateScriptOption option in options)
                {
                    if (Directory.Exists(outputFolder))
                    {
                        option.ScriptOutputFolder = outputFolder;
                    }

                    if (this.chkGenerateSourceScripts.Checked)
                    {
                        option.ScriptOutputMode = option.ScriptOutputMode | GenerateScriptOutputMode.WriteToFile;
                    }
                    if (this.chkOutputScripts.Checked)
                    {
                        option.ScriptOutputMode = option.ScriptOutputMode | GenerateScriptOutputMode.WriteToFile;
                    }
                }
            }

            return true;
        }

        private GenerateScriptMode GetGenerateScriptMode()
        {
            GenerateScriptMode scriptMode = GenerateScriptMode.None;
            if (this.chkScriptSchema.Checked)
            {
                scriptMode = scriptMode | GenerateScriptMode.Schema;
            }
            if (this.chkScriptData.Checked)
            {
                scriptMode = scriptMode | GenerateScriptMode.Data;
            }

            return scriptMode;
        }

        private async Task ConvertorBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (this.convertorBackgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            SchemaInfo schemaInfo = this.GetSourceTreeSchemaInfo();
            if (!this.ValidateSource(schemaInfo))
            {
                return;
            }

            if (this.targetDbConnectionInfo == null)
            {
                MessageBox.Show("Target connection info is null.");
                return;
            }

            if (this.sourceDbConnectionInfo.Server == this.targetDbConnectionInfo.Server && this.sourceDbConnectionInfo.Database == this.targetDbConnectionInfo.Database)
            {
                MessageBox.Show("Source database cannot be equal to the target database.");
                return;
            }

            DatabaseType sourceDbType = this.GetDatabaseType(this.cboSourceDB.Text);
            DatabaseType targetDbType = this.GetDatabaseType(this.cboTargetDB.Text);

            int dataBatchSize = SettingManager.Setting.DataBatchSize;
            GenerateScriptOption sourceScriptOption = new GenerateScriptOption() { ScriptOutputMode = GenerateScriptOutputMode.None, DataBatchSize = dataBatchSize };
            GenerateScriptOption targetScriptOption = new GenerateScriptOption() { ScriptOutputMode = (GenerateScriptOutputMode.WriteToString), DataBatchSize = dataBatchSize };

            this.SetGenerateScriptOption(sourceScriptOption, targetScriptOption);

            targetScriptOption.GenerateIdentity = this.chkGenerateIdentity.Checked;

            GenerateScriptMode scriptMode = this.GetGenerateScriptMode();
            if (scriptMode == GenerateScriptMode.None)
            {
                MessageBox.Show("Please specify the script mode.");
                return;
            }

            DbConvetorInfo source = new DbConvetorInfo() { DbInterpreter = DbInterpreterHelper.GetDbInterpreter(sourceDbType, this.sourceDbConnectionInfo, sourceScriptOption) };
            DbConvetorInfo target = new DbConvetorInfo() { DbInterpreter = DbInterpreterHelper.GetDbInterpreter(targetDbType, this.targetDbConnectionInfo, targetScriptOption) };

            DbConvertor dbConvertor = new DbConvertor(source, target, null);
            dbConvertor.Option.GenerateScriptMode = scriptMode;

            dbConvertor.OnFeedback += Feedback;

            if (sourceDbType == DatabaseType.MySql)
            {
                source.DbInterpreter.Option.InQueryItemLimitCount = 2000;
            }

            if (targetDbType == DatabaseType.SqlServer)
            {
                target.DbOwner = this.txtTargetDbOwner.Text ?? "dbo";
            }
            else if (targetDbType == DatabaseType.MySql)
            {
                target.DbInterpreter.Option.RemoveEmoji = true;
            }
            else if (targetDbType == DatabaseType.Oracle)
            {
                dbConvertor.Option.SplitScriptsToExecute = true;
                dbConvertor.Option.ScriptSplitChar = ';';
            }

            DataTransferErrorProfile dataErrorProfile = null;
            if (this.chkPickup.Checked && scriptMode.HasFlag(GenerateScriptMode.Data))
            {
                dataErrorProfile = DataTransferErrorProfileManager.GetProfile(this.sourceDbConnectionInfo, this.targetDbConnectionInfo);
                if (dataErrorProfile != null)
                {
                    dbConvertor.Option.PickupTable = new Table() { Owner = schemaInfo.Tables.FirstOrDefault()?.Owner, Name = dataErrorProfile.SourceTableName };
                }
            }

            this.btnExecute.Enabled = false;
            this.btnCancel.Enabled = true;

            bool success = false;
            try
            {
                await dbConvertor.ConvertAsync(schemaInfo, false);
                success = true;

                if (dataErrorProfile != null)
                {
                    DataTransferErrorProfileManager.Remove(dataErrorProfile);
                }
            }
            catch (Exception ex)
            {
                string errMsg = ex.Message;

                sbFeedback.AppendLine("Error:" + ex.Message);
                if (ex.InnerException != null)
                {
                    sbFeedback.AppendLine("Innser Exception:" + ex.InnerException.Message);
                }

                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sbFeedback.AppendLine(ex.StackTrace);
                }

                this.AppendErrorMessage(errMsg);

                this.txtMessage.SelectionStart = this.txtMessage.TextLength;
                this.txtMessage.ScrollToCaret();

                this.btnExecute.Enabled = true;
                this.btnCancel.Enabled = false;

                if (ex is TableDataTransferException dataException)
                {
                    DataTransferErrorProfileManager.Save(new DataTransferErrorProfile
                    {
                        SourceServer = dataException.SourceServer,
                        SourceDatabase = dataException.SourceDatabase,
                        SourceTableName = dataException.SourceTableName,
                        TargetServer = dataException.TargetServer,
                        TargetDatabase = dataException.TargetDatabase,
                        TargetTableName = dataException.TargetTableName
                    });
                }

                MessageBox.Show(ex.Message);
            }

            LogHelper.Log(sbFeedback.ToString());
            sbFeedback.Clear();

            if (success)
            {
                this.btnExecute.Enabled = true;
                this.btnCancel.Enabled = false;

                this.txtMessage.AppendText(Environment.NewLine + DONE);
                MessageBox.Show(DONE);
            }
        }

        private void Feedback(FeedbackInfo info)
        {
            this.Invoke(new Action(() =>
            {
                sbFeedback.AppendLine($"{info.InfoType}:{info.Message}");

                if (info.InfoType == FeedbackInfoType.Error)
                {
                    this.AppendErrorMessage(info.Message);
                }
                else
                {
                    this.txtMessage.Text += (this.txtMessage.Text.Length > 0 ? Environment.NewLine : "") + info.Message;
                }

                this.txtMessage.SelectionStart = this.txtMessage.TextLength;
                this.txtMessage.ScrollToCaret();
            }));
        }

        private void AppendErrorMessage(string errMsg)
        {
            int start = this.txtMessage.Text.Length;
            this.txtMessage.Text += (this.txtMessage.Text.Length > 0 ? Environment.NewLine : "") + errMsg;

            this.txtMessage.Select(start, errMsg.Length + 1);
            this.txtMessage.SelectionColor = Color.Red;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.convertorBackgroundWorker.CancelAsync();
            this.btnExecute.Enabled = true;
            this.btnCancel.Enabled = false;
        }

        private void btnSourceScript_Click(object sender, EventArgs e)
        {
            if (!this.sourceScriptBackgroundWorker.IsBusy)
            {
                this.sourceScriptBackgroundWorker.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show("The worker is busy now.");
            }
        }

        private void SourceScriptBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (this.sourceScriptBackgroundWorker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            SchemaInfo schemaInfo = this.GetSourceTreeSchemaInfo();
            if (!this.ValidateSource(schemaInfo))
            {
                return;
            }

            DatabaseType sourceDbType = this.GetDatabaseType(this.cboSourceDB.Text);

            int dataBatchSize = SettingManager.Setting.DataBatchSize;
            GenerateScriptOption sourceScriptOption = new GenerateScriptOption() { ScriptOutputMode = GenerateScriptOutputMode.None, DataBatchSize = dataBatchSize };

            this.SetGenerateScriptOption(sourceScriptOption);

            GenerateScriptMode scriptMode = this.GetGenerateScriptMode();
            if (scriptMode == GenerateScriptMode.None)
            {
                MessageBox.Show("Please specify the script mode.");
                return;
            }

            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(sourceDbType, this.sourceDbConnectionInfo, sourceScriptOption);
            string[] tableNames = schemaInfo.Tables.Select(item => item.Name).ToArray();
            schemaInfo = dbInterpreter.GetSchemaInfo(tableNames);

            dbInterpreter.Subscribe(this);

            if (scriptMode.HasFlag(GenerateScriptMode.Schema))
            {
                dbInterpreter.GenerateSchemaScripts(schemaInfo);
            }

            if (scriptMode.HasFlag(GenerateScriptMode.Data))
            {
                dbInterpreter.GenerateDataScripts(schemaInfo);
            }

            MessageBox.Show(DONE);
        }

        private void settingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmSetting frmSetting = new frmSetting();
            frmSetting.ShowDialog();
        }

        #region IObserver<FeedbackInfo>
        void IObserver<FeedbackInfo>.OnCompleted()
        {
        }
        void IObserver<FeedbackInfo>.OnError(Exception error)
        {
        }
        void IObserver<FeedbackInfo>.OnNext(FeedbackInfo info)
        {
            this.Feedback(info);
        }
        #endregion

        private void cboSourceProfile_DrawItem(object sender, DrawItemEventArgs e)
        {
            this.profileCombobox_DrawItem(sender, e);
        }

        private void profileCombobox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combobox = sender as ComboBox;
            if (combobox.DroppedDown)
            {
                e.DrawBackground();
            }

            e.DrawFocusRectangle();

            var items = combobox.Items;

            if (e.Index < 0)
            {
                e.Graphics.DrawString(combobox.Text, e.Font, new SolidBrush(e.ForeColor), e.Bounds.Left, e.Bounds.Y);
            }
            else
            {
                if (items.Count > 0 && e.Index < items.Count)
                {
                    ConnectionInfoProfile model = items[e.Index] as ConnectionInfoProfile;
                    e.Graphics.DrawString(model.Description, e.Font, new SolidBrush(combobox.DroppedDown ? e.ForeColor : Color.Black), e.Bounds.Left, e.Bounds.Y);
                }
            }
        }

        private void cboTargetProfile_DrawItem(object sender, DrawItemEventArgs e)
        {
            this.profileCombobox_DrawItem(sender, e);
        }

        private void btnRemoveSource_Click(object sender, EventArgs e)
        {
            this.RemoveProfile(true);
        }

        private void btnRemoveTarget_Click(object sender, EventArgs e)
        {
            this.RemoveProfile(false);
        }

        private void RemoveProfile(bool isSource)
        {
            DialogResult dialogResult = MessageBox.Show("Area you sure to delete the profile?", "Confirm", MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.Yes)
            {
                ComboBox dbTypeCombobox = isSource ? this.cboSourceDB : this.cboTargetDB;
                ComboBox profileCombobox = isSource ? this.cboSourceProfile : this.cboTargetProfile;
                DatabaseType dbType = this.GetDatabaseType(dbTypeCombobox.Text);
                string profileName = (profileCombobox.SelectedItem as ConnectionInfoProfile).Name;
                if (ConnectionInfoProfileManager.Remove(dbType, profileName))
                {
                    this.LoadProfileNames(isSource);
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.convertorBackgroundWorker.IsBusy)
            {
                if (MessageBox.Show("Are you sure to abandon current task?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.convertorBackgroundWorker.CancelAsync();
                    e.Cancel = false;
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        private void btnCopyMessage_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(this.txtMessage.Text);
            MessageBox.Show("The message has been copied to clipboard.");
        }

        private void btnSaveMessage_Click(object sender, EventArgs e)
        {
            this.saveFileDialog1.Filter = "txt files|*.txt|all files|*.*";
            DialogResult dialogResult = this.saveFileDialog1.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                File.WriteAllLines(this.saveFileDialog1.FileName, this.txtMessage.Text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                this.saveFileDialog1.Reset();
            }
        }
    }
}
