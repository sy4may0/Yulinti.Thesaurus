using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    public interface IScriba {
        Task Scribere(string path, string content, int tempusPraeteriit = -1);
        Task<string> Legere(string path, int tempusPraeteriit = -1);
        void ScribereSync(string path, string content, int tempusPraeteriit = -1);
        string LegereSync(string path, int tempusPraeteriit = -1);
    }
}