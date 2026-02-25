using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    public enum EstadoCostoReal
    {
        Borrador,
        Aprobado,
        Reclasificado
    }

    /// <summary>
    /// Registro de un gasto real incurrido en el proyecto.
    /// Se vincula a una cuenta hoja de la EDC.
    /// Soporta captura de IVA por separado y clasificación por TipoInsumo.
    /// </summary>
    public class CostoReal
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("projectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>Cuenta hoja de la EDC a la que se carga el gasto.</summary>
        [BsonElement("edcNodeId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EdcNodeId { get; set; } = string.Empty;

        /// <summary>Fecha del documento (factura, estimación, nómina…).</summary>
        [BsonElement("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        // ── Montos ──────────────────────────────────────────────────────────

        /// <summary>Importe del gasto sin IVA. OBLIGATORIO.</summary>
        [BsonElement("importe")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Importe { get; set; } = 0m;

        /// <summary>Monto de IVA correspondiente al documento.</summary>
        [BsonElement("iva")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal IVA { get; set; } = 0m;

        /// <summary>Importe total = Importe + IVA. Calculado, no almacenado.</summary>
        [BsonIgnore]
        public decimal ImporteTotal => Importe + IVA;

        // ── Clasificación ────────────────────────────────────────────────────

        /// <summary>Tipo de recurso. Comparte el enum TipoInsumo del módulo APU.</summary>
        [BsonElement("tipoRecurso")]
        [BsonRepresentation(BsonType.String)]
        public TipoInsumo TipoRecurso { get; set; } = TipoInsumo.Material;

        /// <summary>Estado del registro: Borrador → Aprobado → Reclasificado.</summary>
        [BsonElement("estado")]
        [BsonRepresentation(BsonType.String)]
        public EstadoCostoReal Estado { get; set; } = EstadoCostoReal.Borrador;

        // ── Referencias documentales (opcionales) ────────────────────────────

        /// <summary>Número de póliza contable.</summary>
        [BsonElement("numeroPoliza")]
        public string NumeroPoliza { get; set; } = string.Empty;

        /// <summary>Número de factura / folio CFDI.</summary>
        [BsonElement("numeroFactura")]
        public string NumeroFactura { get; set; } = string.Empty;

        /// <summary>Nombre del proveedor o contratista.</summary>
        [BsonElement("proveedor")]
        public string Proveedor { get; set; } = string.Empty;

        /// <summary>RFC del proveedor para conciliación fiscal.</summary>
        [BsonElement("rfc")]
        public string RFC { get; set; } = string.Empty;

        // ── Detalles del recurso (opcionales) ────────────────────────────────

        /// <summary>Descripción del concepto de gasto.</summary>
        [BsonElement("concepto")]
        public string Concepto { get; set; } = string.Empty;

        /// <summary>Cantidad del recurso (para calcular precio unitario real).</summary>
        [BsonElement("cantidad")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Cantidad { get; set; } = 0m;

        /// <summary>Unidad de medida del recurso (m³, hr, kg…).</summary>
        [BsonElement("unidad")]
        public string Unidad { get; set; } = string.Empty;

        /// <summary>Precio unitario real calculado. Solo disponible si Cantidad > 0.</summary>
        [BsonIgnore]
        public decimal PrecioUnitarioReal => Cantidad > 0 ? Importe / Cantidad : 0m;

        [BsonElement("notas")]
        public string Notas { get; set; } = string.Empty;
    }
}
