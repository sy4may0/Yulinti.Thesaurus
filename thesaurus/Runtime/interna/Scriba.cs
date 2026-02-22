using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Yulinti.Thesaurus {
    internal class Scriba : IScriba {
        public async Task Scribere(string path, string content, CancellationToken ct = default)
        {
            path = Path.GetFullPath(path);
            string tempPath = path + ".tmp";

            try
            {
                string? directoryPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, ct).ConfigureAwait(false);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            finally {
                //tmpを掃除
                try {if (File.Exists(tempPath)) File.Delete(tempPath);} catch {}
            }
        }
    
        public async Task<string> Legere(string path, CancellationToken ct = default)
        {
            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            return await File.ReadAllTextAsync(path, Encoding.UTF8, ct).ConfigureAwait(false);
        }
    
        public void ScribereSync(string path, string content, CancellationToken ct = default)
        {
            path = Path.GetFullPath(path);
            string tempPath = path + ".tmp";

            try
            {
                ct.ThrowIfCancellationRequested();

                string? directoryPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                ct.ThrowIfCancellationRequested();
                File.WriteAllText(tempPath, content, Encoding.UTF8);
                ct.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            finally {
                //tmpを掛除
                try {if (File.Exists(tempPath)) File.Delete(tempPath);} catch {}
            }
        }
    
        public string LegereSync(string path, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            return File.ReadAllText(path, Encoding.UTF8);
        }

        public async Task Scribere(string[] paths, string[] contents, CancellationToken ct = default)
        {
            if (paths.Length != contents.Length)
                throw new ArgumentException("paths and contents must have the same length");

            string[] fullPaths = new string[paths.Length];
            string[] tempPaths = new string[paths.Length];

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    fullPaths[i] = Path.GetFullPath(paths[i]);
                    tempPaths[i] = fullPaths[i] + ".tmp";

                    string? directoryPath = Path.GetDirectoryName(fullPaths[i]);
                    if (!string.IsNullOrEmpty(directoryPath))
                        Directory.CreateDirectory(directoryPath);

                    await File.WriteAllTextAsync(tempPaths[i], contents[i], Encoding.UTF8, ct).ConfigureAwait(false);
                }

                for (int i = 0; i < fullPaths.Length; i++)
                {
                    if (File.Exists(fullPaths[i])) File.Delete(fullPaths[i]);
                    File.Move(tempPaths[i], fullPaths[i]);
                }
            }
            finally {
                //tmpを掃除
                foreach (var tempPath in tempPaths) {
                    try {
                        if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) File.Delete(tempPath);
                    } catch {}
                }
            }
        }

        public async Task<string[]> Legere(string[] paths, CancellationToken ct = default)
        {
            string[] results = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                results[i] = await Legere(paths[i], ct).ConfigureAwait(false);
            }
            return results;
        }

        public void ScribereSync(string[] paths, string[] contents, CancellationToken ct = default)
        {
            if (paths.Length != contents.Length)
                throw new ArgumentException("paths and contents must have the same length");

            string[] fullPaths = new string[paths.Length];
            string[] tempPaths = new string[paths.Length];

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    fullPaths[i] = Path.GetFullPath(paths[i]);
                    tempPaths[i] = fullPaths[i] + ".tmp";

                    string? directoryPath = Path.GetDirectoryName(fullPaths[i]);
                    if (!string.IsNullOrEmpty(directoryPath))
                        Directory.CreateDirectory(directoryPath);

                    File.WriteAllText(tempPaths[i], contents[i], Encoding.UTF8);
                }

                for (int i = 0; i < fullPaths.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (File.Exists(fullPaths[i])) File.Delete(fullPaths[i]);
                    File.Move(tempPaths[i], fullPaths[i]);
                }
            }
            finally {
                //tmpを掃除
                foreach (var tempPath in tempPaths) {
                    try {
                        if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) File.Delete(tempPath);
                    } catch {}
                }
            }
        }

        public string[] LegereSync(string[] paths, CancellationToken ct = default)
        {
            string[] results = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                results[i] = LegereSync(paths[i], ct);
            }
            return results;
        }
    }
}