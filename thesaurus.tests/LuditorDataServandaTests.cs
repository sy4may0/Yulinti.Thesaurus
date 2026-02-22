using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Yulinti.Thesaurus;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public int ScribereCountMul { get; private set; }
        public int LegereCountMul { get; private set; } 
        public int ScribereSyncCountMul { get; private set; }
        public int LegereSyncCountMul { get; private set; }

        public async Task Scribere(string path, string content, CancellationToken ct = default)
        {
            ScribereCount++;
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        }

        public async Task<string> Legere(string path, CancellationToken ct = default)
        {
            LegereCount++;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return await File.ReadAllTextAsync(path, Encoding.UTF8);
        }

        public void ScribereSync(string path, string content, CancellationToken ct = default)
        {
            ScribereSyncCount++;
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        public string LegereSync(string path, CancellationToken ct = default)
        {
            LegereSyncCount++;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public async Task Scribere(string[] paths, string[] contents, CancellationToken ct = default)
        {
            ScribereCountMul++;
            for (int i = 0; i < paths.Length; i++)
            {
                await Scribere(paths[i], contents[i], ct);
            }
        }

        public async Task<string[]> Legere(string[] paths, CancellationToken ct = default)
        {
            LegereCountMul++;
            return await Task.WhenAll(paths.Select(p => Legere(p, ct))).ConfigureAwait(false);
        }

        public void ScribereSync(string[] paths, string[] contents, CancellationToken ct = default)
        {
            ScribereSyncCountMul++;
            for (int i = 0; i < paths.Length; i++)
            {
                ScribereSync(paths[i], contents[i], ct);
            }
        }

        public string[] LegereSync(string[] paths, CancellationToken ct = default)
        {
            LegereSyncCountMul++;
            return paths.Select(p => LegereSync(p, ct)).ToArray();
        }
    }

    private sealed class TestNotitiaDto
    {
        public string Title { get; set; } = string.Empty;
        public int Rank { get; set; }
    }

    private sealed class TestDataDto
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private sealed class IndexServandaDtoLike
    {
        [JsonProperty("revisio_proximus")]
        public long RevisioProximus { get; set; }

        [JsonProperty("versio")]
        public int Versio { get; set; }

        [JsonProperty("manualis")]
        public Dictionary<Guid, DataServandaDtoLike> Manualis { get; set; } = new Dictionary<Guid, DataServandaDtoLike>();

        [JsonProperty("ordo_manualis")]
        public List<Guid> OrdoManualis { get; set; } = new List<Guid>();

        [JsonProperty("automaticus")]
        public Dictionary<Guid, DataServandaDtoLike> Automaticus { get; set; } = new Dictionary<Guid, DataServandaDtoLike>();

        [JsonProperty("ordo_automaticus")]
        public List<Guid> OrdoAutomaticus { get; set; } = new List<Guid>();

        [JsonProperty("novissimus")]
        public NovissimusServandaDtoLike? Novissimus { get; set; }
    }

    private sealed class DataServandaDtoLike
    {
        [JsonProperty("revisio")]
        public long Revisio { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("path_notitia")]
        public string PathNotitia { get; set; } = string.Empty;
    }

    private sealed class NovissimusServandaDtoLike
    {
        [JsonProperty("methodus")]
        public string Methodus { get; set; } = string.Empty;

        [JsonProperty("guid")]
        public Guid Guid { get; set; }

        [JsonProperty("revisio")]
        public long Revisio { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    private static TestNotitiaDto MakeNotitia(int i) => new TestNotitiaDto { Title = $"title-{i}", Rank = i };
    private static TestDataDto MakeData(int i) => new TestDataDto { Name = $"name-{i}", Value = i };

    private static void WriteIndex(string dirPath, IndexServandaDtoLike dto)
    {
        string indexPath = System.IO.Path.Combine(dirPath, "index.json");
        string json = JsonConvert.SerializeObject(dto);
        File.WriteAllText(indexPath, json, Encoding.UTF8);
    }

    private static void WriteDataFile(string dirPath, string relativePath, object dto)
    {
        string fullPath = System.IO.Path.Combine(dirPath, relativePath);
        string? dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, JsonConvert.SerializeObject(dto), Encoding.UTF8);
    }

    [Fact]
    public void Fabrica_Creare_CreatesIndexAndReturnsInstance()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Assert.NotNull(sut);
        string indexPath = System.IO.Path.Combine(temp.Path, "index.json");
        Assert.True(File.Exists(indexPath));

        JObject root = JObject.Parse(File.ReadAllText(indexPath, Encoding.UTF8));
        Assert.Equal(0, (long)root["revisio_proximus"]);
        Assert.Equal(0, (int)root["versio"]);
        Assert.False(((JObject)root["manualis"]).Properties().Any());
        Assert.Equal(0, ((JArray)root["ordo_manualis"]).Count);
        Assert.False(((JObject)root["automaticus"]).Properties().Any());
        Assert.Equal(0, ((JArray)root["ordo_automaticus"]).Count);
        Assert.Equal(JTokenType.Null, root["novissimus"].Type);
    }

    [Fact]
    public void Fabrica_Creare_UsesProvidedScriba()
    {
        using var temp = new TempDir();
        var tracking = new TrackingScriba();

        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path, tracking);

        Assert.NotNull(sut);
        Assert.True(tracking.ScribereSyncCount > 0);
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "index.json")));
    }

    [Fact]
    public void Constructor_InitareServandaDto_InvalidJson_Throws()
    {
        using var temp = new TempDir();
        string indexPath = System.IO.Path.Combine(temp.Path, "index.json");
        File.WriteAllText(indexPath, "{ not json", Encoding.UTF8);

        Assert.Throws<JsonReaderException>(() => FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path));
    }

    [Fact]
    public async Task Normare_RemovesEntriesWithMissingFiles()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Guid guidManual = await sut.CreareManualis(MakeNotitia(1), MakeData(1));
        Guid guidAuto = await sut.CreareAutomaticus(MakeNotitia(2), MakeData(2));

        string manualPath = System.IO.Path.Combine(temp.Path, guidManual.ToString() + ".json");
        if (File.Exists(manualPath)) File.Delete(manualPath);

        var sutReloaded = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        var manualis = await sutReloaded.TabulaManualis();
        var automaticus = await sutReloaded.TabulaAutomaticus();

        Assert.Empty(manualis);
        Assert.Single(automaticus);
        Assert.Equal(guidAuto, automaticus[0]);
    }

    [Fact]
    public async Task Normare_RemovesEntriesWithMissingNotitia()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Guid guidManual = await sut.CreareManualis(MakeNotitia(1), MakeData(1));
        Guid guidAuto = await sut.CreareAutomaticus(MakeNotitia(2), MakeData(2));

        string manualNotitiaPath = System.IO.Path.Combine(temp.Path, guidManual.ToString() + "_n.json");
        if (File.Exists(manualNotitiaPath)) File.Delete(manualNotitiaPath);

        var sutReloaded = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        var manualis = await sutReloaded.TabulaManualis();
        var automaticus = await sutReloaded.TabulaAutomaticus();

        Assert.Empty(manualis);
        Assert.Single(automaticus);
        Assert.Equal(guidAuto, automaticus[0]);
    }

    [Fact]
    public async Task Normare_RemovesDuplicateGuid_KeepingNewestTimestamp()
    {
        using var temp = new TempDir();
        Guid guid = Guid.NewGuid();
        DateTime older = DateTime.UtcNow.AddMinutes(-10);
        DateTime newer = DateTime.UtcNow.AddMinutes(-5);

        var index = new IndexServandaDtoLike
        {
            RevisioProximus = 3,
            Versio = 0,
            Manualis = new Dictionary<Guid, DataServandaDtoLike>
            {
                [guid] = new DataServandaDtoLike { Revisio = 1, Timestamp = older, Path = "m.json", PathNotitia = "m_n.json" }
            },
            OrdoManualis = new List<Guid> { guid },
            Automaticus = new Dictionary<Guid, DataServandaDtoLike>
            {
                [guid] = new DataServandaDtoLike { Revisio = 2, Timestamp = newer, Path = "a.json", PathNotitia = "a_n.json" }
            },
            OrdoAutomaticus = new List<Guid> { guid },
            Novissimus = null
        };

        WriteIndex(temp.Path, index);
        WriteDataFile(temp.Path, "m.json", MakeData(1));
        WriteDataFile(temp.Path, "m_n.json", MakeNotitia(1));
        WriteDataFile(temp.Path, "a.json", MakeData(2));
        WriteDataFile(temp.Path, "a_n.json", MakeNotitia(2));

        var sutReloaded = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);
        var manualis = await sutReloaded.TabulaManualis();
        var automaticus = await sutReloaded.TabulaAutomaticus();

        Assert.Empty(manualis);
        Assert.Single(automaticus);
        Assert.Equal(guid, automaticus[0]);
        Assert.False(File.Exists(System.IO.Path.Combine(temp.Path, "m.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(temp.Path, "m_n.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "a.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "a_n.json")));
    }

    [Fact]
    public async Task Normare_RemovesDuplicateRevisio_KeepingNewestTimestamp()
    {
        using var temp = new TempDir();
        Guid guidManual = Guid.NewGuid();
        Guid guidAuto = Guid.NewGuid();
        DateTime older = DateTime.UtcNow.AddMinutes(-10);
        DateTime newer = DateTime.UtcNow.AddMinutes(-5);

        var index = new IndexServandaDtoLike
        {
            RevisioProximus = 3,
            Versio = 0,
            Manualis = new Dictionary<Guid, DataServandaDtoLike>
            {
                [guidManual] = new DataServandaDtoLike { Revisio = 1, Timestamp = older, Path = "manual.json", PathNotitia = "manual_n.json" }
            },
            OrdoManualis = new List<Guid> { guidManual },
            Automaticus = new Dictionary<Guid, DataServandaDtoLike>
            {
                [guidAuto] = new DataServandaDtoLike { Revisio = 1, Timestamp = newer, Path = "auto.json", PathNotitia = "auto_n.json" }
            },
            OrdoAutomaticus = new List<Guid> { guidAuto },
            Novissimus = null
        };

        WriteIndex(temp.Path, index);
        WriteDataFile(temp.Path, "manual.json", MakeData(1));
        WriteDataFile(temp.Path, "manual_n.json", MakeNotitia(1));
        WriteDataFile(temp.Path, "auto.json", MakeData(2));
        WriteDataFile(temp.Path, "auto_n.json", MakeNotitia(2));

        var sutReloaded = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);
        var manualis = await sutReloaded.TabulaManualis();
        var automaticus = await sutReloaded.TabulaAutomaticus();

        Assert.Empty(manualis);
        Assert.Single(automaticus);
        Assert.Equal(guidAuto, automaticus[0]);
        Assert.False(File.Exists(System.IO.Path.Combine(temp.Path, "manual.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(temp.Path, "manual_n.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "auto.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "auto_n.json")));
    }

    [Fact]
    public async Task CreareManualis_UpdatesIndexAndLists()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        DateTime start = DateTime.UtcNow;
        Guid guid = await sut.CreareManualis(MakeNotitia(1), MakeData(1));
        DateTime end = DateTime.UtcNow;

        var manualis = await sut.TabulaManualis();
        var automaticus = await sut.TabulaAutomaticus();
        Guid? novissimus = await sut.LegoNovissimus();
        var data = await sut.Arcessere(guid);
        var notitia = await sut.ArcessereNotitiam(guid);

        Assert.Single(manualis);
        Assert.Equal(guid, manualis[0]);
        Assert.Empty(automaticus);
        Assert.Equal(guid, novissimus);
        Assert.Equal(guid, data.Guid);
        Assert.Equal(0, data.Revisio);
        Assert.InRange(data.Timestamp, start.AddSeconds(-1), end.AddSeconds(1));
        Assert.Equal(1, data.Data.Value);
        Assert.Equal("name-1", data.Data.Name);
        Assert.Equal(1, notitia.Notitia.Rank);
        Assert.Equal("title-1", notitia.Notitia.Title);
    }

    [Fact]
    public async Task CreareAutomaticus_RotatesAndKeepsLatest()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path, longitudoAutomaticus: 2, tempusPraeteriitSec: 30);

        Guid guid1 = await sut.CreareAutomaticus(MakeNotitia(1), MakeData(1));
        Guid guid2 = await sut.CreareAutomaticus(MakeNotitia(2), MakeData(2));
        Guid guid3 = await sut.CreareAutomaticus(MakeNotitia(3), MakeData(3));

        var automaticus = await sut.TabulaAutomaticus();
        Assert.Equal(2, automaticus.Count);
        Assert.Equal(guid3, automaticus[0]);
        Assert.Equal(guid2, automaticus[1]);

        Guid? novissimus = await sut.LegoNovissimus();
        Assert.Equal(guid3, novissimus);

        Assert.DoesNotContain(guid1, automaticus);
        string removedPath = System.IO.Path.Combine(temp.Path, guid1.ToString() + ".json");
        string removedNotitiaPath = System.IO.Path.Combine(temp.Path, guid1.ToString() + "_n.json");
        Assert.False(File.Exists(removedPath));
        Assert.False(File.Exists(removedNotitiaPath));
    }

    [Fact]
    public async Task Servare_UpdatesRevisionTimestampAndOrder()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Guid guidA = await sut.CreareManualis(MakeNotitia(1), MakeData(1));
        Guid guidB = await sut.CreareManualis(MakeNotitia(2), MakeData(2));

        var before = await sut.Arcessere(guidA);
        await Task.Delay(10);

        await sut.Servare(guidA, MakeNotitia(99), MakeData(99));
        var after = await sut.Arcessere(guidA);
        var afterNotitia = await sut.ArcessereNotitiam(guidA);

        var manualis = await sut.TabulaManualis();

        Assert.Equal(guidA, manualis[0]);
        Assert.Equal(2, after.Revisio);
        Assert.True(after.Timestamp >= before.Timestamp);
        Assert.Equal(99, after.Data.Value);
        Assert.Equal("name-99", after.Data.Name);
        Assert.Equal(99, afterNotitia.Notitia.Rank);
        Assert.Equal("title-99", afterNotitia.Notitia.Title);
        Assert.Contains(guidB, manualis);
    }

    [Fact]
    public async Task Deleto_RemovesEntryAndUpdatesNovissimus()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Guid guidManual = await sut.CreareManualis(MakeNotitia(1), MakeData(1));
        Guid guidAuto = await sut.CreareAutomaticus(MakeNotitia(2), MakeData(2));

        Guid? latest = await sut.LegoNovissimus();
        Assert.Equal(guidAuto, latest);

        await sut.Deleto(guidAuto);

        Guid? latestAfter = await sut.LegoNovissimus();
        Assert.Equal(guidManual, latestAfter);

        var automaticus = await sut.TabulaAutomaticus();
        Assert.Empty(automaticus);

        await Assert.ThrowsAsync<FileNotFoundException>(() => sut.Arcessere(guidAuto));
        await Assert.ThrowsAsync<FileNotFoundException>(() => sut.ArcessereNotitiam(guidAuto));
    }

    [Fact]
    public async Task ArcessereNotitiam_ReturnsNotitiaData()
    {
        using var temp = new TempDir();
        var sut = FabricaLuditorDataServanda.Creare<TestNotitiaDto, TestDataDto>(temp.Path);

        Guid guid = await sut.CreareManualis(MakeNotitia(7), MakeData(7));
        var notitia = await sut.ArcessereNotitiam(guid);

        Assert.Equal(guid, notitia.Guid);
        Assert.Equal(0, notitia.Revisio);
        Assert.Equal(7, notitia.Notitia.Rank);
        Assert.Equal("title-7", notitia.Notitia.Title);
    }
}
