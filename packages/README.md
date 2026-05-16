# TargetSelector packages

这个目录只用于临时托管修复包。

正常情况下，`TargetSelector.json` 应直接指向插件作者的官方 release zip。只有上游 release zip 本身损坏，例如 zip 内 manifest 版本与 DLL 版本不一致，才允许在这里放置临时修复包。

使用临时修复包时必须满足：

1. 不修改插件 DLL。
2. 只修复安装所需的 manifest 元数据。
3. `scripts/validate_targetselector.py` 校验通过。
4. 上游 release 修复后，尽快切回官方 release zip，并删除不再使用的临时包。
