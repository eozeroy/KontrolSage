using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    /// <summary>
    /// Representa un registro de avance físico de una actividad (EDT hoja).
    /// Este modelo es APPEND-ONLY: nunca se actualiza ni elimina.
    /// El avance vigente es siempre el último registro por FechaRegistro DESC.
    /// </summary>
    public class RegistroAvance
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("projectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>Nodo hoja del EDT al que corresponde este avance.</summary>
        [BsonElement("edtNodeId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EdtNodeId { get; set; } = string.Empty;

        /// <summary>Baseline al que pertenece la actividad (0 = LBo, 1 = LBa1, ...).</summary>
        [BsonElement("baseline")]
        public int Baseline { get; set; } = 0;

        /// <summary>
        /// Fecha y hora UTC del registro. Se asigna automáticamente al guardar.
        /// No se expone a edición del usuario.
        /// </summary>
        [BsonElement("fechaRegistro")]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Porcentaje de avance ACUMULADO al momento del registro.
        /// Rango: 0.0 (0%) a 1.0 (100%).
        /// </summary>
        [BsonElement("porcentajeAcumulado")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal PorcentajeAcumulado { get; set; } = 0m;

        /// <summary>Observaciones opcionales del inspector o responsable.</summary>
        [BsonElement("notas")]
        public string Notas { get; set; } = string.Empty;
    }
}
