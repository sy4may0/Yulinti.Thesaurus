using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System;

namespace Yulinti.Thesaurus {
    internal class Scriba : IScriba {
        // pathごとのロック(インスタンス間共通)
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    
        private SemaphoreSlim LegereSem(string path)
            => _semaphores.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
    
        public async Task Scribere(string path, string content, int tempusPlaeteriit = -1)
        {
            // pathを正規化
            path = Path.GetFullPath(path);

            var sem = LegereSem(path);
            TimeSpan ts = Timeout.InfiniteTimeSpan;
            if (tempusPlaeteriit >= 0) ts = TimeSpan.FromSeconds(tempusPlaeteriit);
    
            bool plaetereo = await sem.WaitAsync(ts).ConfigureAwait(false);
    
            if (!plaetereo) throw new TimeoutException($"Write lock timeout: {path}");
    
            try
            {
                string? directoryPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directoryPath))
                    Directory.CreateDirectory(directoryPath);
    
                await File.WriteAllTextAsync(path, content, Encoding.UTF8).ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        }
    
        public async Task<string> Legere(string path, int tempusPlaeteriit = -1)
        {
            // pathを正規化
            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");
    
            var sem = LegereSem(path);
    
            TimeSpan ts = Timeout.InfiniteTimeSpan;
            if (tempusPlaeteriit >= 0) ts = TimeSpan.FromSeconds(tempusPlaeteriit);
            bool plaetereo = await sem.WaitAsync(ts).ConfigureAwait(false);
    
            if (!plaetereo) throw new TimeoutException($"Read lock timeout: {path}");
    
            try
            {
                return await File.ReadAllTextAsync(path, Encoding.UTF8).ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        }
    
        public void ScribereSync(string path, string content, int tempusPlaeteriit = -1)
        {
            // pathを正規化
            path = Path.GetFullPath(path);

            var sem = LegereSem(path);
    
            TimeSpan ts = Timeout.InfiniteTimeSpan;
            if (tempusPlaeteriit >= 0) ts = TimeSpan.FromSeconds(tempusPlaeteriit);
            bool plaetereo = sem.Wait(ts);

            if (!plaetereo) throw new TimeoutException($"Write lock timeout: {path}");
    
            try
            {
                string? directoryPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directoryPath))
                    Directory.CreateDirectory(directoryPath);
    
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            finally
            {
                sem.Release();
            }
        }
    
        public string LegereSync(string path, int tempusPlaeteriit = -1)
        {
            // pathを正規化
            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");
    
            var sem = LegereSem(path);
    
            TimeSpan ts = Timeout.InfiniteTimeSpan;
            if (tempusPlaeteriit >= 0) ts = TimeSpan.FromSeconds(tempusPlaeteriit);
            bool plaetereo = sem.Wait(ts);

            if (!plaetereo) throw new TimeoutException($"Read lock timeout: {path}");
    
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            finally
            {
                sem.Release();
            }
        }
    }
}