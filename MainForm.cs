using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LTSpice_Lib_Merger
{
    public partial class MainForm : Form
    {
        // List of valid extensions to merge
        private static readonly string[] _validExts = {"bjt", "dio", "jft", "mos"};

        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void bOk_Click(object sender, EventArgs e)
        {
            // Proceed to scan the source directory
            lbLog.Items.Add("- Scanning Source folder " + folderSrc.Text);
            var srcFileLists = new Dictionary<string, List<string>>();
            TreeScan(folderSrc.Text, ref srcFileLists);
            lbLog.Items.Add($"- Found {srcFileLists.Count} files in Source folder");

            // And proceed to scan the destination directory
            lbLog.Items.Add("- Scanning Destination folder " + folderDst.Text);
            var dstFileLists = new Dictionary<string, List<string>>();
            TreeScan(folderDst.Text, ref dstFileLists);
            lbLog.Items.Add($"- Found {dstFileLists.Count} files in Destination folder");

            // Go one by one of the source folder files
            foreach (var srcEntry in srcFileLists)
            {
                var srcContents = "";
                foreach (var srcFile in srcEntry.Value)
                {
                    lbLog.Items.Add($"- Processing file {srcFile}");

                    // Get the original file contents and append it
                    srcContents += GetCleanedFileContentsAsAscii(srcFile);
                }

                // Now go for each destination
                if (dstFileLists.ContainsKey(srcEntry.Key))
                    foreach (var dstFile in dstFileLists[srcEntry.Key])
                    {
                        // Get the destination file contents
                        var dstContents = GetCleanedFileContentsAsAscii(dstFile);

                        // Add both files
                        var merged = srcContents + dstContents;

                        // Split it into lines
                        var lines = new List<string>(merged.Split('\n'));

                        // Here, there could be a problem: More than 1 model for the same ID.
                        //  We must fix this.
                        var models = new Dictionary<string, List<Model>>();
                        foreach (var model in lines)
                        {
                            // Parse the model
                            var mdl = new Model(model);

                            // We are only interested in models ...
                            if (!mdl.IsValid) continue;

                            // At this point, we have a valid model parsed

                            // If the model was not contained into our db, add it
                            if (!models.ContainsKey(mdl.Name))
                            {
                                var lst = new List<Model> {mdl};
                                models[mdl.Name] = lst;
                                continue;
                            }

                            // The model WAS contained into our DB, we need to compare to the list of already contained models
                            var inList = false;
                            var modelList = models[mdl.Name];
                            for (var i = 0; i < modelList.Count; i++)
                            {
                                var m = modelList[i];
                                switch (m.Compare(mdl))
                                {
                                    case Model.CompareResult.Same:
                                        // Both models are the same, do nothing and stop looking for it
                                        inList = true;
                                        break;

                                    case Model.CompareResult.Different:
                                        // Models are different - Continue search
                                        break;

                                    case Model.CompareResult.OtherMoreComplete:
                                        // The other model is more complete, substitute the current one
                                        modelList[i] = mdl;
                                        inList = true;
                                        break;

                                    case Model.CompareResult.RefMoreComplete:
                                        // The already existing model is more complete, just keep it
                                        inList = true;
                                        break;
                                }

                                if (inList)
                                    break;
                            }

                            // If the model was not found in the list, we must add it to the list
                            if (!inList)
                                modelList.Add(mdl);
                        }

                        // Now convert back the model database to list
                        var result = new List<string>();
                        // Go model by different model
                        foreach (var modelEntry in models)
                        {
                            var postFix = "";
                            // Now go with all the models for the same name
                            foreach (var model in modelEntry.Value)
                            {
                                result.Add(".model " + model.Name + postFix + " " + model.Def());
                                postFix += "_";
                            }
                        }

                        // Sort it
                        result.Sort();

                        // Finally, overwrite the destination
                        lbLog.Items.Add($"- Writing merged result to file {dstFile}");
                        File.WriteAllLines(dstFile, result);
                    }
            }
        }

        /// <summary>
        ///     Get cleaned up file contents as ASCII
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        private static string GetCleanedFileContentsAsAscii(string f)
        {
            // Get the file encoding first
            var srcEncoding = EncodingHelper.GetEncoding(f);
            if (srcEncoding == null)
                srcEncoding = Encoding.ASCII;

            // Read the file as bytes 
            var srcContentsInBytes = File.ReadAllBytes(f);

            // Convert encoding to ANSI
            var iso = Encoding.GetEncoding("ISO-8859-1");
            var isoBytes = Encoding.Convert(srcEncoding, iso, srcContentsInBytes);
            var data = iso.GetString(isoBytes);

            // Replace tabs by spaces
            data = data.Replace("\t", " ");

            // Now, remove all redundant spaces
            while (true)
            {
                var newData = data.Replace("  ", " ");
                if (newData == data) break;
                data = newData;
            }

            // Now, convert to unix line endings
            data = data.Replace("\r\n", "\n");

            // Remove all ending spaces
            while (true)
            {
                var newData = data.Replace(" \n", "\n");
                if (newData == data) break;
                data = newData;
            }

            // Remove all starting spaces
            while (true)
            {
                var newData = data.Replace("\n ", "\n");
                if (newData == data) break;
                data = newData;
            }

            // Remove all blank lines
            while (true)
            {
                var newData = data.Replace("\n\n", "\n");
                if (newData == data) break;
                data = newData;
            }

            // Coalesce continued models into one line
            data = data.Replace("\n+", " ");

            // And redo the redundant space suppression
            while (true)
            {
                var newData = data.Replace("  ", " ");
                if (newData == data) break;
                data = newData;
            }

            // If the file does not end in \n, add it
            if (data.Length > 0 && data[data.Length - 1] != '\n')
                data += '\n';
            return data;
        }

        private void folderSrc_Click(object sender, EventArgs e)
        {
            fileBrowser.Description = "Select the folder of the source models to add";
            //fileBrowser.RootFolder = Environment.SpecialFolder.MyDocuments;
            fileBrowser.SelectedPath = folderSrc.Text;
            fileBrowser.ShowNewFolderButton = false;

            // Open folder
            if (fileBrowser.ShowDialog(this) != DialogResult.OK)
                return;

            folderSrc.Text = fileBrowser.SelectedPath;

            // Enable import button if possible
            bOk.Enabled = !string.IsNullOrEmpty(folderSrc.Text) && !string.IsNullOrEmpty(folderDst.Text);
        }

        private void folderDst_Click(object sender, EventArgs e)
        {
            fileBrowser.Description = "Select the folder of the destination where models will be added";
            //fileBrowser.RootFolder = Environment.SpecialFolder.MyDocuments;
            fileBrowser.SelectedPath = folderDst.Text;
            fileBrowser.ShowNewFolderButton = false;

            // Open folder
            if (fileBrowser.ShowDialog(this) != DialogResult.OK)
                return;

            folderDst.Text = fileBrowser.SelectedPath;

            // Enable import button if possible
            bOk.Enabled = !string.IsNullOrEmpty(folderSrc.Text) && !string.IsNullOrEmpty(folderDst.Text);
        }

        private static void TreeScan(string sDir, ref Dictionary<string, List<string>> fileLists)
        {
            foreach (var f in Directory.GetFiles(sDir))
            {
                // Get the extension
                var ext = Path.GetExtension(f).ToLower();

                // Remove the starting dot
                if (ext.Length <= 0)
                    continue;
                ext = ext.Substring(1);

                // Only if supported extension found
                if (!_validExts.Contains(ext))
                    continue;

                var file = Path.GetFileName(f);
                if (!fileLists.ContainsKey(file)) fileLists[file] = new List<string>();

                fileLists[file].Add(f);
            }

            foreach (var d in Directory.GetDirectories(sDir))
                TreeScan(d, ref fileLists);
        }
    }
}