﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows.Forms;
using GPDWin2XTUManager.UpdateChecks;
using Newtonsoft.Json;

namespace GPDWin2XTUManager
{
    public partial class MainForm : Form
    {
        private List<Button> _profileButtons = new List<Button>();
        private List<XTUProfile> _xtuProfiles = new List<XTUProfile>();

        private GithubRelease _newRelease;

        public MainForm(string[] args = null)
        {
            InitializeComponent();
            CheckForXTU();
            StartXTUService();

            if (args == null)
            {
                return;
            }

            if (args.Length > 0)
            {
                if (args.Length == 4) // Temp profile application. Parameters: minW maxW cpuUV gpuUV
                {
                    XTUProfile tempProfile = new XTUProfile("TEMP", Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
                    ApplyXTUProfile(tempProfile);
                    Environment.Exit(0);
                }
                else if (args.Length == 1) // Apply profile by name. Parameter: profile name.
                {
                    XTUProfile profileToApply = _xtuProfiles.Find(p => p.Name == args[0]);

                    if (profileToApply != null)
                    {
                        ApplyXTUProfile(profileToApply);
                        Environment.Exit(0);
                    }
                    else
                    {
                        MessageBox.Show("Attempted to start a profile named " + args[0] + " that isn't defined. Closing application.", "GPD Win 2 XTU Manager: Profile not defined!",MessageBoxButtons.OK,MessageBoxIcon.Error);
                        Environment.Exit(404);
                    }
                }
                else
                {
                    MessageBox.Show("Incorrect number of arguments. Expected 4, but was given " + args.Length);
                }

            }

        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            Text += " v" + Shared.VERSION;
            _newRelease = await UpdateChecker.CheckForUpdates();

            if (_newRelease != null)
            {
                btnUpdateAvailable.Visible = true;
                btnUpdateAvailable.Text = "v" + _newRelease.tag_name + " is available!\r\nClick for changelog.";
            }
        }

        private void CheckForXTU()
        {
            if (!File.Exists(Shared.XTU_PATH))
            {
                if (MessageBox.Show("The Intel Extreme Tuning Utility couldn't be found. Open download page?",
                        "Unable to find XTU", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                {
                    Process.Start("https://downloadcenter.intel.com/download/24075/Intel-Extreme-Tuning-Utility-Intel-XTU-");
                }

                Environment.Exit(0);

            }
            else
            {
                InitializeProgram();
            }
        }

        private void InitializeProgram()
        {
            FillButtonList();
            ReadCurrentValues();
            LoadProfilesIntoList();
        }

        private void ReadCurrentValues()
        {
            try
            {
                string minW = ExecuteInXTUAndGetOutput("-t -id 48").Trim();
                string maxW = ExecuteInXTUAndGetOutput("-t -id 47").Trim();
                string cpuUV = ExecuteInXTUAndGetOutput("-t -id 34").Trim();
                string gpuUV = ExecuteInXTUAndGetOutput("-t -id 100").Trim();

                txtInfo.Text += "Current values: \r\n" + "Min W: \r\n" + minW + "\r\nMax W: \r\n" + maxW + "\r\nCPU UV: \r\n" + cpuUV + "\r\nGPU UV: \r\n" + gpuUV;
            }
            catch
            {
                txtInfo.Text += "Couldn't read current values.";
            }


        }

        private string ExecuteInXTUAndGetOutput(string command)
        {
            string output = string.Empty;

            ProcessStartInfo processStartInfo = new ProcessStartInfo(Shared.XTU_PATH, command);
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
            processStartInfo.UseShellExecute = false;

            Process process = Process.Start(processStartInfo);
            using (StreamReader streamReader = process.StandardOutput)
            {
                output = streamReader.ReadToEnd();
            }

            return output;
        }

        private void FillButtonList()
        {
            _profileButtons.Add(btnProfile1);
            _profileButtons.Add(btnProfile2);
            _profileButtons.Add(btnProfile3);
            _profileButtons.Add(btnProfile4);
            _profileButtons.Add(btnProfile5);
            _profileButtons.Add(btnProfile6);
            _profileButtons.Add(btnProfile7);
            _profileButtons.Add(btnProfile8);
        }

        private void LoadProfilesIntoList()
        {
            _xtuProfiles.Clear();

            if (!File.Exists(Shared.SETTINGS_PATH))
            {
                CreateNewSettings();
            }

            _xtuProfiles = JsonConvert.DeserializeObject<List<XTUProfile>>(File.ReadAllText(Shared.SETTINGS_PATH));
            RefreshButtonInfo();
        }

        private void RefreshButtonInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                if (i < _xtuProfiles.Count)
                {
                    XTUProfile profile = _xtuProfiles[i];
                    _profileButtons[i].Text = profile.Name + "\n\n" + "Min W: " + profile.MinimumWatt + "\nMax W: " +
                                              profile.MaximumWatt + "\nCPU Undervolt: -" + profile.CPUUndervolt +
                                              " mV" + "\nGPU Undervolt: -" + profile.GPUUndervolt + " mV";
                }
                else
                {
                    _profileButtons[i].Text = "Create profile...";
                }


            }
        }

        private void CreateNewSettings()
        {
            AddDefaultProfiles();
            Shared.SaveProfilesToDisk(_xtuProfiles);
        }

        private void AddDefaultProfiles()
        {
            _xtuProfiles.Add(new XTUProfile("STOCK", 7, 15, 0, 0));
        }

        public void ApplyProfileByButton(int number)
        {
            if (number < _xtuProfiles.Count)
            {
                ApplyXTUProfile(_xtuProfiles[number]);
            }
            else
            {
                OpenSettings();
            }
        }

        private void ApplyXTUProfile(XTUProfile xtuProfile)
        {
            StartXTUService();

            string minWResult = ExecuteInXTUAndGetOutput("-t -id 48 -v " + xtuProfile.MinimumWatt);
            string maxWResult = ExecuteInXTUAndGetOutput("-t -id 47 -v " + xtuProfile.MaximumWatt);
            string cpuUvResult = ExecuteInXTUAndGetOutput("-t -id 34 -v -" + xtuProfile.CPUUndervolt);
            string gpuUvResult = ExecuteInXTUAndGetOutput("-t -id 100 -v -" + xtuProfile.GPUUndervolt);

            if (minWResult.Contains("Successful") && maxWResult.Contains("Successful") && cpuUvResult.Contains("Successful") && gpuUvResult.Contains("Successful"))
            {
                txtInfo.Text = "Applied " + xtuProfile.Name + " profile succesfully!";
                Console.WriteLine("Applied " + xtuProfile.Name + " profile succesfully!");
            }
            else
            {
                txtInfo.Text = "Failed to fully apply " + xtuProfile.Name + " profile. Results:\n";
                txtInfo.Text += minWResult + "\n" + maxWResult + "\n" + cpuUvResult + "\n" + gpuUvResult + "\n";

                Console.WriteLine("Failed to fully apply " + xtuProfile.Name + " profile. Results:\n");
                Console.WriteLine(minWResult + "\n" + maxWResult + "\n" + cpuUvResult + "\n" + gpuUvResult + "\n");
            }

            StopXTUService();
        }

        private void StartXTUService()
        {
            ServiceController service = new ServiceController("XTU3SERVICE");
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(1000 * 15);

                if (service.Status != ServiceControllerStatus.Running)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: XTU Service not found! " + e);
            }
        }

        private void StopXTUService()
        {
            ServiceController service = new ServiceController("XTU3SERVICE");
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(1000 * 15);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch
            {
                Console.WriteLine("Error: XTU Service not found!");
            }
        }

        private void btnProfile1_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(0);
        }

        private void btnProfile2_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(1);
        }

        private void btnProfile3_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(2);
        }

        private void btnProfile4_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(3);
        }

        private void btnProfile5_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(4);
        }

        private void btnProfile6_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(5);
        }

        private void btnProfile7_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(6);
        }

        private void btnProfile8_Click(object sender, EventArgs e)
        {
            ApplyProfileByButton(7);
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            Options frmOptions = new Options();
            frmOptions.Profiles = _xtuProfiles;
            frmOptions.FormClosed += FrmOptionsOnFormClosed;
            frmOptions.ShowDialog();
        }

        private void FrmOptionsOnFormClosed(object sender, FormClosedEventArgs formClosedEventArgs)
        {
            LoadProfilesIntoList();
        }

        private void btnUpdateAvailable_Click(object sender, EventArgs e)
        {
            // Show changelog, ask if user wants to open release page. If yes, open page in browser.
            if (MessageBox.Show(_newRelease.name + "\n\nChangelog:\n" + _newRelease.body + "\n\nDo you want to open the release page?", "Update available!", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                Process.Start(_newRelease.html_url);
            }
        }
    }
}
