using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Singleton;
using System.Diagnostics;
using System.Reflection;
using System.Collections;
using File = System.IO.File;

namespace FirstApp
{
    public partial class Form1 : Form
    {

        SldWorks swApp;
        ModelDoc2 swModel;
        string productName = "Assembra";
        bool alreadyShowingData = false;
        DataGridView dataGridView = new DataGridView();
        string fileName = String.Empty;
        private int errors;
        Dictionary<string, string> imageDict = new Dictionary<string, string>();
        DataTable data;

        // Initialize the form window
        public Form1()
        {
            InitializeComponent();

            // Set button states
            openBtn.Enabled = false;
            quitBtn.Enabled = false;
            exportAscsvToolStripMenuItem.Enabled = false;

            // Set window name
            this.Text = productName;

            toolStripStatusLabel1.Text = "No file open";
            //this.MaximizeBox = false;
            //this.FormBorderStyle = FormBorderStyle.FixedSingle;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        // Close the Solidworks application (not necessary)
        private void quitBtn_Click(object sender, EventArgs e)
        {
            SolidWorksSingleton.Dispose();
        }

        // Browse to a file
        private void openModelOption_Click(object sender, EventArgs e)
        {
            OpenFileDialog CADfile = new OpenFileDialog();
            CADfile.Title = "Browse to CAD file";
            CADfile.Filter = "STEP files (*.stp;*.STEP)|*.stp;*.STEP";
            //CADfile.Filter = "STEP files (*.stp;*.STEP)|*.stp;*.STEP|All files (*.*)|*.*";
            CADfile.FilterIndex = 2;
            CADfile.RestoreDirectory = true;
            if (CADfile.ShowDialog() == DialogResult.OK)
            {
                fileName = CADfile.FileName;
                Debug.WriteLine(fileName);
                openBtn.Enabled = true;
                openBtn.Select();
                quitBtn.Enabled = true;
                toolStripStatusLabel1.Text = "Ready to process";
                exportAscsvToolStripMenuItem.Enabled = true;
            }

            // set the form title text
            this.Text = productName + " - " + fileName;
        }

        // Send the user to an online "about" page
        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://magnushoegholm.com/assembra");
        }

        // Process the model
        private void openBtn_Click_1(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(fileName))
            {
                
                // Open the SolidWorks application
                Debug.WriteLine("Getting application");
                toolStripStatusLabel1.Text = "Getting application..";
                swApp = SolidWorksSingleton.getApplication();

                // Get import information
                ImportStepData swImportStepData = (ImportStepData)swApp.GetImportFileData(fileName);

                // Set import options (optional)
                swImportStepData.MapConfigurationData = false; // Map configuration data

                // Import the STEP file
                ModelDoc2 swModel = (ModelDoc2)swApp.LoadFile4(fileName, "r", swImportStepData, ref errors);

                // Run Solidworks in the background
                swModel.Visible = false;

                
                if (errors != 0)
                {
                    Debug.WriteLine("Wow an error");
                }

                var swAssemblyDoc = (AssemblyDoc)swModel;

                // Create folder for screen captures (visualization within Assembra)
                string subfolder = "captures";

                // Combine the input file directory and the subfolder to create the output directory
                string outputDirectory = Path.Combine(Path.GetDirectoryName(fileName), subfolder);
                DirectoryInfo di = Directory.CreateDirectory(outputDirectory);
                Debug.WriteLine(outputDirectory);

                // Visualize the main assembly
                string imagePath = MagnusTools.captureImage(swModel, outputDirectory);
                pictureBox1.ImageLocation = imagePath;

                // Process the model
                toolStripStatusLabel1.Text = "Processing model..";
                data = MagnusTools.processModel(swAssemblyDoc, swApp, outputDirectory);

                // Create dropdown for visualizations
                comboBox1.Visible = true;
                imageDict = MagnusTools.createDictFromTable(data,imagePath);
                addToComboBox(imageDict);


                if (data != null) {
                    // Show the returned data in a table
                    showTable(data);
                    toolStripStatusLabel1.Text = "Model processed";
                }
                else
                {
                    toolStripStatusLabel1.Text = "Model had no interferences";
                }
                
                // Close Solidworks
                SolidWorksSingleton.Dispose();

            }
            else
            {
                Debug.WriteLine("No file selected");
                return;
            }
        }

        // Displays the constraints in a table
        private void showTable(DataTable data)
        {

            // If a datagridview has already been built, simply update the table information
            if (alreadyShowingData)
            {
                updateTable(data);
                return;
            }

            // Create a grid view
            this.wholeWindow.Controls.Add(dataGridView,1,1);

            // Add the data to the data grid view with styling
            DataTable dataTable = data;
            dataGridView.DataSource = dataTable;
            dataGridView.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView.Dock = DockStyle.Fill; // Dock the DataGridView to fill the form
            dataGridView.RowHeadersVisible = false;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.AllowUserToResizeColumns = false;
            dataGridView.Columns.RemoveAt(dataGridView.Columns.Count - 1);
            //dataGridView.Columns[0].Width = 240;
            dataGridView.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            for (int i = 1; i < dataGridView.Columns.Count; i++)
            {
                dataGridView.Columns[i].Width = 35;
            }
            dataGridView.RowPrePaint += DataGridView_RowPrePaint;
            dataGridView.MaximumSize = new Size(1000, dataGridView.MaximumSize.Height);
            void DataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
            {
                dataGridView.Rows[e.RowIndex].DefaultCellStyle.Font = new Font("Inter", 8);
            }

            // Remove selections
            dataGridView.ClearSelection();

            // So we do not need to built the grid view again:
            alreadyShowingData = true;

            

        }

        // Updates the values of the table, in case a new model is loaded
        private void addToComboBox(Dictionary<string, string> imageDict)
        {
            
            foreach (var key in imageDict.Keys)
            {
                comboBox1.Items.Add(key);
            }

        }

        // Updates the values of the table, in case a new model is loaded
        private void updateTable(DataTable data)
        {
            // Update the existing data grid view with data from a new model
            dataGridView.DataSource = data;
            dataGridView.ClearSelection();
        }

        // Change visualization according to dropdown selection
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            pictureBox1.ImageLocation = imageDict[comboBox1.Text];
        }

        private void exportAscsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Create a SaveFileDialog
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            saveFileDialog.Title = "Save CSV File";

            // Show the dialog and get the selected file name and location
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Get the file name
                string fileName = saveFileDialog.FileName;

                // Save the DataTable to CSV
                SaveDataTableToCSV(data, fileName);

                Console.WriteLine("CSV file saved successfully.");
            }
            else
            {
                Console.WriteLine("User canceled the operation.");
            }
        }

        static void SaveDataTableToCSV(DataTable dataTable, string filePath)
        {
            StringBuilder sb = new StringBuilder();

            // Write the column headers
            foreach (DataColumn column in dataTable.Columns)
            {
                sb.Append(column.ColumnName);
                sb.Append(',');
            }
            sb.AppendLine();

            // Write the data rows
            foreach (DataRow row in dataTable.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    sb.Append(item);
                    sb.Append(',');
                }
                sb.AppendLine();
            }

            // Write to the CSV file
            File.WriteAllText(filePath, sb.ToString());
        }

        private void topMenu_Paint(object sender, PaintEventArgs e)
        {

        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void wholeWindow_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        
    }

    

}
