using KontrolSage.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KontrolSage.Services
{
    public interface IEdtImportService
    {
        Task<List<EdtNode>> ParseImportFileAsync(string filePath, string projectId);
    }
}
