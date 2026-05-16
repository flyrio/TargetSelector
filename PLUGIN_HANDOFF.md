# TargetSelector 插件收录交接文档

本文档用于交接“新增/更新插件收录”的标准流程，尤其适用于像 `AutoFollow` 这种直接从独立 GitHub 仓库发布页收录的插件。

## 0. 当前清单状态与交接重点

截至当前工作区，`TargetSelector.json` 中共有 **9 个插件**，全部为 **Dalamud API 15**。

### 0.1 当前从 `MyDalamudRepo` 自动同步的插件

这 7 个插件由 `scripts/sync_sources.json` + `scripts/sync_plugin_sources.py` 管理版本号、API 等级和下载链接：

1. `DalamudACT`
2. `PluginDockStandalone`
3. `PartyIcons`
4. `Saucy`
5. `StarlightBreaker`
6. `WondrousTailsSolver`
7. `日随伴侣卫月版`

### 0.2 当前本仓库手动保留的插件

这 2 个插件当前不在 `scripts/sync_sources.json` 里，按手动条目维护：

1. `AutoFollow`
2. `ActionTimelineReborn`

### 0.3 已清理的 API 14 自有条目

以下旧自有条目已经从 `TargetSelector.json` 删除，不要在未确认 API 15 可用版本前重新加回订阅源：

1. `TargetSelector`
2. `DeathRecap`
3. `DeathBuffTracker`
4. `Coyote-FFXiv`
5. `MitigationPolice`

如果以后要恢复其中任何插件，必须先确认：

- 有适配当前 Dalamud 的 API 15 构建。
- `DalamudApiLevel` 和 `TestingDalamudApiLevel` 都应为 15，或符合当时 Dalamud 官方要求。
- `DownloadLinkInstall`、`DownloadLinkUpdate`、`DownloadLinkTesting` 都是可直接下载的 zip 链接。
- 插件 JSON 可解析，且中文字段没有被写成 `????` 或 mojibake。
- 恢复后同步更新 `README.md` 的“当前仓库内其他保留条目”列表。

### 0.4 最重要的注意事项

1. **当前订阅源只保留 API 15 插件。**
   - 删除 API 14 条目是有意为之。
   - 不要只因为旧仓库还有 release，就把 API 14 条目重新放回 `TargetSelector.json`。

2. **同步脚本不会自动新增插件。**
   - `scripts/sync_plugin_sources.py` 只会更新 `TargetSelector.json` 里已经存在的条目。
   - 新增来自 `MyDalamudRepo` 的插件时，必须先补完整 JSON 条目，再改 `scripts/sync_sources.json`。

3. **同步脚本只覆盖部分字段。**
   - 会覆盖：`AssemblyVersion`、`ApplicableVersion`、`DalamudApiLevel`、`TestingDalamudApiLevel`、下载链接字段。
   - 不会覆盖：`Name`、`Author`、`Description`、`Punchline`、`IconUrl`、`Tags`、`RepoUrl`。
   - 如果本仓库有中文说明、图标、标签等定制内容，需要手动维护。

4. **手动插件不要随便加入同步配置。**
   - `AutoFollow`、`ActionTimelineReborn` 当前按手动条目维护。
   - 除非确认存在稳定上游 manifest，且同步脚本能正确同步，否则不要把它们加入 `scripts/sync_sources.json`。
   - `AutoFollow` 正常情况下应优先使用官方 release zip；只有上游 release 包本身损坏时，才临时使用本仓库 `packages/AutoFollow_*.zip` 修复包。
   - 如果临时使用本仓库 `packages/AutoFollow_*.zip`，必须确认 zip 内 `AutoFollow.json` 的 `AssemblyVersion` 与 `TargetSelector.json` 一致；上游 release 修复后优先切回官方 release zip，并删除不再使用的临时包。

5. **下载链接必须是直接 zip 链接。**
   - 三个字段通常填同一个 zip：
     - `DownloadLinkInstall`
     - `DownloadLinkUpdate`
     - `DownloadLinkTesting`
   - 不要填 GitHub release 页面地址。

6. **图标链接必须是可直接访问的图片。**
   - 推荐 raw 链接。
   - 不要填 GitHub `blob` 页面链接。

7. **不要靠 PowerShell 控制台判断中文是否正常。**
   - PowerShell 里看到乱码，不一定代表文件坏了。
   - 必须用 Python 按 UTF-8/UTF-8-SIG 读取验证。

8. **提交前必须做基础校验。**
   - 优先运行 `python scripts/validate_targetselector.py`。
   - `TargetSelector.json` 能被 Python `json.loads` 解析。
   - 当前不应出现 API 14 条目，除非明确就是要恢复且已经确认可用。
   - `README.md`、`UPDATE.md`、`PLUGIN_HANDOFF.md` 保持 UTF-8 with BOM。
   - `TargetSelector.json`、`scripts/sync_sources.json` 保持 UTF-8 无 BOM。

## 1. 文件与编码约定

本仓库涉及插件收录时，重点关注这些文件：

1. `TargetSelector.json`
   - 作用：实际提供给 Dalamud 订阅源使用的插件清单。
   - 编码：UTF-8，无 BOM。
   - 注意：不要用 PowerShell 控制台里看到的中文来判断文件是否乱码，控制台可能会显示异常。

2. `scripts/sync_sources.json`
   - 作用：自动同步上游来源的配置。
   - 编码：UTF-8，无 BOM。
   - 注意：只给确实由同步脚本管理的插件添加来源，不要把所有手动收录插件都塞进去。

3. `README.md`
   - 作用：完整说明文档，并链接本交接文档。
   - 编码：UTF-8 with BOM。

4. `UPDATE.md`
   - 作用：日常快速更新速查。
   - 编码：UTF-8 with BOM。

建议用下面命令检查文件编码头：

```powershell
python -c "from pathlib import Path; paths=['README.md','UPDATE.md','PLUGIN_HANDOFF.md','TargetSelector.json','scripts/sync_sources.json']; [print(s, Path(s).read_bytes()[:3].hex()) for s in paths]"
```

期望结果：

- `README.md`：`efbbbf`
- `UPDATE.md`：`efbbbf`
- `PLUGIN_HANDOFF.md`：`efbbbf`
- `TargetSelector.json`：通常以 `5b0d0a` 开头，即 `[` + CRLF，无 BOM
- `scripts/sync_sources.json`：通常以 `7b0d0a` 开头，即 `{` + CRLF，无 BOM

## 2. 新增插件前先判断类型

新增插件前，先判断它属于哪一种。

无论哪一种，当前仓库默认只收录 **Dalamud API 15** 插件。遇到 API 14 或更旧的条目，除非已经确认当前 Dalamud 仍允许且仓库维护者明确要求保留，否则不要加入订阅源。

### 类型 A：来自 `MyDalamudRepo` 且需要自动同步

这种插件要做两件事：

1. 先把完整插件条目补进 `TargetSelector.json`。
2. 再把该插件的 `InternalName` 加进 `scripts/sync_sources.json` 的 `plugins` 列表。

原因：当前同步脚本只更新 `TargetSelector.json` 里已经存在的条目，不会自动新增条目。如果只改 `sync_sources.json`，脚本会跳过。

### 类型 B：独立仓库手动收录

例如：

- `https://github.com/wang3x/AutoFollow`

这种插件通常只需要改 `TargetSelector.json`，不要加入 `scripts/sync_sources.json`，除非你已经给它配置了稳定的上游 JSON 清单，并确认同步脚本能处理。

## 3. 独立 GitHub 仓库手动收录流程

以 `AutoFollow` 为例。

### 3.1 获取插件元数据

优先读取插件仓库里的 Dalamud manifest，例如：

```powershell
Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/wang3x/AutoFollow/main/AutoFollow.json' | Select-Object -ExpandProperty Content
```

需要确认这些字段：

- `Name`
- `Author`
- `Punchline`
- `Description`
- `InternalName`
- `AssemblyVersion`
- `DalamudApiLevel`
- `RepoUrl`
- `Tags`

如果 manifest 缺少 `ApplicableVersion`，本仓库通常填：

```json
"ApplicableVersion": "any"
```

如果 manifest 缺少 `TestingDalamudApiLevel`，通常和 `DalamudApiLevel` 保持一致。

### 3.2 获取最新 tag

可用命令：

```powershell
git ls-remote --tags https://github.com/wang3x/AutoFollow.git
```

例如 `AutoFollow` 当前确认到最新 tag：

```text
v1.4.5
```

### 3.3 确认 release zip 文件名

如果 GitHub API 被限流，可以直接读取发布页或资产展开页：

```powershell
$html = (Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/wang3x/AutoFollow/releases/expanded_assets/v1.4.5').Content
[regex]::Matches($html, 'href="([^"]*releases/download/[^"]*AutoFollow_v1\.4\.5\.zip)"') | ForEach-Object { $_.Groups[1].Value }
```

`AutoFollow` 的 zip 路径是：

```text
/wang3x/AutoFollow/releases/download/v1.4.5/AutoFollow_v1.4.5.zip
```

最终填到 `TargetSelector.json` 的完整链接：

```text
https://github.com/wang3x/AutoFollow/releases/download/v1.4.5/AutoFollow_v1.4.5.zip
```

三个下载字段都填同一个链接：

- `DownloadLinkInstall`
- `DownloadLinkTesting`
- `DownloadLinkUpdate`

### 3.4 编辑 `TargetSelector.json`

新增条目一般追加到数组末尾即可。

示例：

```json
{
    "Author": "XM.",
    "Name": "强效跟随",
    "InternalName": "AutoFollow",
    "Punchline": "vnavmesh寻路跟随，自动疾跑，与循环插件协作",
    "Description": "基于vnavmesh的智能自动跟随插件。自动疾跑追赶，通过陌语命令与循环插件协同工作。支持自定义命令、紧急停止热键、调试日志窗口。",
    "AssemblyVersion": "1.4.5.0",
    "RepoUrl": "https://github.com/wang3x/AutoFollow",
    "ApplicableVersion": "any",
    "DalamudApiLevel": 15,
    "TestingDalamudApiLevel": 15,
    "Tags": [
        "follow",
        "auto",
        "dungeon",
        "vnavmesh",
        "ipc"
    ],
    "DownloadLinkInstall": "https://github.com/wang3x/AutoFollow/releases/download/v1.4.5/AutoFollow_v1.4.5.zip",
    "DownloadLinkTesting": "https://github.com/wang3x/AutoFollow/releases/download/v1.4.5/AutoFollow_v1.4.5.zip",
    "DownloadLinkUpdate": "https://github.com/wang3x/AutoFollow/releases/download/v1.4.5/AutoFollow_v1.4.5.zip"
}
```

注意事项：

- 如果上游没有图标，不必强行填 `IconUrl`。当前仓库已有无图标条目的先例。
- 如果补了 `IconUrl`，必须使用能直接访问的图片原始链接，避免使用 GitHub `blob` 页面链接。
- 不要把独立手动收录插件误加到 `scripts/sync_sources.json`。

## 4. 中文与 JSON 校验

### 4.1 校验 JSON 可解析

```powershell
python -c "import json; from pathlib import Path; obj=json.loads(Path('TargetSelector.json').read_text(encoding='utf-8-sig')); print(len(obj))"
```

### 4.2 验证指定插件条目

```powershell
python -c "import json; from pathlib import Path; obj=json.loads(Path('TargetSelector.json').read_text(encoding='utf-8-sig')); x=[i for i in obj if i.get('InternalName')=='AutoFollow'][0]; print(json.dumps(x, ensure_ascii=False, indent=2))"
```

如果 PowerShell 控制台显示中文异常，可以改用 `ensure_ascii=True` 看 Unicode 转义，确认文件里不是问号：

```powershell
python -c "import json; from pathlib import Path; obj=json.loads(Path('TargetSelector.json').read_text(encoding='utf-8-sig')); x=[i for i in obj if i.get('InternalName')=='AutoFollow'][0]; print(json.dumps(x, ensure_ascii=True, indent=2))"
```

如果输出里出现真正的 `????`，说明中文已经被写坏，需要用 Unicode 转义或支持 UTF-8 的编辑器重新写入。

## 5. 更新 README / UPDATE

新增插件后，建议同步更新文档：

1. `README.md`
   - 如果是自动同步插件，更新“当前从 MyDalamudRepo 自动同步的插件”列表和数量。
   - 如果是手动收录插件，更新“当前仓库内其他保留条目”列表。
   - 保持 UTF-8 with BOM。

2. `UPDATE.md`
   - 如需日常速查，也同步补充对应列表。
   - 保持 UTF-8 with BOM。

## 6. 检查改动

```powershell
git status --short --untracked-files=all
git diff --stat
git diff -- TargetSelector.json README.md UPDATE.md PLUGIN_HANDOFF.md
```

确认：

- 没有临时目录或临时 HTML 文件残留。
- `TargetSelector.json` JSON 可解析。
- 中文字段没有变成 `????`。
- `README.md`、`UPDATE.md` 和 `PLUGIN_HANDOFF.md` 是 UTF-8 with BOM。
- `TargetSelector.json` 和 `scripts/sync_sources.json` 是 UTF-8 无 BOM。

## 7. 提交建议

如果只是新增一个手动收录插件：

```powershell
git add -- TargetSelector.json README.md UPDATE.md PLUGIN_HANDOFF.md
git commit -m "chore: add AutoFollow plugin"
git push origin main
```

如果还更新了同步来源配置，则额外加入：

```powershell
git add -- scripts/sync_sources.json
```

## 8. 常见坑

1. **GitHub API 限流**
   - 可以不用 API，改用 `git ls-remote --tags`、raw manifest、release HTML 或 `expanded_assets` 页面。

2. **PowerShell 中文显示异常**
   - 不要仅凭控制台显示判断文件乱码。
   - 用 Python 按 UTF-8 读取并检查。

3. **中文被写成问号**
   - 通常是通过非 UTF-8 管道传递中文导致。
   - 可以用 Unicode 转义写入，或使用明确支持 UTF-8 的编辑器/脚本。

4. **误把手动插件加入同步配置**
   - 只有确认存在稳定上游插件清单且脚本能同步时，才改 `scripts/sync_sources.json`。

5. **IconUrl 填错**
   - GitHub 图片要用 raw 链接，不要用 `blob` 链接。

6. **清理临时 Git 克隆目录失败**
   - Windows 下 `.git` 目录可能有隐藏/只读属性，必要时先取消属性再删除。
