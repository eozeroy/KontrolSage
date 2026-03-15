using ExcelDataReader;
using KontrolSage.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KontrolSage.Services
{
    public class EdtImportService : IEdtImportService
    {
        public EdtImportService()
        {
            // Required for ExcelDataReader in .NET Core / .NET 5+
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public async Task<List<EdtNode>> ParseImportFileAsync(string filePath, string projectId)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("El archivo seleccionado no existe.", filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".xlsx" or ".xls" or ".csv" => await ParseExcelFileAsync(filePath, projectId),
                ".xml" => await ParseXmlFileAsync(filePath, projectId),
                _ => throw new NotSupportedException($"El formato de archivo '{extension}' no es compatible.")
            };
        }

        private async Task<List<EdtNode>> ParseExcelFileAsync(string filePath, string projectId)
        {
            return await Task.Run(() =>
            {
                var nodes = new List<EdtNode>();

                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true
                            }
                        });

                        if (result.Tables.Count == 0) return nodes;

                        var table = result.Tables[0];

                        // Look for expected column names
                        int idxIe = GetColumnIndex(table, "Unique ID", "UID", "Id único", "Id");
                        int idxName = GetColumnIndex(table, "Name", "Nombre", "Task", "Tarea");
                        int idxWbs = GetColumnIndex(table, "WBS", "EDT", "Hierarchy");
                        int idxOutlineLevel = GetColumnIndex(table, "Nivel de esquema", "Outline Level", "Nivel");
                        int idxStart = GetColumnIndex(table, "Start", "Comienzo", "Inicio", "Fecha de inicio");
                        int idxFinish = GetColumnIndex(table, "Finish", "Fin", "Fecha de fin");
                        int idxNotes = GetColumnIndex(table, "Notes", "Notas", "Comentarios");

                        if (idxName == -1)
                            throw new Exception("No se encontró la columna 'Nombre' (Name) en el archivo Excel.");

                        List<int> currentWbsParts = new List<int>();

                        foreach (DataRow row in table.Rows)
                        {
                            string name = row[idxName]?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) continue;

                            int? uid = null;
                            if (idxIe >= 0 && int.TryParse(row[idxIe]?.ToString(), out int parsedUid))
                            {
                                uid = parsedUid;
                            }

                            string wbs = string.Empty;
                            if (idxWbs >= 0)
                            {
                                wbs = row[idxWbs]?.ToString() ?? string.Empty;
                            }

                            int outlineLevel = -1;
                            if (idxOutlineLevel >= 0 && int.TryParse(row[idxOutlineLevel]?.ToString(), out int parsedLevel))
                            {
                                outlineLevel = parsedLevel;
                            }

                            // Synthesize dotted WBS from Outline Level if explicit WBS is missing
                            if (string.IsNullOrWhiteSpace(wbs) && outlineLevel > 0)
                            {
                                while (currentWbsParts.Count > outlineLevel)
                                {
                                    currentWbsParts.RemoveAt(currentWbsParts.Count - 1);
                                }
                                if (currentWbsParts.Count < outlineLevel)
                                {
                                    while (currentWbsParts.Count < outlineLevel)
                                        currentWbsParts.Add(1);
                                }
                                else
                                {
                                    currentWbsParts[outlineLevel - 1]++;
                                }
                                wbs = string.Join(".", currentWbsParts);
                            }

                            DateTime? start = ParseExcelDate(row[idxStart]);
                            DateTime? finish = ParseExcelDate(row[idxFinish]);

                            string notes = string.Empty;
                            if (idxNotes >= 0)
                            {
                                notes = row[idxNotes]?.ToString() ?? string.Empty;
                            }

                            var node = new EdtNode
                            {
                                ProjectId = projectId,
                                Name = name,
                                Ie = uid,
                                HierarchyCode = wbs,
                                ImportInicio = start,
                                ImportFin = finish,
                                ImportNotas = notes,
                                Children = new System.Collections.ObjectModel.ObservableCollection<EdtNode>()
                            };

                            nodes.Add(node);
                        }
                    }
                }

                // Try to infer ParentId based on HierarchyCode (e.g. "1.1" -> parent is "1")
                BuildHierarchyFromWbs(nodes);

                return nodes;
            });
        }

        private DateTime? ParseExcelDate(object cellValue)
        {
            if (cellValue == null || cellValue == DBNull.Value) return null;
            if (cellValue is DateTime dt) return dt;

            string s = cellValue.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return null;

            // Is it an OADate number?
            if (double.TryParse(s, out double oaDate))
            {
                try { return DateTime.FromOADate(oaDate); } catch { }
            }

            // Try standard parsing
            if (DateTime.TryParse(s, out DateTime d1)) return d1;

            // Try explicit Spanish culture
            var ci = new System.Globalization.CultureInfo("es-MX");
            if (DateTime.TryParse(s, ci, System.Globalization.DateTimeStyles.None, out DateTime d2)) return d2;
            // MS Project exports dates like "31 agosto 2012 07:00 p. m."
            // Apply replacements from MOST specific to LEAST specific to avoid partial matches
            string cleaned = s
                .Replace(" p. m.", " PM")
                .Replace(" a. m.", " AM")
                .Replace(" p.m.", " PM")
                .Replace(" a.m.", " AM")
                .Replace(" p.", " PM")
                .Replace(" a.", " AM");

            if (DateTime.TryParse(cleaned, ci, System.Globalization.DateTimeStyles.None, out DateTime d3)) return d3;

            // Last resort: try explicit format patterns with Spanish culture
            var formats = new[]
            {
                "dd MMMM yyyy HH:mm",
                "dd MMMM yyyy hh:mm tt",
                "d MMMM yyyy HH:mm",
                "d MMMM yyyy hh:mm tt",
                "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy",
                "MM/dd/yyyy",
            };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(cleaned, fmt, ci, System.Globalization.DateTimeStyles.None, out DateTime d4)) return d4;
                if (DateTime.TryParseExact(cleaned, fmt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime d5)) return d5;
            }

            Console.WriteLine($"[ParseExcelDate] No se pudo convertir: '{s}'");
            return null;
        }

        private async Task<List<EdtNode>> ParseXmlFileAsync(string filePath, string projectId)
        {
            return await Task.Run(() =>
            {
                var nodes = new List<EdtNode>();
                var doc = XDocument.Load(filePath);

                // MS Project XML usually defines a default namespace
                XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                var tasksElem = doc.Descendants(ns + "Tasks").FirstOrDefault();
                if (tasksElem == null)
                    return nodes;

                foreach (var taskElem in tasksElem.Elements(ns + "Task"))
                {
                    // Skip root project summary task (UID 0) if it's there
                    string? uidStr = taskElem.Element(ns + "UID")?.Value;
                    if (uidStr == "0") continue;

                    string name = taskElem.Element(ns + "Name")?.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string wbs = taskElem.Element(ns + "WBS")?.Value ?? string.Empty;

                    DateTime? start = null;
                    if (DateTime.TryParse(taskElem.Element(ns + "Start")?.Value, out DateTime parsedStart))
                    {
                        start = parsedStart;
                    }

                    DateTime? finish = null;
                    if (DateTime.TryParse(taskElem.Element(ns + "Finish")?.Value, out DateTime parsedFinish))
                    {
                        finish = parsedFinish;
                    }

                    string notes = taskElem.Element(ns + "Notes")?.Value ?? string.Empty;

                    int? uid = null;
                    if (int.TryParse(uidStr, out int parsedUid))
                    {
                        uid = parsedUid;
                    }

                    var node = new EdtNode
                    {
                        ProjectId = projectId,
                        Name = name,
                        Ie = uid,
                        HierarchyCode = wbs,
                        ImportInicio = start,
                        ImportFin = finish,
                        ImportNotas = notes,
                        Children = new System.Collections.ObjectModel.ObservableCollection<EdtNode>()
                    };

                    nodes.Add(node);
                }

                // Link hierarchy
                BuildHierarchyFromWbs(nodes);

                return nodes;
            });
        }

        private int GetColumnIndex(DataTable table, params string[] possibleNames)
        {
            for (int i = 0; i < table.Columns.Count; i++)
            {
                string colName = table.Columns[i].ColumnName?.Trim() ?? "";
                if (possibleNames.Any(n =>
                    colName.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                    colName.StartsWith(n, StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith(colName, StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }
            return -1;
        }

        private void BuildHierarchyFromWbs(List<EdtNode> nodes)
        {
            var nodesByWbs = nodes
                .Where(n => !string.IsNullOrEmpty(n.HierarchyCode))
                .ToDictionary(n => n.HierarchyCode);

            foreach (var node in nodes)
            {
                if (string.IsNullOrEmpty(node.HierarchyCode)) continue;

                // Typical WBS format: 1.2.3 -> Parent is 1.2
                int lastDotIdx = node.HierarchyCode.LastIndexOf('.');
                if (lastDotIdx > 0)
                {
                    string parentWbs = node.HierarchyCode.Substring(0, lastDotIdx);
                    if (nodesByWbs.TryGetValue(parentWbs, out var parentNode))
                    {
                        // We cannot assign ObjectIds directly here since they might not be in the DB yet.
                        // We will rely on the UI/ViewModel or the DB Insertion process to map them or
                        // we can generate ObjectIds for all nodes before insertion.

                        // For a pure memory tree, we just use arbitrary temporary IDs if not set
                        if (string.IsNullOrEmpty(node.Id)) node.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                        if (string.IsNullOrEmpty(parentNode.Id)) parentNode.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

                        node.ParentId = parentNode.Id;
                        parentNode.Children.Add(node);
                    }
                }
                else
                {
                    // Root node
                    if (string.IsNullOrEmpty(node.Id)) node.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                    node.ParentId = null;
                }
            }
        }
    }
}
