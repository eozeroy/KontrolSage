using CommunityToolkit.Mvvm.ComponentModel;

namespace KontrolSage.ViewModels
{
    /// <summary>
    /// Representa una línea de mapeo en el diálogo:
    /// "el campo X del modelo se leerá de la columna Y del Excel"
    /// </summary>
    public partial class ColumnMappingEntry : ObservableObject
    {
        /// <summary>Nombre interno de la propiedad del modelo (e.g. "Descripcion")</summary>
        public string FieldName { get; init; } = string.Empty;

        /// <summary>Etiqueta amigable mostrada al usuario (e.g. "Descripción")</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Si es true, el botón Importar no se habilitará si esta entrada queda en "— Ignorar —"</summary>
        public bool IsRequired { get; init; }

        /// <summary>Header del Excel seleccionado por el usuario. null = Ignorar esta columna.</summary>
        [ObservableProperty]
        private string? _selectedHeader;
    }
}
