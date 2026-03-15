namespace KontrolSage.Services
{
    /// <summary>
    /// Configuración centralizada de MongoDB.
    /// Cambia aquí la cadena de conexión y el nombre de la base de datos
    /// para que todos los servicios lo usen automáticamente.
    /// </summary>
    public static class DatabaseConfig
    {
        // public static string ConnectionString { get; set; } = "mongodb+srv://eozeroy_db_user:h5xMylQp3ySZ5Nn1@cluster0.fqs0rf6.mongodb.net/";
        public static string ConnectionString { get; set; } = "mongodb://localhost:27017/";
        public static string DatabaseName { get; set; } = "KontrolSageDB";
    }
}
