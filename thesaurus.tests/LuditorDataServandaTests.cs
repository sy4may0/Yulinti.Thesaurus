using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Yulinti.Thesaurus;

public class LuditorDataServandaTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "thesaurus-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for test runs.
            }
        }
    }

    private sealed class TrackingScriba : IScriba
    {
        public int ScribereCount { get; private set; }
        public int LegereCount { get; private set; }
        public int ScribereSyncCount { get; private set; }
        public int LegereSyncCount { get; private set; }

        public async Task Scribere(string path, string content, int tempusPraeteriit = -1)
        {
            ScribereCount++;
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        }

        public async Task<string> Legere(string path, int tempusPraeteriit = -1)
        {
            LegereCount++;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return await File.ReadAllTextAsync(path, Encoding.UTF8);
        }

        public void ScribereSync(string path, string content, int tempusPraeteriit = -1)
        {
            ScribereSyncCount++;
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        public string LegereSync(string path, int tempusPraeteriit = -1)
        {
            LegereSyncCount++;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }

    private sealed class TestDto
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private static TestDto MakeDto(int i) => new TestDto { Name = $"name-{i}", Value = i };

    [Fact]
    public void Fabrica_Creare_CreatesIndexAndReturnsInstance()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path);

        Assert.NotNull(sut);
        string indexPath = System.IO.Path.Combine(temp.Path, "index.json");
        Assert.True(File.Exists(indexPath));

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(indexPath, Encoding.UTF8));
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("revisio_proximus").GetInt64());
        Assert.Equal(0, root.GetProperty("versio").GetInt32());
        Assert.False(root.GetProperty("manualis").EnumerateObject().Any());
        Assert.Equal(0, root.GetProperty("ordo_manualis").GetArrayLength());
        Assert.False(root.GetProperty("automaticus").EnumerateObject().Any());
        Assert.Equal(0, root.GetProperty("ordo_automaticus").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("novissimus").ValueKind);
    }

    [Fact]
    public void Fabrica_Creare_UsesProvidedScriba()
    {
        using var temp = new TempDir();
        var tracking = new TrackingScriba();

        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path, tracking);

        Assert.NotNull(sut);
        Assert.True(tracking.ScribereSyncCount > 0);
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "index.json")));
    }

    [Fact]
    public async Task CreareManualis_UpdatesIndexAndLists()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path);

        DateTime start = DateTime.UtcNow;
        Guid guid = await sut.CreareManualis(MakeDto(1));
        DateTime end = DateTime.UtcNow;

        var manualis = await sut.TabulaManualis();
        var automaticus = await sut.TabulaAutomaticus();
        Guid? novissimus = await sut.LegoNovissimus();
        var data = await sut.Arcessere(guid);

        Assert.Single(manualis);
        Assert.Equal(guid, manualis[0]);
        Assert.Empty(automaticus);
        Assert.Equal(guid, novissimus);
        Assert.Equal(guid, data.Guid);
        Assert.Equal(0, data.Revisio);
        Assert.InRange(data.Timestamp, start.AddSeconds(-1), end.AddSeconds(1));
        Assert.Equal(1, data.Data.Value);
        Assert.Equal("name-1", data.Data.Name);
    }

    [Fact]
    public async Task CreareAutomatics_RotatesAndKeepsLatest()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path, longitudoAutomaticus: 2, tempusPraeteriitSec: 30);

        Guid guid1 = await sut.CreareAutomatics(MakeDto(1));
        Guid guid2 = await sut.CreareAutomatics(MakeDto(2));
        Guid guid3 = await sut.CreareAutomatics(MakeDto(3));

        var automaticus = await sut.TabulaAutomaticus();
        Assert.Equal(2, automaticus.Count);
        Assert.Equal(guid3, automaticus[0]);
        Assert.Equal(guid2, automaticus[1]);

        Guid? novissimus = await sut.LegoNovissimus();
        Assert.Equal(guid3, novissimus);

        Assert.DoesNotContain(guid1, automaticus);
        string removedPath = System.IO.Path.Combine(temp.Path, guid1.ToString() + ".json");
        Assert.False(File.Exists(removedPath));
    }

    [Fact]
    public async Task Servare_UpdatesRevisionTimestampAndOrder()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path);

        Guid guidA = await sut.CreareManualis(MakeDto(1));
        Guid guidB = await sut.CreareManualis(MakeDto(2));

        var before = await sut.Arcessere(guidA);
        await Task.Delay(10);

        await sut.Servare(guidA, MakeDto(99));
        var after = await sut.Arcessere(guidA);

        var manualis = await sut.TabulaManualis();

        Assert.Equal(guidA, manualis[0]);
        Assert.Equal(2, after.Revisio);
        Assert.True(after.Timestamp >= before.Timestamp);
        Assert.Equal(99, after.Data.Value);
        Assert.Equal("name-99", after.Data.Name);
        Assert.Contains(guidB, manualis);
    }

    [Fact]
    public async Task Deleto_RemovesEntryAndUpdatesNovissimus()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestDto>(temp.Path);

        Guid guidManual = await sut.CreareManualis(MakeDto(1));
        Guid guidAuto = await sut.CreareAutomatics(MakeDto(2));

        Guid? latest = await sut.LegoNovissimus();
        Assert.Equal(guidAuto, latest);

        await sut.Deleto(guidAuto);

        Guid? latestAfter = await sut.LegoNovissimus();
        Assert.Equal(guidManual, latestAfter);

        var automaticus = await sut.TabulaAutomaticus();
        Assert.Empty(automaticus);

        await Assert.ThrowsAsync<FileNotFoundException>(() => sut.Arcessere(guidAuto));
    }
}
