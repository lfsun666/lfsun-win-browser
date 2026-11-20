# LfsunBrowser

一个基于 **.NET 8 + WinForms + WebView2** 的 Windows 浏览器，面向开发者设计：等宽字体地址栏、一键 DevTools、页面加载耗时显示、查看源码、地址栏命令等。整体遵循"可扩展 + 低内存优先"的实现思路。

> 仓库地址：https://gitee.com/pc-app/lfsun-win-browser

## 功能概览

### 核心浏览
- 多标签浏览（标签拖拽排序、溢出滚动、最近关闭标签恢复 `Ctrl+Shift+T`）
- 地址栏智能识别：输入 `163` 等关键词走默认搜索引擎，输入 `163.com`/`localhost`/IP 直接访问
- 地址栏命令体系：直接输入 `devtools` / `viewsource` / `darkmode` / `reloadplugins` 等指令回车执行
- 自定义搜索引擎：内置 Bing/百度/Google/DuckDuckGo，支持添加关键词引擎（如 `gh 关键词` 搜 GitHub）
- 页内查找 `Ctrl+F`（匹配计数、区分大小写）
- 页面缩放 `Ctrl+滚轮 / Ctrl+±0`，按域名记忆缩放倍率
- 无痕窗口 `Ctrl+Shift+N`（不写历史、独立会话）
- 广告过滤（内置广告/统计域名库 + 自定义黑名单，支持 EasyList 规则语法，状态栏显示拦截计数）

### 开发者向
- `F12` 或工具栏 `</>` 按钮打开开发者工具（Console / Network）
- `Ctrl+U` 新标签查看页面源码（view-source）
- 状态栏显示页面加载耗时芯片（`⏱ 356ms`，按快慢三级着色）
- 地址栏与状态栏数值采用等宽字体（Cascadia Mono → Consolas 自动回退）
- 状态栏显示公网 IP（多源回退）与内存占用

### 数据与管理
- 树形书签（书签/文件夹），书签栏独立一行、可一键隐藏 `Ctrl+B`
- 书签设置页支持增删改查、导入、导出
- 下载管理（进度、状态、来源地址、打开下载目录），下载记录持久化
- 历史记录页（浏览历史 + 最近关闭标签）`Ctrl+H`
- 会话管理：启动恢复上次会话、命名会话保存/恢复/导入导出
- 网站账号密码记录（本机用户加密），登录页自动填充（可开关）
- 快捷键设置页：每个动作独立启用/禁用、改键、冲突检测、恢复默认
- 工具栏按钮可自定义显隐与排序

### 外观
- 明暗主题 + 跟随系统，Mica 质感，多强调色可选
- 新标签页速拨面板
- 品牌 Logo 与 exe 图标（`scripts/gen-app-icon.ps1` 生成）

### 插件系统
- 基于 `manifest.json + JS` 的页面脚本注入
- 插件权限白名单（未声明或超白名单权限会被阻止加载）
- 支持安装（目录/zip）、启用、禁用、卸载、重载（地址栏 `reloadplugins`）

### 低内存策略
- 限制活跃标签页数量（`maxLiveTabs`）
- 空闲标签页自动休眠（`suspendAfterMinutes`），切回自动恢复

## 快捷键速查

| 快捷键 | 功能 | 可改键 |
| --- | --- | --- |
| `Ctrl+T` | 新建标签 | ✅ |
| `Ctrl+W` | 关闭标签 | ✅ |
| `Ctrl+Shift+T` | 恢复最近关闭标签 | ✅ |
| `Ctrl+L` | 聚焦地址栏 | ✅ |
| `Ctrl+F` | 页内查找 | ✅ |
| `Ctrl+D` | 收藏当前页 | ✅ |
| `Ctrl+B` | 显示/隐藏书签栏 | ✅ |
| `Ctrl+H` | 历史记录 | ✅ |
| `F5` | 刷新 | ✅ |
| `F12` | 开发者工具 | ✅ |
| `Ctrl+U` | 查看页面源码 | 固定 |
| `Ctrl+Shift+N` | 无痕窗口 | 固定 |
| `Ctrl+滚轮` / `Ctrl+±0` | 页面缩放 | 固定 |

## 项目结构

- `Lfsun.Win.Browser/Core`：运行配置、地址解析、会话存储、搜索引擎目录
- `Lfsun.Win.Browser/Browser`：标签页生命周期和内存控制
- `Lfsun.Win.Browser/Bookmarks`：书签持久化和管理
- `Lfsun.Win.Browser/Downloads`：下载跟踪和下载窗口
- `Lfsun.Win.Browser/History`：历史记录
- `Lfsun.Win.Browser/Security`：账号密码存储与自动填充
- `Lfsun.Win.Browser/Sessions`：命名会话管理
- `Lfsun.Win.Browser/Settings`：综合设置页
- `Lfsun.Win.Browser/UI`：主题、图标、自绘标签控件等
- `Lfsun.Win.Browser/Plugins`：插件加载、匹配、权限校验、脚本注入
- `Lfsun.Win.Browser.Tests`：xUnit 单元测试
- `scripts`：打包与图标生成脚本
- `packaging`：安装包脚本（Inno Setup）

## 环境要求

- Windows 10/11
- .NET SDK 8.0+
- WebView2 Runtime（目标机器也需要）

## 快速开始

```powershell
git clone https://gitee.com/pc-app/lfsun-win-browser.git
cd lfsun-win-browser
dotnet run --project .\Lfsun.Win.Browser\Lfsun.Win.Browser.csproj
```

运行测试：

```powershell
dotnet test .\LfsunWin.sln
```

## 配置说明

配置文件位置：`%LocalAppData%\LfsunBrowser\config\browser-config.json`
（首次运行时会自动从旧版程序目录下的 `browser-config.json` 迁移。）

常用字段（节选）：

```json
{
  "startPage": "about:blank",
  "maxLiveTabs": 3,
  "suspendAfterMinutes": 3,
  "pluginsDirectoryName": "plugins",
  "enforcePluginPermissions": true,
  "allowedPluginPermissions": ["content:dom_read", "content:dom_write", "content:inject"],
  "isGeneralAutofillEnabled": true,
  "isPasswordAutosaveEnabled": true,
  "isCredentialAutofillEnabled": true,
  "restoreLastSessionOnStartup": true,
  "defaultSearchEngineName": "Bing",
  "defaultSearchEngineUrlTemplate": "https://www.bing.com/search?q={query}",
  "showBookmarksBar": true,
  "uiTheme": "Light",
  "adBlockEnabled": false,
  "downloadDirectory": "",
  "disableWebSecurity": false,
  "additionalBrowserArguments": "",
  "fetchProxyUrl": "",
  "fetchUserAgents": [
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15"
  ],
  "fetchRetry": 2,
  "fetchRetryDelayMs": 500,
  "fetchSsrfGuardEnabled": true,
  "shortcutDevToolsEnabled": true,
  "shortcutDevTools": "F12"
}
```

字段说明：

- `startPage`：启动页
- `maxLiveTabs`：最多保持活跃的标签页数量（超出会休眠旧标签页）
- `suspendAfterMinutes`：空闲多少分钟后自动休眠
- `pluginsDirectoryName`：插件目录名称（相对程序目录）
- `enforcePluginPermissions` / `allowedPluginPermissions`：插件权限白名单开关与列表（默认含 `content:dom_read`/`content:dom_write`/`content:inject`/`net:fetch`；`net:fetch` 用于插件跨域抓取，见下文）
- `disableWebSecurity`：开发调试用，启动时为 WebView2 追加 `--disable-web-security`，关闭同源策略/跨域限制（**仅自用浏览器，默认关闭，开启会降低安全性**）
- `additionalBrowserArguments`：追加到 WebView2 启动参数的自由文本（空格分隔），例如 `"--disable-web-security --proxy-server=http://127.0.0.1:7890"`，与 `disableWebSecurity` 叠加生效
- `fetchProxyUrl`：跨域抓取桥（`chrome.net.fetch`）的**全局默认代理**（如 `"http://127.0.0.1:7890"`），留空直连；插件也可在 `init.proxy` 按请求临时覆盖
- `fetchUserAgents`：跨域抓取桥的 **User-Agent 候选池**；插件开启「轮换 UA」后按请求轮流使用，降低被单一 UA 限流的概率
- `fetchRetry`：跨域抓取桥**默认重试次数**（1~5，仅网络层异常重试，HTTP 4xx/5xx 不重试）
- `fetchRetryDelayMs`：重试之间的退避间隔（毫秒，0~5000）
- `fetchSsrfGuardEnabled`：跨域抓取 SSRF 防护（默认开启）：拦截环回/内网/链路本地目标（含 `169.254.169.254` 云元数据）及解析到内网的域名，仅允许 http/https；需要抓取本机或局域网（如本地演示站 `127.0.0.1:8090`）时设为 `false`
- `isGeneralAutofillEnabled` / `isPasswordAutosaveEnabled` / `isCredentialAutofillEnabled`：自动填充相关开关
- `restoreLastSessionOnStartup`：启动时恢复上次会话
- `defaultSearchEngineName` / `defaultSearchEngineUrlTemplate`：默认搜索引擎（模板必须包含 `{query}`）
- `uiTheme`：`Light` / `Dark` / `System`
- `adBlockEnabled`：是否启用广告过滤
- `adBlockCustomDomains`：自定义屏蔽列表；每行支持两种写法——纯域名（`example.com`，后缀匹配含子域）或 EasyList 规则（`||example.com^` 屏蔽、`@@||example.com^` 例外放行，`$script`/`$third-party` 等修饰符自动剥离，`!`/`#` 注释行忽略；例外规则优先级最高，可覆盖内置库）
- IPv6 地址：地址栏可直接输入 IPv6 字面量（`::1`、`2001:db8::1`、`[::1]:8080`），自动按 URL 导航
- `downloadDirectory`：下载目录；为空时默认使用系统"下载"文件夹
- `shortcut*Enabled` / `shortcut*`：各快捷键开关与按键组合（格式如 `Ctrl+D` / `Ctrl+Shift+L` / `F12`）
- `toolbarButtons` / `addressCommands`：工具栏按钮与地址栏命令布局（建议通过设置页修改）

## 插件开发

插件目录格式：`plugins/<plugin-id>/manifest.json`

```json
{
  "id": "sample-plugin",
  "name": "Sample Plugin",
  "enabled": true,
  "matches": ["<all_urls>", "https://*.example.com/*"],
  "permissions": ["content:dom_read", "content:dom_write"],
  "scripts": ["inject.js"]
}
```

说明：

- `matches` 支持 `*` 通配符和 `<all_urls>`
- `scripts` 中的 JS 在页面导航完成后注入
- 开启 `enforcePluginPermissions=true` 时，插件声明的每个权限都必须在 `allowedPluginPermissions` 内
- 可在"设置 -> 插件"中安装、启用、禁用、卸载插件

### 跨域抓取（net:fetch）

普通插件在页面里用 `fetch` 受浏览器同源/CORS 限制，只能请求当前站点。声明 `net:fetch` 权限后，可通过宿主桥 `chrome.net.fetch(url, init)` 让**浏览器进程（C# / HttpClient）代为发请求**，从而抓取任意站点，彻底绕过 CORS。

```json
{
  "id": "web-scraper",
  "permissions": ["content:dom_read", "content:dom_write", "content:inject", "net:fetch"],
  "contentScripts": [{ "matches": ["<all_urls>"], "scripts": ["inject.js"] }]
}
```

```js
// inject.js（内容脚本，main world 注入）
chrome.net.fetch("https://other-site.com/page", {
  method: "GET",
  headers: { "Accept": "text/html" },
  timeoutMs: 30000,
  retry: 3,            // 网络层异常重试次数（1~5，HTTP 4xx/5xx 不重试）
  retryDelayMs: 500,   // 重试退避间隔（ms）
  rotateUA: true,      // 轮换 User-Agent（取自配置候选池 fetchUserAgents）
  proxy: "http://127.0.0.1:7890", // 按请求指定代理（null/缺省则用全局 fetchProxyUrl）
  userAgent: null      // 显式 UA（优先级高于轮换；null/缺省时按 rotateUA 决定）
}).then(function (res) {
  // res: { ok, status, statusText, headers, body, encoding, truncated, arrayBuffer? }
  console.log(res.status, res.body); // body 为响应文本（文本类）或 base64（二进制）
});
```

说明：

- `chrome.net.fetch` 返回 Promise；文本类响应 `encoding:"utf8"`、`body` 为字符串，二进制 `encoding:"base64"` 并附带 `arrayBuffer`。
- 宿主侧有安全护栏：单请求超时 1~120s、响应体积上限 8MB（超出截断并置 `truncated:true`）、权限 `net:fetch` 门控（插件须声明且白名单允许）、SSRF 防护（`fetchSsrfGuardEnabled`，默认拦截内网/环回/链路本地目标）。
- **令牌校验**：宿主为每个 WebView 生成随机安装令牌，页面桥消息必须携带；网页脚本读不到令牌，无法伪造插件身份调用 `chrome.net.fetch`/`storage`/`openUrl` 等宿主能力。
- **重试**：仅对网络层异常（连接失败/超时/DNS 解析失败）重试，HTTP 4xx/5xx 视为业务结果不重试；次数与退避由 `retry`/`retryDelayMs` 控制，缺省回退全局 `fetchRetry`/`fetchRetryDelayMs`。
- **代理**：留空直连；可按请求 `proxy` 覆盖，否则用全局 `fetchProxyUrl`；代理 `HttpClient` 按地址缓存复用。
- **UA 轮换**：`rotateUA:true` 时按请求轮流使用 `fetchUserAgents` 候选池；若显式给了 `userAgent` 则优先使用该值。
- 若只是想全局关闭同源限制，也可在设置里开 `disableWebSecurity`（启动参数 `--disable-web-security`），但安全性更低，仅建议自用浏览器开启。

## 打包发布与版本交付

### 1) 一键自动化发布（推荐）

集成了**版本号自增、代码质量门禁 (单元测试)、绿色便携包与 Inno Setup 安装包打包、SHA256 校验和生成、更新日志自动归档**于一体，直接产出到 `release/` 目录：

```powershell
# 自动递增补丁版本并发布（如 0.3.0 -> 0.3.1）
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version patch -Notes "优化了若干体验并修复已知问题"

# 或指定具体版本号发布当前版本到 release/
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version current
```

或直接执行批处理：

```cmd
scripts\release.cmd -Version patch
```

**产出目录 `release/` 包含：**
- `LfsunBrowser-setup-{Version}.exe`：Windows 一键安装程序
- `LfsunBrowser-setup-latest.exe`：最新版安装程序副本
- `LfsunBrowser-portable-win-x64-v{Version}.zip`：绿色免安装便携版
- `LfsunBrowser-portable-latest.zip`：最新版便携包副本
- `SHA256SUMS.txt`：发布文件的 SHA-256 完整性校验和
- `RELEASE_NOTES_v{Version}.md`：本次发布说明

每次版本迭代说明同时自动归档记录于根目录 `CHANGELOG.md` 及应用内「关于与更新日志」窗口。

### 1) 绿色免安装版（推荐先用）

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable.ps1
```

或直接双击：`scripts\build-portable.cmd`

输出：

- `artifacts\portable\win-x64\`（可直接运行目录）
- `artifacts\portable\LfsunBrowser-portable-win-x64.zip`

### 2) 安装包（Inno Setup）

先安装 Inno Setup，并确保 `iscc` 在 PATH 中：
https://jrsoftware.org/isinfo.php

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Version 0.1.0
```

或执行：`scripts\build-installer.cmd 0.1.0`

输出：

- `artifacts\installer\LfsunBrowser-setup-0.1.0.exe`

安装脚本文件在：`packaging/LfsunWin.iss`

## 常见问题

### 1. `MSBUILD : error MSB1009: 项目文件不存在`

原因：当前目录不对，使用了相对路径。
解决：先 `cd` 到仓库目录，或使用 `--project` 绝对路径。

### 2. 构建有 `MSB3277 (WindowsBase)` 警告

目前不影响运行和打包。属于 WebView2 引入的引用版本警告，可暂时忽略。

### 3. 启动时报 WebView2 相关错误

目标机器缺少 WebView2 Runtime，安装后重试即可。

## 参与贡献

1. Fork 本仓库并创建特性分支
2. 提交前请确保 `dotnet build` 无错误、`dotnet test` 全部通过
3. 提交 Pull Request 并描述变更内容

## 许可证

本项目仅供学习与研究使用。

> AI生成
