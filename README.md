---
title: Web小説の取得と発行
tags: Blazor ASP.NET MudBlazor PetaPoco MySQL MariaDB
---

# Web小説の取得と発行
## はじめに
これは極めて個人的なプロジェクトです。

### 目的
Web小説を取得し、EPUBを生成してKindleパーソナルドキュメント向けに発行(SendToKindle)します。

#### 経緯
元々、FileMakerで実現していたものです。

### 環境
#### ビルド
- .NET 8.0
- MudBlazor 8.6.0
- PetaPoco 6.0.683
- MySqlConnector 2.4.0
- AngleSharp 1.3.0
- MailKit 4.12.1

#### サーバ

https://zenn.dev/tetr4lab/articles/ad947ade600764

## できること
- Web小説の取得 (なろう、ノクターン、カクヨム、ノベルアップ、文字の冷凍庫)
- 小説内容の確認、各種メモ、文字校正
- EPUBの生成とKindleパーソナルドキュメント向け発行

## データ構造
### 論理構成
- 設定
  - 
  - 備考
- 書誌
  - 
  - 備考
- シート
  - 
  - 備考

### テーブルスキーマ

<details><summary>sql</summary>

```sql:MariaDB
```

</details>

## 画面と機能
- 共通: ナビゲーションバー
  - メニュー: ボタン並び
    - Books、Publish、Contents、Read、Settings ページ切り替え
  - 検索語: フィールド
    - 検索実行: ボタン
    - 絞込解除: ボタン
  - 未着手: ボタン
    - 簡易検索
  - テーマ: ボタン
    - ライト/ダークモード切り替え
- Books(ホーム): 小説一覧
  - テーブル(編集不可)
    - ID: 数値
    - 既刊: チェックボックス
    - 既読: チェックボックス
    - 希望: チェックボックス
    - Publish: アイコンボタン
    - Contents: アイコンボタン
    - 書名: テキスト
    - 著者: テキスト
    - 掲載: テキスト
    - 状態: テキスト着色
    - 備考: テキスト
- Publish: 書誌編集、取得・更新、発行、削除
  - 
- Contents: シート一覧
  - 
- Read: シート閲覧、編集
  - 
- Settings
  - 

## おわりに
最後までお読みいただきありがとうございました。
