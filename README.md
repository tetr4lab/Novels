---
title: Web小説の取得と発行
tags: epub webscraping smtp-mail blazor
---

# Web小説の取得と発行
## はじめに
これは極めて個人的なプロジェクトです。
実装がBlazor Serverになっていて、Blazor WASM+SQLiteのようなスタンドアロンアプリにしなかったのは、個人的な都合です。

### 目的
Web小説を取得し、EPUBを生成して、Kindleパーソナルドキュメント向けに発行(Send To Kindle)します。

#### 経緯
元々、FileMakerとAozora Epub3で実現していたものをBlazorで置き換えました。

### 環境
#### ビルド
- .NET 8.0
  - Microsoft.AspNetCore.Authentication.Google 8.0.18
  - Microsoft.AspNetCore.Authorization 8.0.18
- MudBlazor 8.12.0
- PetaPoco 6.0.683
- MySqlConnector 2.4.0
- [AngleSharp](https://github.com/AngleSharp/AngleSharp) 1.3.0
  - The ultimate angle brackets parser library parsing HTML5, MathML, SVG and CSS to construct a DOM based on the official W3C specifications.
- [MailKit](https://github.com/jstedfast/MailKit) 4.13.0
  - A cross-platform .NET library for IMAP, POP3, and SMTP.
- [QuickEPUB](https://github.com/tetr4lab/QuickEPUB/tree/feature/spine-page-progression)
  - Feat: Implement page-progression-direction attribute for spine. Users can now define the reading direction (LTR or RTL) within the spine element of the EPUB.
- [Tetr4labNugetPackages](https://github.com/tetr4lab/Tetr4labNugetPackages)

#### サーバ

https://zenn.dev/tetr4lab/articles/ad947ade600764

https://zenn.dev/tetr4lab/articles/3fb1d4e8e7ff21

#### その他
##### メールサーバ
さくらインターネット (TLS, SMTP認証)

##### ブラウザ
Google Chrome

##### 認証

https://zenn.dev/tetr4lab/articles/1946ec08aec508

https://github.com/tetr4lab/BlazorGoogleOAuthMinimal

###### 認証を除去するには
- `Novels/Novels/Components/Pages/AccessDenied.razor`
  - 削除
- `Novels/Novels/Components/Layout/MainLayout.razor`
  - `<AuthorizeView~>`、`<Authorized>`と`</AuthorizeView>`、`</Authorized>`を削除 (コンテンツは残す)
  - `<NotAuthorized>`~`</NotAuthorized>`を削除
- `Novels/Novels/Components/Routes.razor`
  - `<AuthorizeRouteView>`を`<RouteView>`に変更
- `Novels/Novels/Components/_Imports.razor`
  - 以下を削除
    ```csharp:Novels/Novels/Components/_Imports.razor
    @using Microsoft.AspNetCore.Authentication
    @using Microsoft.AspNetCore.Authentication.Cookies
    @using Microsoft.AspNetCore.Authorization
    @using Microsoft.AspNetCore.Components.Authorization
    @attribute [Authorize (Policy = "Users")]
    ```
- `Novels/Novels/Program.cs`
  - 以下を削除
    ```csharp:Novels/Novels/Program.cs
    // クッキーとグーグルの認証を構成
    builder.Services.AddAuthentication (
        builder.Configuration ["Authentication:Google:ClientId"]!,
        builder.Configuration ["Authentication:Google:ClientSecret"]!
    );

    // メールアドレスを保持するクレームを要求する認可用のポリシーを構成
    await builder.Services.AddAuthorizationAsync (
        $"database=accounts;{builder.Configuration.GetConnectionString ("Host")}{builder.Configuration.GetConnectionString ("Account")}Allow User Variables=true;",
        new () {
            { "Admin", "Administrator" },
            { "Users", "Private" },
        }
    );
    ```

## できること
- Web小説の取得・更新 (なろう、ノクターン、カクヨム、ノベルアップ他)
- 小説内容の確認、各種メモ、文字校正
- EPUBの生成、取得
- Kindleパーソナルドキュメント向け送信

## データ構造
### 論理構成
- 書誌
  - 書誌と目次
- シート
  - 発表単位の本文
- 設定
  - 取得設定、発行設定

### テーブルスキーマ
https://github.com/tetr4lab/Novels/blob/main/Novels/Novels/novels_init.sql

## 画面と機能
当初、複数ページで作成したものをSPA化したので、名残があります。

![](ScreenShot.png)

### 構成
- 共通: 上部ナビゲーションバー
  - ページ切り替え、検索、テーマ切り替え
- ページ
  - Books(ホーム)
    - 書誌一覧、新規作成
  - Issue
    - 書誌編集、校正、取得・更新、発行、削除
  - Contents
    - シート一覧
  - Read
    - シート閲覧、校正
  - Settings
    - 設定

## おわりに
お読みいただきありがとうございました。
