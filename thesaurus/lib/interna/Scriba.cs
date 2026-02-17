using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    internal class Scriba : IScriba {
        // pathにcontentを書き込む。
        public async Task Scribere(string path, string content) {
            string directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        }

        // pathの内容を読み込んでstringを返す。
        public async Task<string> Legere(string path) {
            if(!File.Exists(path)) {
                throw new FileNotFoundException($"File not found: {path}");
            }

            return await File.ReadAllTextAsync(path, Encoding.UTF8);
        }
    }
}