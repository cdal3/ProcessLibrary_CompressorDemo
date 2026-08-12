#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.System;
using FTOptix.NativeUI;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.OPCUAServer;
using FTOptix.RAEtherNetIP;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.AuditSigning;
using FTOptix.EventLogger;
using FTOptix.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FTOptix.DataLogger;
using FTOptix.InfluxDBStore;
using FTOptix.InfluxDBStoreLocal;
using FTOptix.SerialPort;
#endregion

public class ImportAndExportTranslations_Tab : BaseNetLogic
{
    [ExportMethod]
    public void ExportTranslations()
    {
        Log.Info("ImportAndExportTranslations.Export", "Exporting dictionary to CSV file");
        var csvPath = GetCSVFilePath();
        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("ImportAndExportTranslations.Export", "An error was detected while reading the CSV file path, check Studio Output for more details");
            return;
        }

        bool wrapFields = GetWrapFields();

        var localizationDictionary = GetDictionary();
        if (localizationDictionary == null)
        {
            Log.Error("ImportAndExportTranslations.Export", "No valid LocalizationDictionary was found");
            return;
        }

        var dictionary = (string[,])localizationDictionary.Value.Value;
        var rowCount = dictionary.GetLength(0);
        var columnCount = dictionary.GetLength(1);

        try
        {
            using (var csvWriter = new CSVFileWriter(csvPath) { FieldDelimiter = '\t', WrapFields = wrapFields })
            {
                for (var currentRow = 0; currentRow < rowCount; ++currentRow)
                {
                    var row = new string[columnCount];

                    for (var currentColumn = 0; currentColumn < columnCount; ++currentColumn)
                    {
                        if (currentRow == 0 && currentColumn == 0)
                            row[currentColumn] = "Key";
                        else
                            row[currentColumn] = ReplaceNewLineWithSymbol(dictionary[currentRow, currentColumn]);
                    }

                    csvWriter.WriteLine(row);
                }
            }

            Log.Info("ImportAndExportTranslations.Export", $"Translations successfully exported to \"{csvPath}\"");
        }
        catch (Exception ex)
        {
            Log.Error("ImportAndExportTranslations.Export", $"Unable to export the translations: {ex}");
        }
    }

    [ExportMethod]
    public void ImportTranslations()
    {
        Log.Info("ImportAndExportTranslations.Import", "Importing translations from CSV file");
        var csvPath = GetCSVFilePath();

        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("ImportAndExportTranslations.Import", "An error was detected while reading the CSV file path, check Studio Output for more details");
            return;
        }

        bool wrapFields = GetWrapFields();

        var localizationDictionary = GetDictionary();
        if (localizationDictionary == null)
        {
            Log.Error("ImportAndExportTranslations.Import", "No valid LocalizationDictionary was found!");
            return;
        }

        if (!File.Exists(csvPath))
        {
            Log.Error("ImportAndExportTranslations.Import", $"The file at \"{csvPath}\" does not exist");
            return;
        }

        try
        {
            using (var csvReader = new CSVFileReader(csvPath) { FieldDelimiter = '\t', WrapFields = wrapFields })
            {
                if (csvReader.EndOfFile())
                {
                    Log.Error("ImportAndExportTranslations.Import", $"The file at \"{csvPath}\" is empty");
                    return;
                }

                var fileTranslations = csvReader.ReadAll();
                if (fileTranslations.Count == 0 || fileTranslations[0].Count == 0)
                    return;

                var fileTranslationsRows = fileTranslations.Count;
                var fileTranslationsColumns = fileTranslations[0].Count;

                var actualTranslations = (string[,])localizationDictionary.Value.Value;
                var actualTranslationsRows = actualTranslations.GetLength(0);
                var actualTranslationsColumns = actualTranslations.GetLength(1);

                //Retrieve all language columns from the CSV file (excluding the 'Key' column)
                var csvLanguages = new List<string>();
                for (int i = 1; i < fileTranslationsColumns; i++)
                    csvLanguages.Add(fileTranslations[0][i]);

                // Retrieve existing language columns from the dictionary
                var dictLanguages = new List<string>();
                for (int i = 1; i < actualTranslationsColumns; i++)
                    dictLanguages.Add(actualTranslations[0, i]);

                // Identify language columns that need to be added
                var newLanguages = csvLanguages.Except(dictLanguages).ToList();
                int newColCount = actualTranslationsColumns + newLanguages.Count;
                string[,] newTranslations = new string[actualTranslationsRows, newColCount];

                // Copy the header row
                newTranslations[0, 0] = "Key";
                int colIdx = 1;
                foreach (var lang in dictLanguages)
                {
                    newTranslations[0, colIdx] = lang;
                    colIdx++;
                }
                foreach (var lang in newLanguages)
                {
                    newTranslations[0, colIdx] = lang;
                    colIdx++;
                }

                // Build a mapping of language column indexes (CSV column → new dictionary column)
                var langColMap = new Dictionary<int, int>();
                for (int i = 1; i < fileTranslationsColumns; i++)
                {
                    string lang = fileTranslations[0][i];
                    int dictIdx = dictLanguages.IndexOf(lang);
                    if (dictIdx != -1)
                        langColMap[i] = 1 + dictIdx;
                    else
                        langColMap[i] = actualTranslationsColumns + newLanguages.IndexOf(lang);
                }

                // Create a mapping from keys to row numbers
                var dictKeyRowMap = new Dictionary<string, int>();
                for (int r = 1; r < actualTranslationsRows; r++)
                    dictKeyRowMap[actualTranslations[r, 0]] = r;

                int keyUpdated = 0;
                //Copy the original content
                for (int r = 1; r < actualTranslationsRows; r++)
                {
                    newTranslations[r, 0] = actualTranslations[r, 0];
                    for (int c = 1; c < actualTranslationsColumns; c++)
                        newTranslations[r, c] = actualTranslations[r, c];
                }

                // Overwrite or add new content
                for (int i = 1; i < fileTranslationsRows; i++)
                {
                    var row = fileTranslations[i];
                    string key = row[0];
                    int targetRow = dictKeyRowMap.ContainsKey(key) ? dictKeyRowMap[key] : -1;
                    if (targetRow == -1)
                    {
                        // If it's a new key, extend the row
                        string[,] resized = new string[newTranslations.GetLength(0) + 1, newColCount];
                        for (int rr = 0; rr < newTranslations.GetLength(0); rr++)
                            for (int cc = 0; cc < newColCount; cc++)
                                resized[rr, cc] = newTranslations[rr, cc];
                        targetRow = resized.GetLength(0) - 1;
                        resized[targetRow, 0] = key;
                        newTranslations = resized;
                    }
                    for (int csvCol = 1; csvCol < row.Count; csvCol++)
                    {
                        int dictCol = langColMap[csvCol];
                        string newValue = row[csvCol];
                        bool isDictLang = dictCol < actualTranslationsColumns; 
                        if (!string.IsNullOrEmpty(newValue))
                        {
                            newTranslations[targetRow, dictCol] = ReplaceSymbolWithNewLine(newValue);
                            keyUpdated++;
                        }
                        else if (isDictLang && targetRow < actualTranslationsRows)
                        {
                            //If it's an existing language column and the CSV value is empty, retain the original translation
                            newTranslations[targetRow, dictCol] = actualTranslations[targetRow, dictCol];
                        }
                        // If it's a newly added language column and the CSV value is empty, leave it blank and translate it into English one by one
                    }
                }

                localizationDictionary.Value = new UAValue(newTranslations);
                var newSize = GetDictionarySize(localizationDictionary);
                if (keyUpdated > 0)
                    Log.Info("ImportAndExportTranslations.Import", $"Successfully updated {keyUpdated} keys/fields into {localizationDictionary.BrowseName} dictionary");
            }
        }
        catch (Exception ex)
        {
            Log.Error("ImportAndExportTranslations.Import", $"Unable to import the translations: {ex}");
        }
    }

    private Int64 GetDictionarySize(IUAVariable dictionary)
    {
        var dictionaryValue = dictionary.Value;
        if (dictionaryValue == null)
            return 0;

        var dictionaryContent = (string[,])dictionary.Value.Value;
        var arraySize = dictionaryContent.GetLength(0) * dictionaryContent.GetLength(1);
        return arraySize - dictionaryContent.GetLength(0);
    }

    private string GetCSVFilePath()
    {
        var csvPathVariable = LogicObject.GetVariable("CSVPath");
        if (csvPathVariable == null)
        {
            Log.Error("ImportAndExportTranslations", "CSVPath variable not found");
            return "";
        }

        string csvPath = LogicObject.GetVariable("CSVPath").Value;
        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("ImportAndExportTranslations", "CSVPath variable is empty");
            return "";
        }
        string[] csvSplittedPath = csvPath.Split('/');
        if (csvSplittedPath.Length <= 1)
        {
            return ResourceUri.FromProjectRelativePath(LogicObject.GetVariable("CSVPath").Value).Uri;
        }
        else
        {

            return new ResourceUri(csvPathVariable.Value).Uri;
        }
    }


    private bool GetWrapFields()
    {
        var wrapFieldsVariable = LogicObject.GetVariable("WrapFields");
        if (wrapFieldsVariable == null)
        {
            Log.Error("ImportAndExportTranslations", "WrapFields variable not found");
            return false;
        }

        return wrapFieldsVariable.Value;
    }

    private IUAVariable GetDictionary()
    {
        var dictionaryVariable = LogicObject.GetVariable("LocalizationDictionary");
        if (dictionaryVariable == null)
        {
            Log.Info("ImportAndExportTranslations", "The first localization dictionary found will be used since the LocalizationDictionary variable cannot be not found");
            return GetDefaultDictionary();
        }

        NodeId nodeIdDictionaryValue = dictionaryVariable.Value;
        if (nodeIdDictionaryValue == null)
        {
            Log.Info("ImportAndExportTranslations", "The first localization dictionary found will be used since the LocalizationDictionary variable is not set");
            return GetDefaultDictionary();
        }

        var dictionaryNode = InformationModel.Get(nodeIdDictionaryValue);
        if (dictionaryNode == null)
        {
            Log.Error("ImportAndExportTranslations", "The node pointed by the LocalizationDictionary variable cannot be found");
            return null;
        }

        var resultDictionaryVariable = dictionaryNode as IUAVariable;
        if (resultDictionaryVariable == null || !resultDictionaryVariable.IsInstanceOf(FTOptix.Core.VariableTypes.LocalizationDictionary))
            Log.Error("ImportAndExportTranslations", "The node pointed by the LocalizationDictionary variable is not a localization dictionary");

        return resultDictionaryVariable;
    }

    private IUAVariable GetDefaultDictionary()
    {
        var localizationDictionaryType = Project.Current.Context.GetNode(FTOptix.Core.VariableTypes.LocalizationDictionary);
        var localizationDictionaries = localizationDictionaryType.InverseRefs.GetNodes(OpcUa.ReferenceTypes.HasTypeDefinition);

        foreach (var dictionaryNode in localizationDictionaries)
        {
            if (dictionaryNode.NodeId.NamespaceIndex == Project.Current.NodeId.NamespaceIndex)
                return (IUAVariable)dictionaryNode;
        }

        return null;
    }

    #region CSVFileReader
    private class CSVFileReader : IDisposable
    {
        public char FieldDelimiter { get; set; } = '\t';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public bool IgnoreMalformedLines { get; set; } = false;

        public CSVFileReader(string filePath, System.Text.Encoding encoding)
        {
            streamReader = new StreamReader(filePath, encoding);
        }

        public CSVFileReader(string filePath)
        {
            streamReader = new StreamReader(filePath, System.Text.Encoding.UTF8);
        }

        public CSVFileReader(StreamReader streamReader)
        {
            this.streamReader = streamReader;
        }

        public bool EndOfFile()
        {
            return streamReader.EndOfStream;
        }

        public List<string> ReadLine()
        {
            if (EndOfFile())
                return null;

            var line = streamReader.ReadLine();

            var result = WrapFields ? ParseLineWrappingFields(line) : ParseLineWithoutWrappingFields(line);

            currentLineNumber++;
            return result;

        }

        public List<List<string>> ReadAll()
        {
            var result = new List<List<string>>();
            while (!EndOfFile())
                result.Add(ReadLine());

            return result;
        }

        private List<string> ParseLineWithoutWrappingFields(string line)
        {
            if (string.IsNullOrEmpty(line) && !IgnoreMalformedLines)
                throw new FormatException($"Error processing line {currentLineNumber}. Line cannot be empty");

            if (line.Length > 1 && IsQuoteChar(line, 0) && IsQuoteChar(line, line.Length - 1))
                line = line.Substring(1, line.Length - 2);

            return line.Split(FieldDelimiter).ToList();
        }

        private List<string> ParseLineWrappingFields(string line)
        {
            var fields = new List<string>();
            var buffer = new StringBuilder("");
            var fieldParsing = false;

            int i = 0;
            while (i < line.Length)
            {
                if (!fieldParsing)
                {
                    if (IsWhiteSpace(line, i))
                    {
                        ++i;
                        continue;
                    }

                    var lineErrorMessage = $"Error processing line {currentLineNumber}";
                    if (i == 0)
                    {
                        if (!IsQuoteChar(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Expected quotation marks at column {i + 1}");
                        }

                        fieldParsing = true;
                    }
                    else
                    {
                        if (IsQuoteChar(line, i))
                            fieldParsing = true;
                        else if (!IsFieldDelimiter(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Wrong field delimiter at column {i + 1}");
                        }
                    }

                    ++i;
                }
                else
                {
                    if (IsEscapedQuoteChar(line, i))
                    {
                        i += 2;
                        buffer.Append(QuoteChar);
                    }
                    else if (IsQuoteChar(line, i))
                    {
                        fields.Add(buffer.ToString());
                        buffer.Clear();
                        fieldParsing = false;
                        ++i;
                    }
                    else
                    {
                        buffer.Append(line[i]);
                        ++i;
                    }
                }
            }

            return fields;
        }

        private bool IsEscapedQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar && i != line.Length - 1 && line[i + 1] == QuoteChar;
        }

        private bool IsQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar;
        }

        private bool IsFieldDelimiter(string line, int i)
        {
            return line[i] == FieldDelimiter;
        }

        private bool IsWhiteSpace(string line, int i)
        {
            return Char.IsWhiteSpace(line[i]);
        }

        private readonly StreamReader streamReader;
        private int currentLineNumber = 1;

        #region IDisposable support

        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamReader.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
    #endregion

    #region CSVFileWriter
    private class CSVFileWriter : IDisposable
    {
        public char FieldDelimiter { get; set; } = '\t';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public CSVFileWriter(string filePath)
        {
            streamWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        }

        public CSVFileWriter(string filePath, System.Text.Encoding encoding)
        {
            streamWriter = new StreamWriter(filePath, false, encoding);
        }

        public CSVFileWriter(StreamWriter streamWriter)
        {
            this.streamWriter = streamWriter;
        }

        public void WriteLine(string[] fields)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < fields.Length; ++i)
            {
                if (WrapFields)
                    stringBuilder.AppendFormat("{0}{1}{0}", QuoteChar, EscapeField(fields[i]));
                else
                    stringBuilder.AppendFormat("{0}", fields[i]);

                if (i != fields.Length - 1)
                    stringBuilder.Append(FieldDelimiter);
            }

            streamWriter.WriteLine(stringBuilder.ToString());
            streamWriter.Flush();
        }

        private string EscapeField(string field)
        {
            var quoteCharString = QuoteChar.ToString();
            return field.Replace(quoteCharString, quoteCharString + quoteCharString);
        }

        private readonly StreamWriter streamWriter;

        #region IDisposable Support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamWriter.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
    #endregion

    private string ReplaceNewLineWithSymbol(string i) => i.Replace("\n", newLinePlaceHolder);
    private string ReplaceSymbolWithNewLine(string i) => i.Replace(newLinePlaceHolder, "\n");

    private const string newLinePlaceHolder = "\\n";
}
