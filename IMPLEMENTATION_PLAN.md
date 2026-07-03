# SimpleSoflanFramework 迁移计划

## Summary
创建 `netstandard2.0` 类库 `SimpleSoflanFramework`，把 `OngekiFumenEditor` 中 `SoflanList` 及 `GetVisibleRanges_PreviewMode()` 相关完整时间轴/变速/BPM 逻辑迁入新库，命名空间改为 `SimpleSoflanFramework.*`，并仅提供一个自包含的 `PropertyChangedBase` 兼容实现，不继续迁移 Caliburn 其余基础设施。

目标状态：
- 新库可独立编译，不依赖 `OngekiFumenEditor`。
- `SoflanList.GetVisibleRanges_PreviewMode()` 行为与上游保持一致。
- API 尽量保留原类型名和成员名，但使用新命名空间。
- `PropertyChangedBase` 仅保留常用属性通知能力，不保留 Caliburn 平台抽象层。

## Key Changes
### 1. 工程结构
- 新建 `SimpleSoflanFramework.csproj`，目标框架 `netstandard2.0`。
- 根命名空间统一为 `SimpleSoflanFramework`。
- 按功能分层组织源码：
 - `SimpleSoflanFramework.Mvm`
 - `SimpleSoflanFramework.Core`
 - `SimpleSoflanFramework.Collections`
 - `SimpleSoflanFramework.Collections.Base`
 - `SimpleSoflanFramework.EditorObjects`
 - `SimpleSoflanFramework.OngekiObjects`
 - `SimpleSoflanFramework.Utils`

### 2. PropertyChangedBase 兼容实现
只迁移 `PropertyChangedBase` 本身，并做最小自包含改写：
- 提供 `INotifyPropertyChanged` 基础能力。
- 保留 `IsNotifying`、`Refresh()`、`NotifyOfPropertyChange()`、`Set<T>()`。
- 去掉对 `Caliburn.Micro` 的 `PlatformProvider`、`Execute`、`INotifyPropertyChangedEx` 等依赖。
- `NotifyOfPropertyChange()` 直接同步触发 `PropertyChanged`，不做 UI 线程封送。
- 类名保留为 `PropertyChangedBase`，放到 `SimpleSoflanFramework.Mvm`。

### 3. 时间轴基础模型
迁移 `SoflanList` 所需的完整基础模型：
- `GridBase`
- `GridOffset`
- `TGrid`
- `ITimelineObject`
- `IDisplayableObject`
- `ISoflan`
- `IKeyframeSoflan`
- `IDurationSoflan`

补齐必要基础对象与基类：
- 一个新的时间轴基类，负责 `TGrid` 和属性通知。
- `BPMChange`
- `Soflan`
- `KeyframeSoflan`
- `InterpolatableSoflan`

要求：
- `TGrid`、`GridOffset` 的比较、归一化、加减逻辑与上游一致。
- `ISoflan.SpeedInEditor`、`EndTGrid`、`SoflanGroup` 等成员保留。
- `InterpolatableSoflan` 只迁移算法依赖的数据和关键帧展开能力，不迁移 UI 附属对象。

### 4. 集合与查询基础层
迁移 `SoflanList` 和 `BpmList` 所需集合能力：
- `BpmList`
- `TGridSortList<T>`
- `IBinaryFindRangeEnumable<T, TKey>`
- `IntervalTreeWrapper<TKey, TValue>` 或等价最小实现

策略：
- 保持时间轴对象按 `TGrid` 排序。
- 保留 `SoflanList` 依赖的区间查询能力。
- 尽量自包含，不引入新的外部包；若上游区间树实现耦合过重，则在库内补一个最小可用实现。

### 5. Soflan 算法迁移
完整迁移 `SoflanList` 两个 partial 中与预览计算相关的公开能力：
- `GetVisibleStartObjects`
- `GetCalculatableEvents`
- `GetCachedSoflanPositionList_DesignMode`
- `GetCachedSoflanPositionList_PreviewMode`
- `GetCachedSoflanSegment_PreviewMode`
- `GetVisibleRanges_PreviewMode`
- `CalculateSpeed`
- `GenerateDurationSoflans`
- `GenerateKeyframeSoflans`

一并迁移或重写必要支撑类型：
- `SoflanPoint`
- `SoflanSegment`
- `VisibleTGridRange`
- `MathUtils`
- `CollectionHelper.MergeTwoSortedCollections`
- `RandomHepler`
- 二分查询扩展
- `DistinctContinuousBy`

对象池部分处理：
- 原实现里的对象池调用保留相同行为即可。
- 如果对象池迁移成本高，改为普通 `List<T>` / `HashSet<T>` 实现，优先保证算法正确性与可读性。

### 6. API 兼容边界
对外保留这些关键 API 名称和签名，仅改命名空间：
- `PropertyChangedBase`
- `BpmList`
- `SoflanList`
- `SoflanList.VisibleTGridRange`
- `SoflanList.GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale)`

明确不迁移：
- Caliburn 的 `PlatformProvider`、`Execute`、`INotifyPropertyChangedEx`
- 编辑器 UI、WPF/Gemini 相关代码
- 与 `SoflanList` 算法无关的更大编辑器对象体系

## Test Plan
1. `PropertyChangedBase.Set<T>` 在值变化时触发通知，未变化时不触发。
2. `TGrid` 与 `GridOffset` 的比较、归一化、加减结果正确。
3. `BpmList.GetBpm`、`GetPrevBpm`、`GetNextBpm` 在边界点正确。
4. `SoflanList.GetCalculatableEvents` 在无变速、正向变速、负向变速、duration soflan 展开、同点 BPM/变速变更场景下结果正确。
5. `GetVisibleRanges_PreviewMode()` 在以下场景与上游一致：
 - 无变速
 - 多段正向变速
 - 含负向变速
 - 含 `Speed = 0`
 - 视野跨多个段
 - `currentY` 超过最后一个变速点
 - 不同 `preOffset`
 - 不同 `scale`
6. `GenerateDurationSoflans` 与 `GenerateKeyframeSoflans` 输出切分正确。
7. 用固定样例对拍上游实现的缓存位置表和可视范围结果。

## Assumptions
- `PropertyChangedBase` 只需要“兼容常用调用方式”，不需要 Caliburn 完整语义。
- 新库命名空间统一为 `SimpleSoflanFramework.*`。
- 迁移边界按“完整 `SoflanList` + 所需时间轴基础层”执行，而不是只搬单个方法。
- 若上游存在隐藏小依赖，实施时按“最小自包含闭包”继续补齐。
