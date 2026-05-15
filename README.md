# TargetSelector

这个仓库提供 Dalamud 插件订阅源：

`https://raw.githubusercontent.com/flyrio/TargetSelector/main/TargetSelector.json`

如果你只想快速更新，先看 [`UPDATE.md`](UPDATE.md)；如果要交接、新增、删除或手动维护插件，先看 [`PLUGIN_HANDOFF.md`](PLUGIN_HANDOFF.md)；这里保留完整说明和防乱码细节。

## 当前文件角色

这个仓库里和发布/同步最相关的文件有：

1. `TargetSelector.json`：实际提供给 Dalamud 订阅的插件清单
2. `scripts/sync_sources.json`：上游来源配置
3. `scripts/sync_plugin_sources.py`：同步脚本
4. `/.github/workflows/sync-plugin-sources.yml`：GitHub Actions 自动同步工作流
5. [`PLUGIN_HANDOFF.md`](PLUGIN_HANDOFF.md)：新增/删除/交接插件收录流程与注意事项

---

## 交接文档与关键注意事项

交接、新增、删除或手动维护插件前，请先阅读：

[`PLUGIN_HANDOFF.md`](PLUGIN_HANDOFF.md)

最容易踩坑的点：

- 当前清单只保留 **Dalamud API 15** 插件，不要把已删除的 API 14 自有条目重新加回订阅源。
- 来自 `MyDalamudRepo` 的插件：先在 `TargetSelector.json` 里有完整条目，再把 `InternalName` 加到 `scripts/sync_sources.json`；同步脚本不会自动新增条目。
- 手动维护插件不要随便加入 `scripts/sync_sources.json`，除非确认有稳定上游 manifest 且同步脚本能处理。
- `README.md`、`UPDATE.md`、`PLUGIN_HANDOFF.md` 保持 UTF-8 with BOM；`TargetSelector.json` 和 `scripts/sync_sources.json` 保持 UTF-8 无 BOM。
- 不要用 PowerShell 控制台肉眼判断 JSON 中文是否乱码，必须用 Python 按 UTF-8/UTF-8-SIG 读取验证。

---

## 当前从 MyDalamudRepo 自动同步的插件

目前会从 `MyDalamudRepo` 自动同步这 7 个插件：

1. `DalamudACT`
2. `PluginDockStandalone`
3. `PartyIcons`
4. `Saucy`
5. `StarlightBreaker`
6. `WondrousTailsSolver`
7. `日随伴侣卫月版`

上游来源地址：

`https://raw.githubusercontent.com/anmili2022/MyDalamudRepo/main/pluginmaster.json`

## 当前仓库内其他保留条目

除了上面 7 个会自动同步的插件，当前 `TargetSelector.json` 里还保留这些本仓库自己的 API 15 条目：

1. `AutoFollow`
2. `ActionTimelineReborn`

---

## 自动同步说明

这个仓库已经接了 GitHub Actions 自动同步：

- 工作流名称：`Sync Plugin Sources`
- 手动触发：仓库 `Actions` 页面里的 `workflow_dispatch`
- 定时触发：**每 6 小时** 自动检查一次
- 工作流文件：`/.github/workflows/sync-plugin-sources.yml`
- 实际同步脚本：`/scripts/sync_plugin_sources.py`
- 上游来源配置：`/scripts/sync_sources.json`

### 这个脚本会同步哪些字段

当前脚本会从上游清单同步这些字段：

- `AssemblyVersion`
- `ApplicableVersion`
- `DalamudApiLevel`
- `TestingDalamudApiLevel`
- `DownloadLinkInstall`
- `DownloadLinkUpdate`
- `DownloadLinkTesting`

### 这个脚本不会自动同步哪些字段

下面这些字段，当前脚本**不会**自动覆盖：

- `Name`
- `Author`
- `Description`
- `Punchline`
- `IconUrl`
- `Tags`
- `RepoUrl`

也就是说：

- 如果你只是想跟上游版本号和下载链接，直接跑同步脚本就够了
- 如果你还想改中文文案、图标、标签或仓库地址，需要手动编辑 `TargetSelector.json`

---

# 下次更新的最快流程

目标：

- 尽量少手动改文件
- 尽量减少和远端冲突
- 尽量避免中文乱码判断失误

## 1. 进入仓库并同步远端

```powershell
cd E:\git\TargetSelector
git fetch origin main
git rebase origin/main
```

## 2. 跑同步脚本

```powershell
python scripts/sync_plugin_sources.py
```

如果配置没问题，而且上游没有新变化，脚本会输出：

```text
no changes
```

如果有变化，会更新 `TargetSelector.json`。

## 3. 更新后检查

```powershell
git status --short --untracked-files=all
git diff --stat
```

建议用 Python 验证当前同步的 7 个插件版本：

```powershell
python --% -c "import json; from pathlib import Path; obj=json.loads(Path(r'E:\git\TargetSelector\TargetSelector.json').read_text(encoding='utf-8-sig')); names=('DalamudACT','PluginDockStandalone','PartyIcons','Saucy','StarlightBreaker','WondrousTailsSolver','日随伴侣卫月版'); print('\\n'.join('{} {}'.format(i['InternalName'], i['AssemblyVersion']) for i in obj if i.get('InternalName') in names))"
```

## 4. 提交并推送

```powershell
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: sync plugin sources"
git push origin main
```

---

# 新增一个来自 MyDalamudRepo 的插件

这是这个仓库和 `MyDalamudRepo` 最大的区别：

**只把插件名加进 `scripts/sync_sources.json` 还不够。**

因为当前同步脚本的逻辑是：

- 先在 `TargetSelector.json` 里找到已有条目
- 再只更新其中的版本号、API 和下载链接字段

如果你只改了 `scripts/sync_sources.json`，但 `TargetSelector.json` 里没有对应条目，脚本会提示类似：

```text
skip: SomePlugin is not present in TargetSelector.json
```

所以新增一个来自 `MyDalamudRepo` 的插件，正确流程是：

## 1. 先从 MyDalamudRepo 复制完整插件条目

来源文件：

`E:\git\MyDalamudRepo\pluginmaster.json`

把对应插件的完整 JSON 条目复制到：

`E:\git\TargetSelector\TargetSelector.json`

## 2. 再把插件名加进同步来源配置

编辑：

`E:\git\TargetSelector\scripts\sync_sources.json`

在 `MyDalamudRepo` 的 `plugins` 列表里加上对应 `InternalName`。

## 3. 再跑一次同步脚本

```powershell
python scripts/sync_plugin_sources.py
```

## 4. 如果需要本仓库自己的文案/图标，再手动改

因为当前脚本不会覆盖：

- `Name`
- `Description`
- `Punchline`
- `IconUrl`
- `Tags`
- `RepoUrl`

如果你想保留 `TargetSelector` 仓库自己的中文文案、图标或来源地址，就在同步后再手动调整这些字段。

## 5. 最后提交推送

```powershell
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: add synced plugin"
git push origin main
```

---

# 如果 push 被拒绝

如果出现远端先更新、导致 `push` 被拒绝：

```powershell
git fetch origin main
git rebase origin/main
```

然后重新跑一次同步脚本：

```powershell
python scripts/sync_plugin_sources.py
```

再重新提交推送：

```powershell
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: sync plugin sources"
git push origin main
```

---

# 防乱码规范（重要）

## 1. `README.md` 和 `UPDATE.md` 保持 UTF-8 with BOM

这两个说明文档里有大量中文，保持 UTF-8 with BOM 更适合在 Windows PowerShell 环境里直接查看。

## 2. 不要用 PowerShell 控制台肉眼判断 `TargetSelector.json` 中文是否正常

这个仓库的：

- `TargetSelector.json`
- `scripts/sync_sources.json`

当前是按 UTF-8 处理的，PowerShell 控制台里看到乱码，不一定代表文件真的坏了。

更稳的方式是用 Python 按 UTF-8 读取：

```powershell
python --% -c "import json; from pathlib import Path; obj=json.loads(Path(r'E:\git\TargetSelector\TargetSelector.json').read_text(encoding='utf-8-sig')); print('\\n'.join('{}\\n{}'.format(i.get('Name'), i.get('Description')) for i in obj if i.get('InternalName') in ('DalamudACT','StarlightBreaker','WondrousTailsSolver','日随伴侣卫月版')))"
```

## 3. 优先让脚本改版本号和下载链接，不手工到处替换

只要是已经接入 `scripts/sync_sources.json` 的插件，优先跑：

```powershell
python scripts/sync_plugin_sources.py
```

而不是手动在 `TargetSelector.json` 里到处找版本号和下载链接替换。

## 4. 新插件先补条目，再加同步名单

这一条非常重要：

- **先补 `TargetSelector.json` 条目**
- **再加 `scripts/sync_sources.json` 里的插件名**

反过来做，脚本只会跳过，不会自动新增条目。

---

# 当前建议

以后日常更新，最省事的顺序就是：

```powershell
cd E:\git\TargetSelector
git fetch origin main
git rebase origin/main
python scripts/sync_plugin_sources.py
git status --short --untracked-files=all
git diff --stat
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: sync plugin sources"
git push origin main
```

如果只记 3 条，就记这三条：

1. **先 rebase，再同步**
2. **新插件先补 `TargetSelector.json`，再加 `sync_sources.json`**
3. **中文和版本用 Python 验证，不靠控制台肉眼猜**
