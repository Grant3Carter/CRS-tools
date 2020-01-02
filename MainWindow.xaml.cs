using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml;

namespace CRS
{
    /// <summary>
    /// Convert CRS native data files to XML
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Members
        public List<table> tables = new List<table>();

        public static int maximumRows = 100;

        [ThreadStatic]
        public static BackgroundWorker BW;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        #region RefreshButton
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            tables = new List<table>();

            Mouse.OverrideCursor = Cursors.AppStarting;
            Refresh.IsEnabled = false;
            ExportSelected.IsEnabled = false;

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Refresh_DoWork;

            worker.ProgressChanged += Export_ProgressChanged;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Refresh_RunWorkerCompleted);

            worker.RunWorkerAsync();
        }

        void Refresh_DoWork(object sender, DoWorkEventArgs e)
        {
            MainWindow.BW = sender as BackgroundWorker;

            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (File.Exists("CRStoXML.txt"))
            {
                foreach (var l in File.ReadAllLines("CRStoXML.txt"))
                {
                    // ! comment 
                    if (l.StartsWith("!"))
                        continue;

                    var words = l.Split(' ');

                    // cd = change directory
                    if (l.StartsWith("cd "))
                        Directory.SetCurrentDirectory(words[1]);

                    // maximumRows for initial preview
                    else if (l.StartsWith("maximumRows "))
                        Int32.TryParse(words[1], out maximumRows);

                    else if (!String.IsNullOrEmpty(l))
                    {
                        // otherwise CDA table names are assumed (.def extension is optional)
                        if (File.Exists(l) || File.Exists(l + ".def"))
                            tables.Add(new table((Path.GetFileNameWithoutExtension(l) + ".def").ToLower()));
                    }
                }
                // TODO alternatively, consider auto "use" based on lookup and table requirements
            }
            else
            {
                // default "standard" CRS application folder
                Directory.SetCurrentDirectory("c:\\crsapp\\pmswin");

                // Default partial PMS table list 
                tables.Add(new table("pms.def"));
                tables.Add(new table("pmsdomai.def"));
                tables.Add(new table("pmsatten.def"));
                tables.Add(new table("pmsprov.def"));
                tables.Add(new table("pmsltr.def"));
                tables.Add(new table("pmsitem.def"));
                tables.Add(new table("pmsoos.def"));
                tables.Add(new table("pmsdrugs.def"));
            }
        }

        void Refresh_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Refresh.IsEnabled = true;
            ExportSelected.IsEnabled = true;

            ShowTable.ItemsSource = tables;
            ShowTable.SelectedItem = tables[0]; // this triggers ShowTable_SelectionChanged, but only generate a new XML if not yet done.

            Mouse.OverrideCursor = null;
        }
        #endregion

        #region ExportButton
        private void ExportSelected_Click(object sender, RoutedEventArgs e)
        {
            var T = ShowTable.SelectedItem as table;
            if (T == null)
                return;

            ExportSelected.IsEnabled = false;

            Mouse.OverrideCursor = Cursors.AppStarting;

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Export_DoWork;

            worker.ProgressChanged += Export_ProgressChanged;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Export_RunWorkerCompleted);

            worker.RunWorkerAsync(argument: T);
        }

        void Export_DoWork(object sender, DoWorkEventArgs e)
        {
            table T = e.Argument as table;
            T.filtered = false;  // Export button will write ALL rows to the XML file - Refresh (and combo selection) only write maxRecords

            MainWindow.BW = sender as BackgroundWorker;

            T.ExportToXML(tables);
            T.filtered = true;
        }

        void Export_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        void Export_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ExportSelected.IsEnabled = true;

            Mouse.OverrideCursor = null;
        }
        #endregion

        #region DataGrid
        private void ShowTableInDataGrid(table T)
        {
            Title = "Table " + T.name + " rows " + T.recordCount + ", fields " + T.fields.Count();

            try
            {
                // Simple datagrid visualisation of a CRS table - add columns with the xml element names then read the XML into a data set bound to the datagrid
                dataGrid1.ItemsSource = null;
                dataGrid1.Columns.Clear();
                foreach (var f in T.fields)
                    switch (f.type)
                    {
                        #region SupportedFieldTypes
                        case 'S':
                        case 'V':
                        case 'L':
                        case 'E':
                        case 'B':
                        case 'W':
                        case 'D':
                        case 'T':
                        case 'M':
                        case 'F':
                        case 'G':
                        #endregion
                            DataGridTextColumn textColumn = new DataGridTextColumn();
                            textColumn.Header = f.name;
                            textColumn.Binding = new Binding(f.name);
                            dataGrid1.Columns.Add(textColumn);
                            break;

                        default:
                            break;
                    }

                if (!File.Exists(T.name + ".xml"))
                    T.ExportToXML(tables);

                if (T.recordCount > 0)
                {
                    DataSet dataSet = new DataSet();
                    dataSet.ReadXml(T.name + ".xml");
                    DataView dataView = new DataView(dataSet.Tables[0]);
                    dataGrid1.ItemsSource = dataView;

                    if (T.recordCount != dataSet.Tables[0].Rows.Count)
                        Title = "Table " + T.name + " rows " + T.recordCount + ", fields " + T.fields.Count() + ", non-deleted rows " + dataSet.Tables[0].Rows.Count;
                }
            }
            catch (Exception ex)
            {
                Title += " " + ex.Message;
            }
        }

#if WIP
        private void ExportDgvToXML()
        {
            DataTable dt = (DataTable)dataGrid1.DataSource;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML|*.xml";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    dt.WriteXml(sfd.FileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
#endif
        #endregion

        #region ShowTableCombo
        private void ShowTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var T = (sender as ComboBox).SelectedItem as table;
            if (T == null)
                return;
            ExportSelected.Content = "Export " + T.name + " to xml";
            if (Mouse.OverrideCursor != Cursors.AppStarting)
                Mouse.OverrideCursor = Cursors.Wait;
            ShowTableInDataGrid(T);
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Helper
        public static void WriteAsIndentedXML(String outputFile, String xml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlTextWriter writer = new XmlTextWriter(outputFile, null);
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
                writer.Close();
            }
            catch
            {
                File.WriteAllText(outputFile, xml);
            }
        }
        #endregion
    }

    public class table
    {
        #region Members
        // Name
        public string name;
        // Field list
        public List<field> fields = new List<field>();
        // Key management
        public List<String> keys = new List<string>();
        public field AutoKeyCode;
        public field AutoKeySegment;
        // An index of the AutoKey (typically primary key) for a CRS lookup (replaces Btreev22.c)
        public Dictionary<string, object> AutoKeys = new Dictionary<string, object>();
        // An index of the relational key (typically the primary key of the parent) for a CRS parent/child relationship  (replaces Btreev22.c)
        public Dictionary<string, object> RelationalKeys = new Dictionary<string, object>();
        // Data encapsulation - tables are read into memory in new table()
        public int recordLength;
        public int recordNumber { set; get; }
        public int recordCount { get { return data.Length / recordLength; } }
        public bool deleted { get { return (data[recordNumber * recordLength] & 0x01) != 0; } }
        private byte[] data;
        private byte[] dvm;
        // basic filtering for quicker UI previews - the limit is set as  MainWindow.maximumRows when filtered == true
        public bool filtered = true;
        // enums (enumerated type)
        public List<enumType> enums = new List<enumType>();
        #endregion

        public table(string file)
        {
            if (!File.Exists(file))
                return;
            name = System.IO.Path.GetFileNameWithoutExtension(file);
            bool bInEnum = false;
            List<String> currentEnums = new List<string>();
            int i = 0;
            foreach (var l in File.ReadAllLines(file))
            {
                if (i == 0)
                    Int32.TryParse(l.Split(' ')[0].Substring(1), out recordLength);

                #region Enum
                if (l == "+")
                {
                    bInEnum = true;
                    continue;
                }
                if (bInEnum)
                {
                    if (l == "-")
                    {
                        bInEnum = false;
                        enums.Add(new enumType() { enums = currentEnums });
                        currentEnums = new List<string>();
                    }
                    else
                        currentEnums.Add(l);
                    continue;
                }
                #endregion

                #region Field
                if (l.StartsWith("@"))
                    fields.Add(new field(l));
                #endregion

                #region Lookup
                if (l.StartsWith(":L"))
                {
                    var splits = l.Substring(2).Split(';');
                    short fieldNumber = -1;
                    Int16.TryParse(splits[0], out fieldNumber);
                    if (fieldNumber >= 0 && fieldNumber < fields.Count)
                    {
                        var field = fields[fieldNumber];
                        field.lookup = new lookup(field, splits[1]);
                    }
                }
                #endregion

                #region Key
                if (l.StartsWith("="))
                    keys.Add(l.Substring(1));
                #endregion

                i++;
            }
            IdentifyAutoKey();

            // If x64 is insufficient to deal with memory issues for large .dat or .dvm files, then re-implement using sequential reads
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read?view=netframework-4.8
            try
            {
                data = File.ReadAllBytes(Path.GetFileNameWithoutExtension(file) + ".dat");
            }
            catch
            {
                data = new byte[0];
            }
            try
            {
                dvm = File.ReadAllBytes(Path.GetFileNameWithoutExtension(file) + ".dvm");
            }
            catch
            {
                dvm = new byte[0];
            }
        }

        public void IdentifyAutoKey()
        {
            if (keys.Count == 0)
                return;

            // Support for AUTO keys in lookups (segmented and non-segmented)
            // If no AUTO key is found use the primary (1st) key 
            var AutoKey = keys.Find(k => k.ToLower().Contains("auto"));

            if (AutoKey == null)
                AutoKey = keys[0];

            var autoKeyCode = String.Empty;
            var autoKeySegment = String.Empty;
            var splits = AutoKey.Split(' ');
            int Token = 0;
            if (splits[0].ToLower().Contains("auto"))
                Token = 1;
            if (splits.Length - Token == 1)
                autoKeyCode = splits[Token];
            else
            {
                autoKeySegment = splits[Token];
                autoKeyCode = splits[Token + 1];
            };
            AutoKeyCode = fields.Find(f1 => String.Compare(f1.name, autoKeyCode, ignoreCase: true) == 0);
            AutoKeySegment = fields.Find(f1 => String.Compare(f1.name, autoKeySegment, ignoreCase: true) == 0);
        }

        public void InstantiateAutoKeys()
        {
            for (recordNumber = 0; recordNumber < recordCount; recordNumber++)
            {
                var fieldCodeValue = sget(AutoKeyCode);
                var key = AutoKeySegment == null ? fieldCodeValue : sget(AutoKeySegment) + "\t" + fieldCodeValue;
                if (!AutoKeys.ContainsKey(key))
                    AutoKeys.Add(key, recordNumber);
            }
        }

        public void InstantiateRelationalKeys(string ParentPrimaryKey = null)
        {
            // TODO implement optional ParentPrimaryKey parameter (see also // TODO Parse e.g. via ID suffix)
            // TODO while the AUTO key is usually first, it is almost always the primary key even when its not first
            var splits = keys[0].ToLower().Split(' ');
            string primaryKey = splits[0];
            if (primaryKey.Contains("auto"))
                primaryKey = splits[1];
            for (recordNumber = 0; recordNumber < recordCount; recordNumber++)
            {
                if (deleted)
                    continue;
                var key = sget(primaryKey);
                if (RelationalKeys.ContainsKey(key))
                    ((related)RelationalKeys[key]).recordNumbers.Add(recordNumber);
                else
                    RelationalKeys.Add(key, new related() { recordNumbers = new List<int> { recordNumber } });
            }
        }

        public override string ToString()
        {
            return name;
        }

        public void ExportToXML(List<table> tables = null)
        {
            StringWriter xml = new StringWriter();
            ExportToXML(tables, xml);

            if (String.IsNullOrEmpty(name))
                return;

            var outputFile = name + ".xml";
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml.ToString());
                XmlTextWriter writer = new XmlTextWriter(outputFile, null);
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
                writer.Close();
            }
            catch (Exception ex)
            {
                File.WriteAllText(outputFile, "<!-- " + ex.Message + "-->\n" + xml.ToString());
            }
        }

        private void ExportToXML(List<table> tables, StringWriter xml, string relationalKeyName = null, string parentKeyValue = null)
        {
            if (parentKeyValue == null)
            {
                var maxRecords = recordCount;
                // Unless "Export table to XML" has been clicked, FILTER to the first maximumRows (set in crsTables.txt) for better UI performance
                if (filtered && MainWindow.maximumRows > 0)
                    maxRecords = MainWindow.maximumRows;

                xml.WriteLine("<!-- " + name + " has " + maxRecords + " records -->");
                xml.WriteLine("<" + name + ">");

                for (recordNumber = 0; recordNumber < maxRecords && recordNumber < recordCount; recordNumber++)
                {
                    if (!deleted)
                        ExportFieldsToXML(tables, xml, relationalKeyName);
                    if (MainWindow.BW != null)
                        MainWindow.BW.ReportProgress(recordNumber * 100 / maxRecords);
                }
                if (MainWindow.BW != null)
                    MainWindow.BW.ReportProgress(0);
                xml.WriteLine("</" + name + ">");
            }
            else
            {
                if (!RelationalKeys.ContainsKey(parentKeyValue))
                    return;
                var related = (related)RelationalKeys[parentKeyValue];
                xml.WriteLine("<" + name + ">");
                foreach (var r in related.recordNumbers)
                {
                    recordNumber = r;
                    ExportFieldsToXML(tables, xml, relationalKeyName);
                }
                xml.WriteLine("</" + name + ">");
            }
        }

        private void ExportFieldsToXML(List<table> tables, StringWriter xml, string relationalKeyName)
        {
            xml.WriteLine("<" + name + " recordNumber=\"" + (recordNumber + 1) + "\">");
            List<String> exportedViews = new List<string>();
            foreach (var f in fields)
            {
                try
                {
                    // Don't repeat the relational key in the child xml elements
                    if (String.Compare(f.name, relationalKeyName, ignoreCase: true) == 0)
                        continue;

                    String s = String.Empty;
                    if (f.type == 'X')
                    {
                        // DONE there MAY be multiple views of the same child table - for POC only process one with no key filter conditions
                        if (relationalKeyName == null && !exportedViews.Contains(f.viewTable))
                        {
                            // WIP single level recursion only for test
                            var view = tables.Find(l => String.Compare(l.name, f.viewTable, ignoreCase: true) == 0);
                            // Re-entrant definitions NOT not be processed
                            // Child must contain the parent's relational key (WIP simplest key notations only)
                            if (view == null)
                            {
                                // KLUDGE suppress warning about pmsx since this is just a view of PMS which generally MUST already be loaded
                                if (f.viewTable != "pmsx")
                                    xml.WriteLine("<!-- view table " + f.viewTable + " not processed -->");
                            }
                            else
                                if (view.name != name && AutoKeyCode != null && view.getField(AutoKeyCode.name) != null)
                                {
                                    if (view.RelationalKeys.Count == 0)
                                        view.InstantiateRelationalKeys();
                                    view.ExportToXML(tables, xml, AutoKeyCode.name, sget(AutoKeyCode));
                                    exportedViews.Add(f.viewTable);
                                }
                        }
                    }
                    else
                        if (f.lookup == null)
                            s = XML(f);
                        else
                        {
                            if (!f.lookup.valid)
                                // e.g. Age TODO
                                continue;

                            var Domain = f.lookup.DomainTable;
                            if (Domain == null)
                                Domain = f.lookup.DomainTable = tables.Find(d => String.Compare(d.name, f.lookup.Domain, ignoreCase: true) == 0);
                            if (Domain == null)
                                continue;

                            var fieldDescription = Domain.getField(f.lookup.Description);
                            var Code = sget(f.lookup.LookupFrom);
                            var key = String.IsNullOrEmpty(f.lookup.Segment) ? Code : f.lookup.Segment + '\t' + Code;

                            if (Domain.AutoKeys.Count == 0)
                                Domain.InstantiateAutoKeys();

                            if (Domain.AutoKeys.ContainsKey(key))
                            {
                                Domain.recordNumber = (int)Domain.AutoKeys[key]; // aka dbrw
                                s = Domain.XML(fieldDescription, f.name);
                            }
                        }

                    if (!String.IsNullOrEmpty(s))
                        xml.WriteLine(s);
                }
                catch (Exception ex)
                {
                    xml.WriteLine("<!-- field + " + f.name + " threw exception " + ex.Message + " -->");
                }
            }
            xml.WriteLine("</" + name + ">");
        }

        public field getField(string fieldName)
        {
            // aka ntof
            if (fieldName == null)
                return null;
            return fields.Find(f => String.Compare(f.name, fieldName, ignoreCase: true) == 0);
        }

        public string sget(string fieldName)
        {
            var field = getField(fieldName);
            if (field == null)
                return null;
            return sget(field);
        }

        public string getEnumValue(field field)
        {
            if (field == null)
                return null;

            if (field.type != 'E')
                return sget(field);

            var R = recordNumber * recordLength + field.offset;
            var Byte = data[R];
            return Byte == 255 ? "" : Byte + "";
        }

        public string sget(field field)
        {
            if (field == null)
                return null;

            var s = String.Empty;
            var R = recordNumber * recordLength + field.offset;

            if (field.lookup != null)
                return "lookup";

            StringBuilder sb; 

            switch (field.type)
            {
                // https://stackoverflow.com/questions/1003275/how-to-convert-utf-8-byte-to-string
                // Legacy C code was simpler because it used pointers but which are unavailable in C#
                case 'S':
                    sb = new StringBuilder();
                    for (int i = 0; i < field.length; i++)
                    {
                        var Char = data[R + i];
                        if (Char < 32)
                            break;
                        sb.Append(Convert.ToChar(Char));
                    }
                    return sb.ToString().Trim();

                case 'V':
                    sb = new StringBuilder();
                    long offset = System.BitConverter.ToInt32(data, R);
                    if (offset > 0 && offset < dvm.Length)
                        for (var i = offset; dvm[i] >= 32; i++)
                            sb.Append(Convert.ToChar(dvm[i]));

                    return sb.ToString().Trim();

                case 'T':
                    long julian = System.BitConverter.ToInt32(data, R);

                    if (julian < -37000) // MISSING == 0x80000000L, -2147483648
                        return String.Empty;
                    DateTime D = new DateTime(1900, 01, 01);
                    return D.AddDays(julian).ToString("yyyy-MM-dd");

                case 'M':
                    long time = System.BitConverter.ToInt32(data, R);

                    if (time == -2147483648)
                        return String.Empty;

                    DateTime T = new DateTime(1900, 01, 01);
                    return T.AddSeconds(time).ToString("hh:mm:ss");

                case 'F':
                    double myDouble = System.BitConverter.ToDouble(data, R);
                    var decimals = field.range > 0 ? new String('0', field.range) : "00";
                    var fps = myDouble.ToString("0." + decimals);
                    // aka FMISSING
                    return fps == "-9.87654321E-38" ? "" : fps;


                case 'G':
                    float myFloat = System.BitConverter.ToSingle(data, R);
                    // TODO FMISSING equivalent
                    return myFloat.ToString();

                case 'L':
                    var LogicalByte = data[R];
                    var x1 = LogicalByte >> field.length;
                    var x2 = (x1 & 0x01);
                    return x2 == 1 ? "yes" : "no";

                case 'B':
                    var Byte = data[R];
                    return Byte == 255 ? "" : Byte + "";

                case 'W':
                    var Word = System.BitConverter.ToInt16(data, R);
                    return Word == -32768 ? "" : Word + "";

                case 'D':
                    var DoubleWord = System.BitConverter.ToInt32(data, R);
                    return DoubleWord == -2147483648 ? "" : DoubleWord + "";

                case 'E':
                    // Enum lists are found in the .def file between + and - 
                    var Enum = data[R];
                    if (Enum == 255)
                        return String.Empty;
                    if (field.range > enums.Count)
                        return Enum + "";
                    return enums[field.range].enums[Enum];

                case 'C':
                    // WIP basic conversion of the 'C' (Document) type initially to string (for e.g. _item fields such as definition etc)
                    // Non-text formats were supported, however since the document type may be, for example, Word Perfect, the question is why bother? 
                    // The application to open them may well be long gone! Word .doc files are the only likely valid format left (but these were rarely put in 'C' fields)

                    sb = new StringBuilder();
                    long offset1 = System.BitConverter.ToInt32(data, R);
                    // The legacy dvm file format consists of an 18 byte header, which prefixes a binary (or just text) based document object
                    // For basic conversion, skip the header and copy to first null (this assumes text file format)

                    if (offset1 > 0 && offset1 < dvm.Length)
                        for (var i = offset1 + 18; dvm[i] > 0; i++)
                        {
                            var C = dvm[i];
                            if (C == '\t')
                                sb.Append(' ');
                            if (C >= 32 || C == '\r' || C == '\n')
                                sb.Append(Convert.ToChar(C));
                        }

                    // TODO consider a base64 encode if non-printing characters are encountered (but this would only make sense Word .doc format)
                    return sb.ToString().Trim();

            }
            return s;
        }

        public string XML(field field, string lookupParent = null)
        {
            var fieldName = lookupParent == null ? field.name : lookupParent;
            var s = sget(field);
            if (String.IsNullOrEmpty(s))
                return String.Empty;
            var enumValue = String.Empty;
            if (field.type == 'E')
                enumValue = "<" + fieldName + "Value>" + getEnumValue(field) + "</" + fieldName + "Value>" + enumValue;
            return "<" + fieldName + ">" + System.Net.WebUtility.HtmlEncode(s) + "</" + fieldName + ">" + enumValue;
        }
    }

    public class related
    {
        public List<int> recordNumbers = new List<int>();
    }

    public class enumType
    {
        public List<String> enums = new List<String>();
    }

    public class field
    {
        #region Members
        public string name;
        public int offset;
        public int length;
        public int range;
        public char type;
        public lookup lookup;
        public string viewTable;

        private string line;

        public List<attribute> attributes = new List<attribute>();
        #endregion

        public field(string line_)
        {
            line = line_;

            var splitSemicolon = line_.Split(';');
            if (splitSemicolon.Length > 1)
                name = splitSemicolon[1].Replace("@", "");

            var splitComma = splitSemicolon[0].Split(',');
            int i = 0;
            foreach (var s in splitComma)
            {
                switch (++i)
                {
                    case 1: // txc
                    case 2: // yc
                        break;
                    case 3: type = s[0]; break;
                    case 4: Int32.TryParse(s, out offset); break;
                    case 5: Int32.TryParse(s, out length); break;
                    case 6: Int32.TryParse(s, out range); break;
                }
            }
            if (type == 'X')
                viewTable = splitSemicolon[2].Split(' ')[0]; // TODO Parse e.g. via ID suffix
        }

        public override string ToString()
        {
            return name;
        }

        public string Description()
        {
            switch (type)
            {
                case 'B':
                case 'D':
                case 'W': return "Integer:" + length;   // if (doms = get_qualifier(fp, DOMS, (LIST*)NULL))
                case 'E': return "Labelled:" + length;
                case 'F': return "Double:" + length; //, fp->range;
                case 'G': return "Float:" + length; //, fp->range;
                case 'S': return "String:" + (length - 1);
                case 'V': return "Variable:" + (length - 1);
                case 'T': return length == 10 ? "Date:10" : "Date";
                case 'C': return "Document";
                case 'N': return "Rank";
                case 'A': return "Alias";
                case 'H': return "Group var.";
                case 'I': return "Dictionary";
                case 'L': return "Logical";
                case 'Y': return "Logical ?";
                case 'M': return "Time";
                case 'o':
                case 'q': return "Object";
                case 'O': return "Window";
                case 'P': return "Procedure";
                case 'Q': return "Query";
                case 'k': return "Class";
                case 'i': return "Class internal";
                case 'u': return "Class function";
                case 'X': return "Table";
                case 'Z': return "Library";
                case 'c': return "Character";
                case 'f': return "Float";
                case 'l': return "Logical";
                case 'm': return "Memo";
                case 'n': return "BCD";
                case 'd': return "Date";
                case '1':
                case '2':
                case '3':
                case '4': return "Internal";
            }
            return "unknown";
        }
    }

    public class attribute
    {
        #region Members
        public string name;
        public string value;
        #endregion
    }

    public class lookup
    {
        #region Members
        public string LookupFrom;
        public string Segment;
        public string Domain;
        public table DomainTable;
        public string Description;
        public bool valid = false;
        #endregion

        public lookup(field f, string lookup)
        {
            // lookup expressions (which could be in parentheses) are of the forms
            //  Title -> 'PMSDomai:TITL.descript'
            //  AttenID -> 'PMSAtten.AttenCombo'

            // parse each element into member fields
            int i = lookup.IndexOf("->");
            if (i < 0)
                return;

            LookupFrom = lookup.Substring(0, i).Trim().Replace("(", ""); // KLUDGE lookups are "real" expressions that may be parenthesised - ?need the full parser....

            var splits = lookup.Split('\'');
            if (splits.Length != 3)
                return;

            splits = splits[1].Split(':');
            // e.g. PMSDomai
            Domain = splits[0].ToLower();
            if (splits.Length > 1)
            {
                // e.g. splits[1] = TITL.descript
                splits = splits[1].Split('.');
                Segment = splits[0];
            }
            else
            {
                splits = splits[0].Split('.');
                Domain = splits[0].ToLower();
            }

            // e.g. splits[0] = TITL, splits[1] = descript

            Description = splits[1].ToLower();
            valid = true;
        }
    }
}
