# Thesaurus

シリアライズ可能なクラスを JSON ファイルに書き出し・読み込みするためのライブラリです。  
`index.json` で目次を管理し、各データは GUID をファイル名とした JSON として保存されます。

---

## インターフェース

### IDataServanda&lt;TData&gt;

保存済みの「1件分の本データ」を表す読み取り専用のインターフェースです。  
`Arcessere` などで取得した結果として返され、メタデータ（GUID・リビジョン・タイムスタンプ）と本体データ `Data` にアクセスできます。

| メンバー | 型 | 説明 |
|----------|-----|------|
| `Guid` | `Guid` | このデータの一意識別子。RFC 4122 の canonical 形式。 |
| `Revisio` | `long` | このデータのリビジョン番号。全体で単一カウンタ。 |
| `Timestamp` | `DateTime` | このデータの更新日時（`updated_at`）。 |
| `Data` | `TData` | 保存時に渡した本データ DTO のデシリアライズ結果。 |

**注意:** `IDataServanda<TData>` は不変のスナップショットとして扱い、返された `Data` を呼び出し元で変更しても永続化されません。上書きする場合は `Servare(guid, notitiaDTO, dataDTO)` を使用してください。

---

### IDataNotitia&lt;TNotitia&gt;

保存済みの「1件分の軽量メタデータ」を表す読み取り専用のインターフェースです。  
`ArcessereNotitiam` で取得した結果として返され、メタデータ（GUID・リビジョン・タイムスタンプ）と軽量データ `Notitia` にアクセスできます。

| メンバー | 型 | 説明 |
|----------|-----|------|
| `Guid` | `Guid` | このデータの一意識別子。RFC 4122 の canonical 形式。 |
| `Revisio` | `long` | このデータのリビジョン番号。全体で単一カウンタ。 |
| `Timestamp` | `DateTime` | このデータの更新日時（`updated_at`）。 |
| `Notitia` | `TNotitia` | 保存時に渡した軽量メタデータ DTO のデシリアライズ結果。 |

**用途:** 大きなデータ本体を読み込まずに、表示用の情報（タイトル、サムネイルパスなど）だけを取得したい場合に使用します。

---

### ILuditorDataServanda&lt;TNotitia, TData&gt;

データの作成・読み取り・更新・削除および一覧取得を行うメインのインターフェースです。  
内部で `index.json` と各データ JSON を管理し、**Semaphore により 1 インスタンスあたり同時に 1 メソッドのみが実行**されます。  
取得は `FabricaLuditorDataServanda.Fabricare<TNotitia, TData>(dirPath)` で行います。

#### 型パラメータ

- **TNotitia**: 軽量メタデータの型（表示情報など）
- **TData**: 本データの型（全データを含む）

#### メソッド一覧

| メソッド | 戻り値 | 説明 |
|----------|--------|------|
| `LegoNovissimus()` | `Task<Guid?>` | 最後に保存されたデータの GUID を取得する。1件も無い場合は `null`。 |
| `Arcessere(guid)` | `Task<IDataServanda<TData>>` | 指定した GUID の本データを読み込んで返す。存在しない GUID の場合は例外。 |
| `ArcessereNotitiam(guid)` | `Task<IDataNotitia<TNotitia>>` | 指定した GUID の軽量メタデータのみを読み込んで返す。存在しない GUID の場合は例外。 |
| `CreareManualis(notitiaDTO, dataDTO)` | `Task<Guid>` | 新規に手動保存用エントリを作成し、データを保存する。採番された GUID を返す。 |
| `CreareAutomaticus(notitiaDTO, dataDTO)` | `Task<Guid>` | 新規に自動保存用エントリを作成し、データを保存する。採番された GUID を返す。自動保存は履歴数に上限あり。 |
| `Servare(guid, notitiaDTO, dataDTO)` | `Task<Guid>` | 既存の GUID に対応するファイルを、渡した DTO で上書き保存する。同じ GUID を返す。 |
| `Deleto(guid)` | `Task` | 指定した GUID のデータ（index 上のエントリとデータ JSON）を削除する。 |
| `TabulaManualis()` | `Task<IReadOnlyList<Guid>>` | 手動保存されたデータの GUID 一覧を取得する。 |
| `TabulaAutomaticus()` | `Task<IReadOnlyList<Guid>>` | 自動保存されたデータの GUID 一覧を取得する。 |

すべて非同期メソッドのため、呼び出し時は `await` を使用してください。

#### Manualis（手動保存）と Automaticus（自動保存）

データの保存先は **Manualis** と **Automaticus** の2種類があります。用途に応じて使い分けてください。

**Manualis（手動保存）**  
GUID を指定した追加・更新・削除を想定したストアです。エントリの作成は `CreareManualis`、既存の更新は `Servare(guid, dataDTO)`、削除は `Deleto(guid)` で行います。一般的な CRUD（作成・読み取り・更新・削除）を行うユースケースでは、こちらを使います。手動で管理するエントリ数に制限はありません。

**Automaticus（自動保存）**  
世代管理されるデータを想定したストアです。`longitudoAutomaticus`（ファクトリで指定、デフォルト 5）で保持する件数が決まり、**新しいデータを追加すると古いものから順に削除**されます。GUID を指定して `Servare` で更新することは可能ですが、運用としては「常に `CreareAutomatics` で新データを追加し、古い世代は自動で消えていく」形にすることを推奨します。自動保存・履歴・スナップショットのような用途に向いています。

---

## 利用時の留意点

- **型パラメータ `TNotitia` と `TData` は JSON シリアライズ可能である必要があります**  
  - 使用しているシリアライザ（例: `System.Text.Json` や Newtonsoft.Json）が扱える型にしてください。必要に応じて `[Serializable]` や属性・パブリックプロパティなどの要件を満たしてください。
  - `TNotitia` は軽量なメタデータ（表示用の情報など）を、`TData` は本データ全体を格納する想定です。

- **引数で渡す DTO はスナップショット化しなくてよい**  
  - メソッド呼び出し時にその場でシリアライズされるため、呼び出し後に呼び出し元が同じオブジェクトを変更しても、既に保存された内容には影響しません。参照をそのまま渡して問題ありません。

- **軽量データと本データの使い分け**  
  - 一覧表示などで多数のエントリを処理する場合は `ArcessereNotitiam` で軽量データのみを取得することで、ファイル I/O を削減できます。詳細表示やフル編集時のみ `Arcessere` で本データを読み込む運用を推奨します。

- **コンストラクタ（Luditor の生成）にはオーバーヘッドがある**  
  - `FabricaLuditorDataServanda.Fabricare<TNotitia, TData>(dirPath)` 実行時に、`index.json` の読み込みとノーマライズ（不整合データの除去、`revisio_proximus` や `ordo` の確定など）が走ります。頻繁にインスタンスを生成・破棄するのではなく、**1 ディレクトリにつき 1 つの `ILuditorDataServanda<TNotitia, TData>` を保持して再利用**する運用を推奨します。

- **1 Luditor で 1 メソッドだけが排他で動く**  
  - 1 つの `ILuditorDataServanda<TNotitia, TData>` インスタンスでは、Semaphore により同時に実行されるメソッドは常に 1 つです。複数メソッドを並列に呼んでも、内部では順次実行されます。大量の並列呼び出しを行う場合は、キューイングやスロットリングを検討してください。

---

## ファクトリ

```csharp
// データディレクトリの絶対パスを指定して Luditor を取得
// MyNotitiaDto: 軽量メタデータの型、MyDataDto: 本データの型
ILuditorDataServanda<MyNotitiaDto, MyDataDto> luditor = 
    FabricaLuditorDataServanda.Fabricare<MyNotitiaDto, MyDataDto>(dirPath);

// 自動保存の履歴数・経過秒数を指定する場合（デフォルト: 5 件, 30 秒）
ILuditorDataServanda<MyNotitiaDto, MyDataDto> luditor2 = 
    FabricaLuditorDataServanda.Fabricare<MyNotitiaDto, MyDataDto>(
        dirPath,
        longitudoAutomaticus: 10,
        tempusPraeteriitSec: 60
    );

// ファイル I/O を差し替える場合（IScriba を渡すオーバーロード）
ILuditorDataServanda<MyNotitiaDto, MyDataDto> luditor3 = 
    FabricaLuditorDataServanda.Fabricare<MyNotitiaDto, MyDataDto>(dirPath, myScriba);
```

データの実体は `index.json` があるディレクトリに、GUID をファイル名とした JSON として配置されます。`dirPath` にはそのディレクトリの**絶対パス**を渡してください。

**データファイル構成:**
- `<guid>.json` - 本データ（TData）
- `<guid>_n.json` - 軽量メタデータ（TNotitia）

---

## Unity プロジェクトへの取り込み

このライブラリは .NET Standard 2.1 でビルドされており、Unity 2021.2 以降で利用できます。取り込み方法は次の2通りです。

### 方法1: DLL を配置する

1. リポジトリで `dotnet build -c Release` を実行し、`thesaurus/bin/Release/netstandard2.1/thesaurus.dll` をビルドする。
2. Unity プロジェクト内の `Assets/Plugins/Thesaurus` など任意のフォルダに `thesaurus.dll` を配置する。
3. 本ライブラリは **System.Text.Json** に依存しています。Unity 2022.1 以降ではエンジンに含まれているためそのままで問題ありません。それより古い Unity の場合は、同様に .NET Standard 2.1 用の System.Text.Json の DLL を Plugins に含めるか、別途対応が必要です。
4. プレイヤー設定で **API Compatibility Level** が .NET Standard 2.1 になっていることを確認する。

手軽に試すときや、ソースを触らない運用に向いています。ライブラリを更新したときは、再度ビルドして DLL を差し替えてください。

### 方法2: Package Manager で Git から取得する（推奨）

Unity の Package Manager は、Git の URL を指定してパッケージを追加できます。UPM 用の `package.json` は **thesaurus フォルダ内**にあり、本体のみがパッケージとして取り込まれます（テストは含みません）。

**手順**

1. Unity で **Window > Package Manager** を開く。
2. 左上の **+** から **Add package from git URL** を選ぶ。
3. 次の URL を入力して **Add** する（`?path=/thesaurus` で本体フォルダのみを指定）。
   - 最新を取得する場合:  
     `https://github.com/sy4may0/Yulinti.Thesaurus.git?path=/thesaurus`
   - タグでバージョンを固定する場合:  
     `https://github.com/sy4may0/Yulinti.Thesaurus.git?path=/thesaurus#v0.0.3`

**manifest.json で直接指定する場合**

プロジェクトの `Packages/manifest.json` の `dependencies` に追加します。

```json
{
  "dependencies": {
    "com.yulinti.thesaurus": "https://github.com/sy4may0/thesaurus.git?path=/thesaurus"
  }
}
```

バージョン固定する場合は URL の末尾に `#v0.0.1` や `#main` を付けます（`?path=/thesaurus` のうしろに `#リビジョン`）。  

**補足**

- この方法では **本体のソース（.cs）だけが Unity に取り込まれ**、テスト用フォルダは含まれません。
- Git クライアント（2.14 以上）がインストールされ、PATH が通っている必要があります。
