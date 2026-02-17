# 設計
シリアライズ可能なクラスをJSONファイルに書き出す。

## ルール
- このクラスはランタイムにnewとかList.Addをやっていい。やらないと死んでしまう。
- GUID文字列は RFC 4122 の canonical
- クラス内の日時はDateTimeを使う。
- 各種ファイルパスはindex.jsonがあるディレクトリ相対パスで扱う。
- index.jsonのパスのみ絶対パスで、引数で受け取る。
- unixtimeは秒までを取り扱う。ミリ秒は含めない。

## JSON設計

### index.json
JSONファイルの目次を管理する。

```json
{
    "revisio_proximus": <long>,
    "versio": <int>,
    "manualis": {
        <string/guid>: {
            "revisio": <long>,
            "timestamp": <string/datetime>,
            "path": <string>
        },
        ...
    },
    "ordo_manualis": [<string/guid>, ...],
    "automaticus": {
        <string/guid>: {
            "revisio": <long>,
            "timestamp": <string/datetime>,
            "path": <string>
        },
        ...
    },
    "ordo_automaticus": [<string/guid>, ...],
    "novissimus": {
        "methodus": <string(manualis|automaticus)>,
        "guid": <string/guid>,
        "revisio": <long>,
        "timestamp": <string/datetime>
    }
}
```
- revisio_proximus

    現在のRevision。保存された最大Revision+1の値になる。

- versio

    JSONのバージョン。

- manualis

    手動保存したデータを管理する。

- ordo_manualis
    
    手動保存データのキー配列

- automaticus

    自動保存されたデータを管理する。

- ordo_automaticus

    自動保存データのキー配列

- novissimus
   
    最後に保存されたデータを管理する。

各パラメータ

|key|説明|
|---|---|
|guid|データのGUID|
|revisio|データのリビジョン/全体で1つのカウンタを使う。必ずnovisssimusのrevisioが最大になる。|
|timestamp|stringでタイムスタンプを保持。これはupdated_atとして使う。created_atではない。|
|path|データのJSONファイルパス|

### データJSON
`path`に指定するJSONファイルの設計。

- ファイル名はguidとする。
- 内容は投入するDTOクラスに依存する。

## interface設計
### public Yulinti.Thesaurus.IDataServanda<T>
- DataServandaの公開IF
- 必要な値だけアクセスできる。(`Guid`, `Revisio`, `Timestamp`, `Data`)

### public Yulinti.Thesaurus.ILuditor<T>
- Luditorの公開IF
- 最新Revisionのguidを取得する。(`string LegoNovissimus()`)
- (Async)指定guidのデータを取得する。(`Task<Arcessere<T>(string guid)`)
- (Async)新規にファイルを作成してデータを保存する。(`Task<Guid> Servare(T dataDTO)`)
- (Async)Automaticusにデータを保存する。(`Task<Guid> ServareAutomaticus(T dataDTO)`)
- (Async)既存のファイルにデータを保存する。(`Task<Guid> Servare(string guid, T dataDTO)`)
- (Async)既存のファイルを削除する。(`Task Deleto(string guid)`)
- guidのリストを取得する。(`string[] TabulaManualis(), string[] TabulaAutomaticus()`)


## DTOクラス設計
### internal Yulinti.Thesaurus.IndexServandaDto
- `index.json`全体のDTO

### internal Yulinti.Thesaurus.DataServandaDto<T>
- `manualis`および`automaticus`辞書内の構造のDTO

### internal Yulinti.Thesaurus.NovissimusServandaDto
- `novissimus`のDTO

### internal Yulinti.Thesaurus.DataServanda<T>
- 外部に公開するデータクラス。
- `guid`、`DataServanda`, `T data`をパラメータに持つ。

## ロジッククラス設計

### internal Yulinti.Thesaurus.Scriba
ファイル書き込み/読み込みを行うクラス。

- (Async)stringを受け取りファイル書き込みを行う。
- (Async)ファイルを読み込みstringを返す。

### internal Yulinti.Thesaurus.Luditor<T>
DataServandaの書き込み/読み込みを行う。

- Tを受け取り`data`の型を最初に決定する。
- コンストラクタの引数はデータディレクトリのパスのみ。
- (R)コンストラクタでindex.jsonを読み込む。
- (W)コンストラクタでindex.jsonのnormalizeを実行(`void Normare()`)
- (R)最新Revisionのguidを取得する。(`string LegoNovissimus()`)
- (R)guidのリストを取得する。(`IReadOnlyList TabulaManualis(), IReadOnlyList TabulaAutomaticus()`)
- (R)(Async)指定guidのデータを取得する。(`Task<IDataServanda<T>> Arcessere(string guid)`)
- (W)(Async)新規にファイルを作成してデータを保存する。(`Task Servare(T dataDTO)`)
- (W)(Async)既存のファイルにデータを保存する。(`Task Servare(string guid, T dataDTO)`)
- (W)(Async)既存のファイルを削除する。(`Task Deleto(string guid)`)
- 自身でIndexServandaを保持し、各関数で状態が変われば更新する。
- メソッド説明で(W)がついている場合、index.jsonの更新を行う。(Async)(`Task ServareIndex()`)

### internal Yulinti.Thesaurus.Luditor<T>.Normare()
Luditorのノーマライズチェック項目。

[Phase0 バージョンチェック/マイグレーション]
- ここは未実装とする。リリース後にアップデートした際はここにマイグレーション関数を入れる。準備としてマイグレーション処理クラスをDIできるようにしておくと良いかもしれない。

[Phase1 不整合データの除去]
- pathのexistsをチェック。存在しなければエントリを除去する。(ファイル内のチェックまではしない。内部が壊れていた場合はRead時に例外とする。)
- guidの重複をチェック。重複している場合はtimestampが古いエントリを削除する。
- revisioの重複をチェック。重複している場合はtimestampが古いエントリを削除する。

[Phase2 関係を確定]
- revisio_proximusを確定(全データから最大revisioを計算する。)
- ordoを確定(manualis, automaticusのDictからListを再構成してordoに代入する。)
- revisio_proximus-1からnovissimusを確定。

### public static Yulinti.Thesaurus.FabricaLuditoris
- ILuditorを外から初期化するためのstaticクラス。
- Luditorを初期化する。(`ILuditor<T> Fabricare<T>(string dirPath)`)