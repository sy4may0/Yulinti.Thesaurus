using System.Threading;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface IScriba {
        Task Scribere(string path, string content, CancellationToken ct = default);
        Task<string> Legere(string path, CancellationToken ct = default);
        void ScribereSync(string path, string content, CancellationToken ct = default);
        string LegereSync(string path, CancellationToken ct = default);
        Task Scribere(string[] paths, string[] contents, CancellationToken ct = default);
        Task<string[]> Legere(string[] paths, CancellationToken ct = default);
        void ScribereSync(string[] paths, string[] contents, CancellationToken ct = default);
        string[] LegereSync(string[] paths, CancellationToken ct = default);
    }
}