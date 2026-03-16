using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace KontrolSage.ViewModels
{
    public enum TipoImportacion
    {
        Insumos,
        MatricesAPU
    }

    public partial class ImportColumnMappingViewModel : ViewModelBase
    {
        // ── Constante especial para "ignorar esta columna" ──────────────────────
        public const string EncabezadoIgnorar = "— Ignorar —";

        // ── Datos del Excel ─────────────────────────────────────────────────────
        private readonly ExcelParseResult _parseResult;

        /// <summary>Todos los headers del Excel más la opción de ignorar, para los ComboBoxes.</summary>
        public List<string> ExcelHeadersConIgnorar { get; }

        /// <summary>Primeras 3 filas del Excel para mostrar en el preview.</summary>
        public List<string[]> PreviewRows { get; }

        /// <summary>Headers del Excel (para columnas del DataGrid de preview).</summary>
        public List<string> ExcelHeaders { get; }

        public int TotalRows => _parseResult.TotalRows;

        // ── Tipo de importación ─────────────────────────────────────────────────
        public TipoImportacion TipoImportacion { get; }
        public string Titulo => TipoImportacion == TipoImportacion.Insumos
            ? "📥 Importar Insumos desde Excel"
            : "📥 Importar Matrices APU desde Excel";

        // ── Mapeos ──────────────────────────────────────────────────────────────
        public ObservableCollection<ColumnMappingEntry> Mappings { get; } = new();

        // ── Resultado ───────────────────────────────────────────────────────────
        /// <summary>Insumos construidos después de confirmar el mapeo (null si se canceló).</summary>
        public List<Insumo>? InsumosImportados { get; private set; }

        /// <summary>Matrices construidas después de confirmar el mapeo (null si se canceló).</summary>
        public List<MatrizAPU>? MatricesImportadas { get; private set; }

        public bool Confirmado { get; private set; }

        // ── Callback para cerrar la ventana (se asigna desde el code-behind) ────
        private Action? _cerrarWindowAction;

        // ── Observable Properties ───────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmarImportCommand))]
        private bool _puedeImportar;

        [ObservableProperty] private string _mensajeValidacion = string.Empty;

        // ────────────────────────────────────────────────────────────────────────
        public ImportColumnMappingViewModel(
            ExcelParseResult parseResult,
            TipoImportacion tipo)
        {
            _parseResult = parseResult;
            TipoImportacion = tipo;

            ExcelHeaders = parseResult.Headers;
            ExcelHeadersConIgnorar = new List<string> { EncabezadoIgnorar };
            ExcelHeadersConIgnorar.AddRange(parseResult.Headers);

            PreviewRows = parseResult.Rows.Take(3).ToList();

            // Construir definiciones de campos según el tipo
            BuildMappings();

            // Intentar auto-mapear campos con nombres similares
            AutoMap();

            // Suscribirse a cambios de los ComboBoxes
            foreach (var entry in Mappings)
                entry.PropertyChanged += (_, _) => RecalcularValidacion();

            RecalcularValidacion();
        }

        // ── Auto-mapeo heurístico ────────────────────────────────────────────────
        private void AutoMap()
        {
            foreach (var entry in Mappings)
            {
                // Buscar header que contenga el nombre del campo (case-insensitive, ignora acentos)
                var matched = ExcelHeaders.FirstOrDefault(h =>
                    NormalizeStr(h).Contains(NormalizeStr(entry.FieldName)) ||
                    NormalizeStr(entry.DisplayName).Contains(NormalizeStr(h)) ||
                    NormalizeStr(h).Contains(NormalizeStr(entry.DisplayName)));

                if (matched != null)
                    entry.SelectedHeader = matched;
                else
                    entry.SelectedHeader = EncabezadoIgnorar;
            }
        }

        private static string NormalizeStr(string s) =>
            new string(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray()).ToLowerInvariant();

        // ── Definición de campos por tipo ────────────────────────────────────────
        private void BuildMappings()
        {
            if (TipoImportacion == TipoImportacion.Insumos)
            {
                Mappings.Add(new ColumnMappingEntry { FieldName = "ClaveExterna",  DisplayName = "Código / Clave",   IsRequired = true });
                Mappings.Add(new ColumnMappingEntry { FieldName = "Descripcion",   DisplayName = "Descripción",      IsRequired = true });
                Mappings.Add(new ColumnMappingEntry { FieldName = "Unidad",        DisplayName = "Unidad de Medida", IsRequired = true });
                Mappings.Add(new ColumnMappingEntry { FieldName = "Tipo",          DisplayName = "Tipo Insumo",      IsRequired = true });
                Mappings.Add(new ColumnMappingEntry { FieldName = "CostoBase",     DisplayName = "Costo Base",       IsRequired = true });
                Mappings.Add(new ColumnMappingEntry { FieldName = "CategoriaId",   DisplayName = "Categoría ID",     IsRequired = false });
            }
            else
            {
                Mappings.Add(new ColumnMappingEntry { FieldName = "CodigoInterno",       DisplayName = "Código Interno", IsRequired = true  });
                Mappings.Add(new ColumnMappingEntry { FieldName = "DescripcionConcepto", DisplayName = "Descripción",    IsRequired = true  });
                Mappings.Add(new ColumnMappingEntry { FieldName = "UnidadAnalisis",      DisplayName = "Unidad",         IsRequired = true  });
                Mappings.Add(new ColumnMappingEntry { FieldName = "CostoDirectoTotal",   DisplayName = "Costo Directo",  IsRequired = false });
                Mappings.Add(new ColumnMappingEntry { FieldName = "MetadataOrigen",      DisplayName = "Origen",         IsRequired = false });
            }
        }

        // ── Validación ───────────────────────────────────────────────────────────
        private void RecalcularValidacion()
        {
            var requeridosSinMapear = Mappings
                .Where(m => m.IsRequired &&
                            (m.SelectedHeader == null || m.SelectedHeader == EncabezadoIgnorar))
                .Select(m => m.DisplayName)
                .ToList();

            if (requeridosSinMapear.Count > 0)
            {
                MensajeValidacion = $"Campos requeridos sin asignar: {string.Join(", ", requeridosSinMapear)}";
                PuedeImportar = false;
            }
            else
            {
                MensajeValidacion = $"✅ Todo listo — se importarán {TotalRows} registros.";
                PuedeImportar = true;
            }
        }

        // ── Comandos ─────────────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(PuedeImportar))]
        private void ConfirmarImport()
        {
            if (TipoImportacion == TipoImportacion.Insumos)
                InsumosImportados = BuildInsumos();
            else
                MatricesImportadas = BuildMatrices();

            Confirmado = true;
            _cerrarWindowAction?.Invoke();
        }

        [RelayCommand]
        private void Cancelar() => _cerrarWindowAction?.Invoke();

        /// <summary>Registrado por el code-behind para que el comando pueda cerrar la Window.</summary>
        public void SetCloseAction(Action closeWindow) => _cerrarWindowAction = closeWindow;

        // ── Construcción de objetos ───────────────────────────────────────────────
        private List<Insumo> BuildInsumos()
        {
            var result = new List<Insumo>();
            var indexMap = BuildIndexMap();

            foreach (var row in _parseResult.Rows)
            {
                var insumo = new Insumo();

                if (indexMap.TryGetValue("ClaveExterna", out int iClave) && iClave >= 0)
                    insumo.ClaveExterna = SafeGet(row, iClave);

                if (indexMap.TryGetValue("Descripcion", out int iDesc) && iDesc >= 0)
                    insumo.Descripcion = SafeGet(row, iDesc);

                if (indexMap.TryGetValue("Unidad", out int iUnidad) && iUnidad >= 0)
                    insumo.Unidad = SafeGet(row, iUnidad);

                if (indexMap.TryGetValue("CostoBase", out int iCosto) && iCosto >= 0)
                {
                    var raw = SafeGet(row, iCosto);
                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal costo))
                        insumo.CostoBase = costo;
                }

                if (indexMap.TryGetValue("Tipo", out int iTipo) && iTipo >= 0)
                    insumo.Tipo = ParseTipoInsumo(SafeGet(row, iTipo));

                if (indexMap.TryGetValue("CategoriaId", out int iCat) && iCat >= 0)
                {
                    var catId = SafeGet(row, iCat);
                    if (!string.IsNullOrWhiteSpace(catId)) insumo.CategoriaId = catId;
                }

                result.Add(insumo);
            }
            return result;
        }

        private List<MatrizAPU> BuildMatrices()
        {
            var result = new List<MatrizAPU>();
            var indexMap = BuildIndexMap();

            foreach (var row in _parseResult.Rows)
            {
                var m = new MatrizAPU();

                if (indexMap.TryGetValue("CodigoInterno", out int iCod) && iCod >= 0)
                    m.CodigoInterno = SafeGet(row, iCod);

                if (indexMap.TryGetValue("DescripcionConcepto", out int iDesc) && iDesc >= 0)
                    m.DescripcionConcepto = SafeGet(row, iDesc);

                if (indexMap.TryGetValue("UnidadAnalisis", out int iUnidad) && iUnidad >= 0)
                    m.UnidadAnalisis = SafeGet(row, iUnidad);

                if (indexMap.TryGetValue("CostoDirectoTotal", out int iCosto) && iCosto >= 0)
                {
                    var raw = SafeGet(row, iCosto);
                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal costo))
                        m.CostoDirectoTotal = costo;
                }

                if (indexMap.TryGetValue("MetadataOrigen", out int iOrigen) && iOrigen >= 0)
                {
                    var origen = SafeGet(row, iOrigen);
                    if (!string.IsNullOrWhiteSpace(origen)) m.MetadataOrigen = origen;
                }

                result.Add(m);
            }
            return result;
        }

        /// Construye un diccionario FieldName → índice de columna en el Excel (-1 = ignorar).
        private Dictionary<string, int> BuildIndexMap()
        {
            var map = new Dictionary<string, int>();
            foreach (var entry in Mappings)
            {
                if (entry.SelectedHeader == null || entry.SelectedHeader == EncabezadoIgnorar)
                {
                    map[entry.FieldName] = -1;
                }
                else
                {
                    map[entry.FieldName] = ExcelHeaders.IndexOf(entry.SelectedHeader);
                }
            }
            return map;
        }

        private static string SafeGet(string[] row, int index) =>
            (index >= 0 && index < row.Length) ? row[index].Trim() : string.Empty;

        /// Parsea el tipo de insumo desde texto o número.
        private static TipoInsumo ParseTipoInsumo(string value)
        {
            // Intentar por nombre (e.g. "Material", "ManoDeObra")
            if (Enum.TryParse<TipoInsumo>(value.Replace(" ", ""), ignoreCase: true, out var byName))
                return byName;

            // Intentar por número
            if (int.TryParse(value, out int num) && Enum.IsDefined(typeof(TipoInsumo), num))
                return (TipoInsumo)num;

            // Alias comunes en español
            return value.ToLowerInvariant() switch
            {
                "material" or "mat"              => TipoInsumo.Material,
                "mano de obra" or "mdo" or "mo"  => TipoInsumo.ManoDeObra,
                "maquinaria" or "equipo" or "maq" => TipoInsumo.MaquinariaEquipo,
                "herramienta" or "herr"          => TipoInsumo.HerramientaMenor,
                "flete"                          => TipoInsumo.Flete,
                "subcontrato" or "sub"           => TipoInsumo.Subcontrato,
                "amortizacion" or "amort"        => TipoInsumo.Amortizacion,
                "no deducible" or "nodeducible"  => TipoInsumo.NoDeducible,
                _                                => TipoInsumo.Material // default
            };
        }
    }
}
