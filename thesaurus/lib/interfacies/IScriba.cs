using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface IScriba {
        Task Scribere(string path, string content);
        Task<string> Legere(string path);
        string ScribereSync(string path, string content);
        string LegereSync(string path);
    }
}