using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace Yulinti.Thesaurus {
    internal class LuditorDataServanda<T> : ILuditorDataServanda<T> {
        private readonly string _dirPath;
        private readonly string _indexPath;
        private readonly int _longitudoAutomaticus;
        private readonly int _tempusPraeteriit;
        private readonly TimeSpan _tempusPraeteriitTS;
        private readonly IScriba _scriba;

        // _indexServandaDtoのアクセスを排他制御するためのセマフォ
        private SemaphoreSlim _semaphoreIndexServanda = new SemaphoreSlim(1, 1);
        private IndexServandaDto _indexServandaDto;

        /// コンストラクタ
        /// <summary>
        /// コンストラクタ。激重処理なので、Unityの最初のロードとかで実行する。ランタイムでこれを呼んではいけない。
        /// <param name="dirPath">データディレクトリのパス</param>
        /// <param name="scriba">Scribaインスタンス</param>
        /// <param name="LongitudoAutomaticus">Automaticusの最大長（デフォルト5）</param>
        /// <param name="tempusPraeteriit">ロックタイムアウト時間（デフォルト30秒）</param>
        /// </summary>
        public LuditorDataServanda(
            string dirPath, IScriba scriba, 
            int longitudoAutomaticus=5,
            int tempusPraeteriitSec = 30
        ) {
            _dirPath = dirPath;
            _indexPath = Path.Combine(dirPath, "index.json");
            _longitudoAutomaticus = longitudoAutomaticus;
            if (tempusPraeteriitSec < 0) {
                _tempusPraeteriit = -1;
                _tempusPraeteriitTS = Timeout.InfiniteTimeSpan;
            } else {
                _tempusPraeteriit = tempusPraeteriitSec;
                _tempusPraeteriitTS = TimeSpan.FromSeconds(tempusPraeteriitSec);
            }
            _scriba = scriba;

            _indexServandaDto = InitareServandaDto();
            Normare();
            ServareIndexSync();
        }

        private IndexServandaDto InitareServandaDto() {
            //ファイルが存在する場合はScribaでロードする。
            if(File.Exists(_indexPath)) {
                string indexJson = _scriba.LegereSync(_indexPath);
                var dto = JsonSerializer.Deserialize<IndexServandaDto>(indexJson);
                return dto ?? throw new InvalidOperationException("index.json could not be deserialized.");
            }

            // 無い場合は新規作成する。
            IndexServandaDto indexServandaDto = new IndexServandaDto();
            _scriba.ScribereSync(_indexPath, JsonSerializer.Serialize(indexServandaDto));
            return indexServandaDto;
        }

        private void Normare() {
            //Phase0: バージョンチェック/マイグレーション
            //未実装

            //Phase1: 不整合データの除去
            //Existsチェック（削除対象を先に収集）
            var manualisDelendus = _indexServandaDto.Manualis
                .Where(m => !File.Exists(Path.Combine(_dirPath, m.Value.Path)))
                .Select(m => m.Key)
                .ToList();
            foreach(var key in manualisDelendus) {
                DeletoDataServandaSync(key);
            }

            var automaticusDelendus = _indexServandaDto.Automaticus
                .Where(a => !File.Exists(Path.Combine(_dirPath, a.Value.Path)))
                .Select(a => a.Key)
                .ToList();
            foreach(var key in automaticusDelendus) {
                DeletoDataServandaSync(key);
            }

            //Guid重複チェック(Manualis/Automaticusで重複は許可されない)
            var guidDuplicatus = _indexServandaDto.Automaticus
                .Where(a => _indexServandaDto.Manualis.ContainsKey(a.Key))
                .ToList();
            
            foreach(var automaticus in guidDuplicatus) {
                var m = _indexServandaDto.Manualis[automaticus.Key];
                var a = automaticus.Value;

                if (m.Timestamp < a.Timestamp) {
                    // DeletoDataServandaはManualis/Automaticusをまとめてやるので、ここでは使えない。
                    _indexServandaDto.Manualis.Remove(automaticus.Key);
                    var fullPath = Path.Combine(_dirPath, m.Path);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                } else {
                    _indexServandaDto.Automaticus.Remove(automaticus.Key);
                    var fullPath = Path.Combine(_dirPath, a.Path);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                }
            }

            //Revisio重複チェック
            Dictionary<long, List<(Guid guid, DataServandaDto data, bool isManualis)>> dataDuplicatus = 
                new Dictionary<long, List<(Guid, DataServandaDto, bool)>>();
            
            // ManalisとAutomaticusのデータを収集
            foreach(var manualis in _indexServandaDto.Manualis) {
                if(!dataDuplicatus.ContainsKey(manualis.Value.Revisio)) {
                    dataDuplicatus[manualis.Value.Revisio] = new List<(Guid, DataServandaDto, bool)>();
                }
                dataDuplicatus[manualis.Value.Revisio].Add((manualis.Key, manualis.Value, true));
            }
            foreach(var automaticus in _indexServandaDto.Automaticus) {
                if(!dataDuplicatus.ContainsKey(automaticus.Value.Revisio)) {
                    dataDuplicatus[automaticus.Value.Revisio] = new List<(Guid, DataServandaDto, bool)>();
                }
                dataDuplicatus[automaticus.Value.Revisio].Add((automaticus.Key, automaticus.Value, false));
            }

            // 重複データの削除（timestampが古い方を削除）
            foreach(var dup in dataDuplicatus) {
                if(dup.Value.Count > 1) {
                    // timestampでソートして、最新のもの以外を削除
                    var sorted = dup.Value.OrderByDescending(x => x.data.Timestamp).ToList();
                    for(int i = 1; i < sorted.Count; i++) {
                        DeletoDataServandaSync(sorted[i].guid);
                    }
                }
            }

            // Automaticusの最大長を超えている場合、Revisuoが小さいものから削除
            RotareAutomaticus();

            // Phase2: 関係を確定
            //revisio_proximusを確定（空の場合は0からスタート）
            long revisioMaximus = 0;
            if(_indexServandaDto.Manualis.Count > 0) {
                revisioMaximus = Math.Max(revisioMaximus, _indexServandaDto.Manualis.Max(x => x.Value.Revisio));
            }
            if(_indexServandaDto.Automaticus.Count > 0) {
                revisioMaximus = Math.Max(revisioMaximus, _indexServandaDto.Automaticus.Max(x => x.Value.Revisio));
            }
            if(
                _indexServandaDto.Manualis.Count > 0 || 
                _indexServandaDto.Automaticus.Count > 0
            ) {
                _indexServandaDto.RevisioProximus = revisioMaximus + 1;
            }

            //ordoを確定(Revisioでソートする)
            _indexServandaDto.OrdoManualis = _indexServandaDto.Manualis.Keys
                                                 .OrderByDescending(x => _indexServandaDto.Manualis[x].Revisio).ToList();
            _indexServandaDto.OrdoAutomaticus = _indexServandaDto.Automaticus.Keys
                                                 .OrderByDescending(x => _indexServandaDto.Automaticus[x].Revisio).ToList();

            NormareNovissimus();
        }

        /// <summary>Manualis/Automaticusの現在状態からnovissimusを確定する。</summary>
        private void NormareNovissimus() {
            if(_indexServandaDto.Manualis.Count == 0 && _indexServandaDto.Automaticus.Count == 0) {
                _indexServandaDto.Novissimus = null;
            } else if(_indexServandaDto.Manualis.Count == 0) {
                Guid guidAutomaticusMaximus = _indexServandaDto.OrdoAutomaticus.First();
                _indexServandaDto.Novissimus = new NovissimusServandaDto {
                    Methodus = "automaticus",
                    Guid = guidAutomaticusMaximus,
                    Revisio = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Revisio,
                    Timestamp = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Timestamp
                };
            } else if(_indexServandaDto.Automaticus.Count == 0) {
                Guid guidManualisMaximus = _indexServandaDto.OrdoManualis.First();
                _indexServandaDto.Novissimus = new NovissimusServandaDto {
                    Methodus = "manualis",
                    Guid = guidManualisMaximus,
                    Revisio = _indexServandaDto.Manualis[guidManualisMaximus].Revisio,
                    Timestamp = _indexServandaDto.Manualis[guidManualisMaximus].Timestamp
                };
            } else {
                // 両方存在する場合。revisioはNormare後は全体で一意なので、大きい方がnovissimus（revisio_proximus-1に相当）
                Guid guidManualisMaximus = _indexServandaDto.OrdoManualis.First();
                Guid guidAutomaticusMaximus = _indexServandaDto.OrdoAutomaticus.First();
                long revisioM = _indexServandaDto.Manualis[guidManualisMaximus].Revisio;
                long revisioA = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Revisio;
                if (revisioM >= revisioA) {
                    _indexServandaDto.Novissimus = new NovissimusServandaDto {
                        Methodus = "manualis",
                        Guid = guidManualisMaximus,
                        Revisio = revisioM,
                        Timestamp = _indexServandaDto.Manualis[guidManualisMaximus].Timestamp
                    };
                } else {
                    _indexServandaDto.Novissimus = new NovissimusServandaDto {
                        Methodus = "automaticus",
                        Guid = guidAutomaticusMaximus,
                        Revisio = revisioA,
                        Timestamp = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Timestamp
                    };
                }
            }
        }

        // GUID指定でManualis/Automaticusどちらかを自動選択して返す。
        private Dictionary<Guid, DataServandaDto> SelegereDict(Guid guid) {
            if(_indexServandaDto.Manualis.ContainsKey(guid)) {
                return _indexServandaDto.Manualis;
            } else {
                return _indexServandaDto.Automaticus;
            }
        }

        private async Task ServareIndex() {
            // ここでスナップショット確定
            string indexJson = JsonSerializer.Serialize(_indexServandaDto);
            await _scriba.Scribere(_indexPath, indexJson, _tempusPraeteriit);
        }

        // この関数はコンストラクタ外から使うな。
        private void ServareIndexSync() {
            string indexJson = JsonSerializer.Serialize(_indexServandaDto);
            _scriba.ScribereSync(_indexPath, indexJson);
        }

        private void DeletoDataServandaSync(Guid guid) {
            if (_indexServandaDto.Manualis.TryGetValue(guid, out var m))
            {
                var fullPath = Path.Combine(_dirPath, m.Path);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                _indexServandaDto.Manualis.Remove(guid);
                _indexServandaDto.OrdoManualis.Remove(guid);
                return;
            }
        
            if (_indexServandaDto.Automaticus.TryGetValue(guid, out var a))
            {
                var fullPath = Path.Combine(_dirPath, a.Path);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                _indexServandaDto.Automaticus.Remove(guid);
                _indexServandaDto.OrdoAutomaticus.Remove(guid);
            }
        }

        private void RotareAutomaticus() {
             if(_indexServandaDto.Automaticus.Count > _longitudoAutomaticus) {
                var sorted = _indexServandaDto.Automaticus.OrderBy(x => x.Value.Revisio).ToList();
                int deleteCount = _indexServandaDto.Automaticus.Count - _longitudoAutomaticus;
                for(int i = 0; i < deleteCount; i++) {
                    DeletoDataServandaSync(sorted[i].Key);
                }
                NormareNovissimus();
            }
        }

        public async Task<Guid?> LegoNovissimus() {
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                return _indexServandaDto.Novissimus?.Guid;
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task<IDataServanda<T>> Arcessere(Guid guid) {
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                Dictionary<Guid, DataServandaDto> d = SelegereDict(guid);
                if(!d.TryGetValue(guid, out var data)) {
                    throw new FileNotFoundException($"File not found: {guid}");
                }
                // データファイルパス取得
                string path = Path.Combine(_dirPath, data.Path);
                // revisio取得
                long revisio = data.Revisio;
                // timestamp取得
                DateTime timestamp = data.Timestamp;
                // データファイル読み込み
                string json = await _scriba.Legere(path, _tempusPraeteriit);
                var dto = JsonSerializer.Deserialize<T>(json);
                if (dto == null)
                    throw new InvalidOperationException($"Data file deserialized to null: {path}");

                return new DataServanda<T>(guid, revisio, timestamp, dto);
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        /// <summary>新規データ作成の共通処理。呼び出し元でセマフォ取得済みであること。dataJsonは呼び出し時点のスナップショットとしてメインスレッドで事前に作成済みであること。</summary>
        private async Task<Guid> CreareInterna(string dataJson, bool isManualis) {
            Guid guid = Guid.NewGuid();
            long revisio = _indexServandaDto.RevisioProximus;
            DateTime timestamp = DateTime.UtcNow;
            string path = guid.ToString() + ".json";

            string fPath = Path.Combine(_dirPath, path);
            await _scriba.Scribere(fPath, dataJson, _tempusPraeteriit);

            var dto = new DataServandaDto {
                Revisio = revisio,
                Timestamp = timestamp,
                Path = path
            };
            string methodus = isManualis ? "manualis" : "automaticus";
            if (isManualis) {
                _indexServandaDto.Manualis[guid] = dto;
                _indexServandaDto.OrdoManualis.Insert(0, guid);
            } else {
                _indexServandaDto.Automaticus[guid] = dto;
                _indexServandaDto.OrdoAutomaticus.Insert(0, guid);
            }
            _indexServandaDto.RevisioProximus++;

            if (_indexServandaDto.Novissimus == null) {
                _indexServandaDto.Novissimus = new NovissimusServandaDto {
                    Methodus = methodus,
                    Guid = guid,
                    Revisio = revisio,
                    Timestamp = timestamp
                };
            } else {
                _indexServandaDto.Novissimus.Methodus = methodus;
                _indexServandaDto.Novissimus.Guid = guid;
                _indexServandaDto.Novissimus.Revisio = revisio;
                _indexServandaDto.Novissimus.Timestamp = timestamp;
            }

            if (!isManualis) {
                RotareAutomaticus();
            }

            await ServareIndex();
            return guid;
        }

        public async Task<Guid> CreareManualis(T dataDTO) {
            // 呼び出し時点のTのスナップショットを確定するため、await前にJSON化する。
            string dataJson = JsonSerializer.Serialize(dataDTO);
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                return await CreareInterna(dataJson, isManualis: true);
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task<Guid> CreareAutomatics(T dataDTO) {
            // 呼び出し時点のTのスナップショットを確定するため、await前にJSON化する。
            string dataJson = JsonSerializer.Serialize(dataDTO);
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                return await CreareInterna(dataJson, isManualis: false);
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task<Guid> Servare(Guid guid, T dataDTO) {
            // 呼び出し時点のTのスナップショットを確定するため、await前にJSON化する。
            string dataJson = JsonSerializer.Serialize(dataDTO);
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                // ManualisかAutomaticusかを判断
                bool isManualis = _indexServandaDto.Manualis.ContainsKey(guid);
                Dictionary<Guid, DataServandaDto> tabla = SelegereDict(guid);
    
                if(!tabla.TryGetValue(guid, out var data)) {
                    throw new FileNotFoundException($"File not found: {guid}");
                }
    
                string path = Path.Combine(_dirPath, data.Path);
                await _scriba.Scribere(path, dataJson, _tempusPraeteriit);
    
                data.Revisio = _indexServandaDto.RevisioProximus;
                data.Timestamp = DateTime.UtcNow;
    
                if (isManualis) {
                    _indexServandaDto.OrdoManualis.Remove(guid);
                    _indexServandaDto.OrdoManualis.Insert(0, guid);
                } else {
                    _indexServandaDto.OrdoAutomaticus.Remove(guid);
                    _indexServandaDto.OrdoAutomaticus.Insert(0, guid);
                }
                _indexServandaDto.RevisioProximus++;

                string methodus = isManualis ? "manualis" : "automaticus";
                if (_indexServandaDto.Novissimus == null) {
                    _indexServandaDto.Novissimus = new NovissimusServandaDto {
                        Methodus = methodus,
                        Guid = guid,
                        Revisio = data.Revisio,
                        Timestamp = data.Timestamp
                    };
                } else {
                    _indexServandaDto.Novissimus.Methodus = methodus;
                    _indexServandaDto.Novissimus.Guid = guid;
                    _indexServandaDto.Novissimus.Revisio = data.Revisio;
                    _indexServandaDto.Novissimus.Timestamp = data.Timestamp;
                }
    
                await ServareIndex();
                return guid;
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task Deleto(Guid guid) {
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                DeletoDataServandaSync(guid);
                NormareNovissimus();
                await ServareIndex();
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task<IReadOnlyList<Guid>> TabulaManualis() {
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                return _indexServandaDto.OrdoManualis.ToList();
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }

        public async Task<IReadOnlyList<Guid>> TabulaAutomaticus() {
            bool praeteriit = await _semaphoreIndexServanda.WaitAsync(_tempusPraeteriitTS);
            if (!praeteriit) throw new TimeoutException("IndexServanda lock timeout.");

            try {
                return _indexServandaDto.OrdoAutomaticus.ToList();
            } finally {
                _semaphoreIndexServanda.Release();
            }
        }
    }
}