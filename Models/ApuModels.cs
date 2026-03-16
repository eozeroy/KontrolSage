using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KontrolSage.Models
{
    public enum TipoInsumo
    {
        Material = 1,
        ManoDeObra = 2,
        MaquinariaEquipo = 3,
        HerramientaMenor = 4,
        AuxiliarMatriz = 5, // Sub-matrices (recursión) — solo para APU
        Flete = 6,
        Subcontrato = 7,
        Amortizacion = 8,
        NoDeducible = 9,
        Otro = 10
    }

    public class Categoria
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [BsonElement("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [BsonElement("parentId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentId { get; set; }
    }

    public class Insumo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Mapeo interoperabilidad Opus/Neodata
        [BsonElement("claveExterna")]
        public string ClaveExterna { get; set; } = string.Empty;

        [BsonElement("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [BsonElement("unidadMedida")]
        public string Unidad { get; set; } = string.Empty;

        [BsonElement("tipo")]
        [BsonRepresentation(BsonType.String)]
        public TipoInsumo Tipo { get; set; }

        [BsonElement("costoBase")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CostoBase { get; set; }

        [BsonElement("categoriaId")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonIgnoreIfDefault]
        public string? CategoriaId { get; set; }

        public override string ToString()
        {
            return Descripcion;
        }
    }

    /// <summary>
    /// Documento embebido para evitar el N+1 Query Problem en MongoDB
    /// Implementa INotifyPropertyChanged para actualizar la UI en vivo (DataGrid).
    /// </summary>
    public class ComposicionMatriz : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [BsonElement("insumoId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string InsumoId { get; set; } = string.Empty;

        // --- Desnormalización estratégica --- 
        [BsonElement("codigoInsumo")]
        public string CodigoInsumo { get; set; } = string.Empty;

        [BsonElement("tipo")]
        [BsonRepresentation(BsonType.String)]
        public TipoInsumo Tipo { get; set; }

        private decimal _cantidad;
        [BsonElement("cantidad")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Cantidad
        {
            get => _cantidad;
            set
            {
                if (_cantidad != value)
                {
                    _cantidad = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ImporteDirecto));
                }
            }
        }

        private decimal _rendimiento = 1m;
        [BsonElement("rendimiento")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Rendimiento
        {
            get => _rendimiento;
            set
            {
                if (_rendimiento != value)
                {
                    _rendimiento = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ImporteDirecto));
                }
            }
        }

        [BsonElement("costoUnitarioSnapshot")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CostoUnitarioSnapshot { get; set; } // Valor denormalizado

        // Propiedad calculada, no es necesario guardarla en Mongo
        [BsonIgnore]
        public decimal ImporteDirecto => (Cantidad / (Rendimiento == 0 ? 1m : Rendimiento)) * CostoUnitarioSnapshot;
    }

    public class MatrizAPU
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("codigoInterno")]
        public string CodigoInterno { get; set; } = string.Empty;

        [BsonElement("descripcionConcepto")]
        public string DescripcionConcepto { get; set; } = string.Empty;

        [BsonElement("unidadAnalisis")]
        public string UnidadAnalisis { get; set; } = string.Empty;

        // Aquí radica la solución NoSQL, incluímos como subdocumentos (Array)
        [BsonElement("composicion")]
        public List<ComposicionMatriz> Composicion { get; set; } = new();

        [BsonElement("costoDirectoTotal")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CostoDirectoTotal { get; set; }

        // Metadato Opus/Neodata Importaciones
        [BsonElement("metadataOrigen")]
        public string MetadataOrigen { get; set; } = "CONTROLO_NATIVO"; // O "NEODATA_2023", "OPUS_24"
    }
}
