using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface IScriba {
        Task Scribere(string path, string content);
        Task<string> Legere(string path);
    }
}