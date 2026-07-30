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
- 广告过滤（简版规则 + 自定义域名黑名单，状态栏显示拦截计数）

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
  "shortcutDevToolsEnabled": true,
  "shortcutDevTools": "F12"
}
```

字段说明：

- `startPage`：启动页
- `maxLiveTabs`：最多保持活跃的标签页数量（超出会休眠旧标签页）
- `suspendAfterMinutes`：空闲多少分钟后自动休眠
- `pluginsDirectoryName`：插件目录名称（相对程序目录）
- `enforcePluginPermissions` / `allowedPluginPermissions`：插件权限白名单开关与列表
- `isGeneralAutofillEnabled` / `isPasswordAutosaveEnabled` / `isCredentialAutofillEnabled`：自动填充相关开关
- `restoreLastSessionOnStartup`：启动时恢复上次会话
- `defaultSearchEngineName` / `defaultSearchEngineUrlTemplate`：默认搜索引擎（模板必须包含 `{query}`）
- `uiTheme`：`Light` / `Dark` / `System`
- `adBlockEnabled`：是否启用广告过滤
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

## 打包发布

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
