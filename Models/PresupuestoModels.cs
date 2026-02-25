using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    public enum TipoConceptoPresupuesto
    {
        Alzado,
        Unitario,
        Adicional,
        Autorizado,
        SinAutorizar
    }

    public enum TipoDistribucion
    {
        Lineal,
        Campana
    }

    public enum TipoRecursoPresupuesto
    {
        Insumo,
        Matriz
    }

    public class RecursoAsignado : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        [BsonRepresentation(BsonType.ObjectId)]
        public string CatalogoItemId { get; set; } = string.Empty;

        public TipoRecursoPresupuesto TipoRecurso { get; set; }
        
        // Metadata from catalog at time of assignment or updated later
        public string CodigoRecurso { get; set; } = string.Empty;
        public string DescripcionRecurso { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;

        private decimal _rendimiento = 1m;
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Rendimiento 
        { 
            get => _rendimiento; 
            set 
            { 
                 if (_rendimiento != value) {
                     _rendimiento = value;
                     OnPropertyChanged();
                     OnPropertyChanged(nameof(ImporteTotal));
                 }
            } 
        }
        
        private decimal _cantidadTotalFormulada = 0m;
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CantidadTotalFormulada 
        { 
            get => _cantidadTotalFormulada; 
            set 
            { 
                 if (_cantidadTotalFormulada != value) {
                     _cantidadTotalFormulada = value;
                     OnPropertyChanged();
                     OnPropertyChanged(nameof(ImporteTotal));
                 }
            } 
        }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CostoUnitarioSnapshot { get; set; } = 0m;

        [BsonIgnore]
        public decimal ImporteTotal => CantidadTotalFormulada * CostoUnitarioSnapshot;
    }

    public class ActividadPresupuesto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("projectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        [BsonElement("edtNodeId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EdtNodeId { get; set; } = string.Empty;

        [BsonElement("baseline")]
        public int Baseline { get; set; } = 0; // 0 = LBo, 1 = LBa1, etc.

        [BsonElement("isFrozen")]
        public bool IsFrozen { get; set; } = false;

        [BsonElement("inicio")]
        public DateTime? Inicio { get; set; }

        [BsonElement("fin")]
        public DateTime? Fin { get; set; }

        [BsonElement("cantidad")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Cantidad { get; set; } = 0m;

        [BsonElement("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [BsonElement("tipoConcepto")]
        [BsonRepresentation(BsonType.String)]
        public TipoConceptoPresupuesto TipoConcepto { get; set; } = TipoConceptoPresupuesto.Unitario;

        [BsonElement("distribucion")]
        [BsonRepresentation(BsonType.String)]
        public TipoDistribucion Distribucion { get; set; } = TipoDistribucion.Lineal;

        [BsonElement("notas")]
        public string Notas { get; set; } = string.Empty;

        [BsonElement("recursosAsignados")]
        public List<RecursoAsignado> RecursosAsignados { get; set; } = new();

        [BsonIgnore]
        public decimal CostoDirectoTotal 
        {
            get
            {
                decimal total = 0;
                if (RecursosAsignados != null)
                {
                    foreach (var rec in RecursosAsignados)
                        total += rec.ImporteTotal;
                }
                return total;
            }
        }
    }
}
