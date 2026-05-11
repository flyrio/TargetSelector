# TargetSelector 更新速查

这个仓库提供 Dalamud 插件订阅源：

`https://raw.githubusercontent.com/flyrio/TargetSelector/main/TargetSelector.json`

详细说明看 `README.md`，这里保留一份适合下次快速更新的速查版。

## 当前从 MyDalamudRepo 同步的插件

1. `DalamudACT`
2. `PluginDockStandalone`
3. `PartyIcons`
4. `Saucy`
5. `StarlightBreaker`
6. `WondrousTailsSolver`

---

## 自动同步

这个仓库会通过 GitHub Actions 的 `Sync Plugin Sources` 定时同步：

- 每 6 小时自动检查一次
- 也可以在 `Actions` 页面手动触发
- 工作流文件：`/.github/workflows/sync-plugin-sources.yml`
- 同步脚本：`/scripts/sync_plugin_sources.py`
- 来源配置：`/scripts/sync_sources.json`

普通更新优先让自动同步跑；如果要手动处理，就照下面流程来。

---

## 最快更新流程

### 1. 进入仓库并对齐远端

```powershell
cd E:\git\TargetSelector
git fetch origin main
git rebase origin/main
```

### 2. 跑同步脚本

```powershell
python scripts/sync_plugin_sources.py
```

### 3. 更新后检查

```powershell
git status --short --untracked-files=all
git diff --stat
```

建议用 Python 验证当前同步的 6 个插件版本：

```powershell
python --% -c "import json; from pathlib import Path; obj=json.loads(Path(r'E:\git\TargetSelector\TargetSelector.json').read_text(encoding='utf-8-sig')); names=('DalamudACT','PluginDockStandalone','PartyIcons','Saucy','StarlightBreaker','WondrousTailsSolver'); print('\\n'.join('{} {}'.format(i['InternalName'], i['AssemblyVersion']) for i in obj if i.get('InternalName') in names))"
```

### 4. 提交并推送

```powershell
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: sync plugin sources"
git push origin main
```

---

## 新增一个来自 MyDalamudRepo 的插件

这个仓库新增同步插件时，要记住这两步必须都做：

1. **先把完整插件条目补进 `TargetSelector.json`**
2. **再把 `InternalName` 加进 `scripts/sync_sources.json`**

然后再跑：

```powershell
python scripts/sync_plugin_sources.py
```

如果只加同步名单、不补条目，脚本会跳过这个插件。

---

## 如果 push 被拒绝

先重新同步，再跑一次脚本：

```powershell
git fetch origin main
git rebase origin/main
python scripts/sync_plugin_sources.py
```

然后重新提交：

```powershell
git add -- TargetSelector.json scripts/sync_sources.json README.md UPDATE.md
git commit -m "chore: sync plugin sources"
git push origin main
```

---

## 防乱码规则

### 1. 不要靠 PowerShell 控制台肉眼判断 `TargetSelector.json` 中文是否正常

更稳的方式是用 Python 按 UTF-8 读取：

```powershell
python --% -c "import json; from pathlib import Path; obj=json.loads(Path(r'E:\git\TargetSelector\TargetSelector.json').read_text(encoding='utf-8-sig')); print('\\n'.join('{}\\n{}'.format(i.get('Name'), i.get('Description')) for i in obj if i.get('InternalName') in ('DalamudACT','StarlightBreaker','WondrousTailsSolver')))"
```

### 2. 优先跑脚本，不手动到处改版本号和下载链接

只要插件已经接进 `scripts/sync_sources.json`，就优先跑：

```powershell
python scripts/sync_plugin_sources.py
```

### 3. 新插件先补条目，再加同步名单

这一条最容易漏：

- 先补 `TargetSelector.json`
- 再改 `scripts/sync_sources.json`

---

## 记住这 3 条就够了

1. **先 rebase，再同步**
2. **新插件先补 `TargetSelector.json`，再加 `sync_sources.json`**
3. **中文和版本用 Python 验证，不靠控制台猜**
