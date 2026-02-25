using System.Collections.Generic;
using System.Threading.Tasks;
using KontrolSage.Models;

namespace KontrolSage.Services
{
    public interface IPriceCatalogService
    {
        // CRUD Básico Insumos
        // CRUD Básico Insumos
        Task CrearInsumoAsync(Insumo insumo);
        Task ActualizarInsumoAsync(Insumo insumo);
        Task EliminarInsumoAsync(string id);
        Task<Insumo> ObtenerInsumoPorIdAsync(string id);
        
        // CRUD Básico Matrices
        Task EliminarMatrizAsync(string id);
        
        // Búsquedas reactivas para el Autocomplete / Grid
        Task<IEnumerable<Insumo>> BuscarInsumosAsync(string patron, TipoInsumo? tipoFilter = null);
        Task<IEnumerable<MatrizAPU>> BuscarMatricesGlobalesAsync(string querySearch, int limit = 50);
        
        // Acciones complejas
        Task<MatrizAPU> ObtenerMatrizListaAsync(string id);
        Task GuardarGeneracionMatrizAsync(MatrizAPU matriz);
        Task ImpactarAumentoPreciosCatalogoMaestro(string insumoId, decimal nuevoPrecio);
    }
}
