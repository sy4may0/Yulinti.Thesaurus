using System.IO;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Yulinti.Thesaurus {
    internal class LuditorDataServanda<T> {
        private readonly string _dirPath;
        private readonly string _indexPath;
        private readonly int _longitudoAutomaticus;
        private readonly IScriba _scriba;

        private IndexServandaDto _indexServandaDto;

        /// コンストラクタ
        /// <summary>
        /// コンストラクタ。激重処理なので、Unityの最初のロードとかで実行する。ランタイムでこれを呼んではいけない。
        /// <param name="dirPath">データディレクトリのパス</param>
        /// <param name="scriba">Scribaインスタンス</param>
        /// <param name="LongitudoAutomaticus">Automaticusの最大長（デフォルト5）</param>
        /// </summary>
        public LuditorDataServanda(string dirPath, IScriba scriba, int longitudoAutomaticus=5) {
            _dirPath = dirPath;
            _indexPath = Path.Combine(dirPath, "index.json");
            _longitudoAutomaticus = longitudoAutomaticus;
            _scriba = scriba;

            _indexServandaDto = InitareServandaDto();
            Normare();
            ServareIndexSync();
        }

        private IndexServandaDto InitareServandaDto() {
            //ファイルが存在する場合はScribaでロードする。
            if(File.Exists(_indexPath)) {
                string indexJson = _scriba.LegereSync(_indexPath);
                return JsonSerializer.Deserialize<IndexServandaDto>(indexJson);
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
                // 古い方を削除
                if(_indexServandaDto.Manualis[automaticus.Key].Timestamp < automaticus.Value.Timestamp) {
                    DeletoDataServandaSync(automaticus.Key);
                } else {
                    DeletoDataServandaSync(automaticus.Key);
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
            if(_indexServandaDto.Automaticus.Count > _longitudoAutomaticus) {
                var sorted = _indexServandaDto.Automaticus.OrderBy(x => x.Value.Revisio).ToList();
                int deleteCount = _indexServandaDto.Automaticus.Count - _longitudoAutomaticus;
                for(int i = 0; i < deleteCount; i++) {
                    DeletoDataServandaSync(sorted[i].Key);
                }
            }

            // Phase2: 関係を確定
            //revisio_proximusを確定（空の場合は0からスタート）
            long revisioMaximus = 0;
            if(_indexServandaDto.Manualis.Count > 0) {
                revisioMaximus = Math.Max(revisioMaximus, _indexServandaDto.Manualis.Max(x => x.Value.Revisio));
            }
            if(_indexServandaDto.Automaticus.Count > 0) {
                revisioMaximus = Math.Max(revisioMaximus, _indexServandaDto.Automaticus.Max(x => x.Value.Revisio));
            }
            _indexServandaDto.RevisioProximus = revisioMaximus + 1;

            //ordoを確定(Revisioでソートする)
            _indexServandaDto.OrdoManualis = _indexServandaDto.Manualis.Keys
                                                 .OrderByDescending(x => _indexServandaDto.Manualis[x].Revisio).ToList();
            _indexServandaDto.OrdoAutomaticus = _indexServandaDto.Automaticus.Keys
                                                 .OrderByDescending(x => _indexServandaDto.Automaticus[x].Revisio).ToList();

            // novissimusを確定（データが存在しない場合はnull）
            if(_indexServandaDto.Manualis.Count == 0 && _indexServandaDto.Automaticus.Count == 0) {
                _indexServandaDto.Novissimus = null;
            } else if(_indexServandaDto.Manualis.Count == 0) {
                // Automaticusのみ存在
                Guid guidAutomaticusMaximus = _indexServandaDto.OrdoAutomaticus.First();
                _indexServandaDto.Novissimus = new NovissimusServandaDto {
                    Methodus = "automaticus",
                    Guid = guidAutomaticusMaximus,
                    Revisio = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Revisio,
                    Timestamp = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Timestamp
                };
            } else if(_indexServandaDto.Automaticus.Count == 0) {
                // Manualisのみ存在
                Guid guidManualisMaximus = _indexServandaDto.OrdoManualis.First();
                _indexServandaDto.Novissimus = new NovissimusServandaDto {
                    Methodus = "manualis",
                    Guid = guidManualisMaximus,
                    Revisio = _indexServandaDto.Manualis[guidManualisMaximus].Revisio,
                    Timestamp = _indexServandaDto.Manualis[guidManualisMaximus].Timestamp
                };
            } else {
                // 両方存在する場合、最新のものを選択
                Guid guidManualisMaximus = _indexServandaDto.OrdoManualis.First();
                Guid guidAutomaticusMaximus = _indexServandaDto.OrdoAutomaticus.First();
                if(_indexServandaDto.Manualis[guidManualisMaximus].Timestamp > _indexServandaDto.Automaticus[guidAutomaticusMaximus].Timestamp) {
                    _indexServandaDto.Novissimus = new NovissimusServandaDto {
                        Methodus = "manualis",
                        Guid = guidManualisMaximus,
                        Revisio = _indexServandaDto.Manualis[guidManualisMaximus].Revisio,
                        Timestamp = _indexServandaDto.Manualis[guidManualisMaximus].Timestamp
                    };
                } else {
                    _indexServandaDto.Novissimus = new NovissimusServandaDto {
                        Methodus = "automaticus",
                        Guid = guidAutomaticusMaximus,
                        Revisio = _indexServandaDto.Automaticus[guidAutomaticusMaximus].Revisio,
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
            await _scriba.Scribere(_indexPath, indexJson);
        }

        // この関数はコンストラクタ外から使うな。
        private void ServareIndexSync() {
            string indexJson = JsonSerializer.Serialize(_indexServandaDto);
            _scriba.ScribereSync(_indexPath, indexJson);
        }

        // この関数はコンストラクタ外から使うな。
        private void DeletoDataServandaSync(Guid guid) {
            if(_indexServandaDto.Manualis.ContainsKey(guid)) {
                // ファイルを削除
                if(File.Exists(Path.Combine(_dirPath, _indexServandaDto.Manualis[guid].Path))) {
                    File.Delete(Path.Combine(_dirPath, _indexServandaDto.Manualis[guid].Path));
                }
                _indexServandaDto.Manualis.Remove(guid);
            } else {
                // ファイルを削除
                if(File.Exists(Path.Combine(_dirPath, _indexServandaDto.Automaticus[guid].Path))) {
                    File.Delete(Path.Combine(_dirPath, _indexServandaDto.Automaticus[guid].Path));
                }
                _indexServandaDto.Automaticus.Remove(guid);
            }
        }
    }
}