# 全局 UI 主题预览（待用户评审）

2026-09-27。使用内置 image_gen 工具，每张独立生成；不是 CLI/API 模式。
仅制作示意图，没有修改 Unity 代码、场景、预制体或现有美术引用，也没有清理旧图片。物流管理不在此次主题预览范围内。

## 统一方向

B 米白工坊的配色 + A 浅灰工业的简洁边框；暖米白底板、铜橙主按钮、深色文字、绿色生命/进度条、蓝色护甲条。减少装饰、提高对比度。面板目标是不透明，不透出旧底图。

## 图稿

- [01 单座仓库](01-warehouse.png)
- [02 主界面与三类信息框](02-hud-selection.png)
- [03 居民名册与派遣](03-resident-roster.png)
- [04 工厂生产](04-factory-production.png)
- [05 机甲装配与组件选择](05-mech-assembly.png)
- [06 建造菜单](06-construction.png)
- [07 主菜单、暂停与通用控件](07-menus-and-controls.png)

## 评审关注点与示意边界

- 01：单座仓库、四列三行可见格子、滚动代替分页、名称查询、单件改名与独立详情区。
- 02：三个信息框是不同选择状态的对照，不同时出现在游戏里；核对统一的头像、文字、操作按钮位置。图中自动补出的日期/天气不是新增功能要求。
- 03：名册、派遣模式、岗位列表是不同状态的展示。人物肖像为示意，不表示本轮已制作人物生成系统。样例人物在不同示例中的状态不要求一致。
- 04：工厂货架、队列和产能；生成图误把底盘标签高亮而展示组件，落地应高亮组件。背景传送带/小地图只是生成背景，不纳入本轮开发。面板高度偏大，可按用户反馈收窄。
- 05：保留左侧属性、中央底盘/插槽预览、右侧组件选择结构。真实插槽数量、位置和约束依实际底盘数据，不采用图中固定四槽替代系统。
- 06：建造卡片与成本提示；样例将不同类别建筑放一起展示，仅用于比较卡片风格，实际按已有分类过滤。面板高度待用户评审。
- 07：菜单和通用控件风格。新增副标题/宣传文案是图像示意，不代表游戏正式命名。主菜单实际背景保留与否待审美反馈决定。
- 所有数值、品质用词、部分组件与建筑图标、地图美术均为视觉样例，正式 UI 使用真实项目资源与数据。
- 静态图不能证明旧图片清理已完成，也无法演示滚动条淡出、悬停、切换动效。旧图清理需要后续实现和验证。
- 这些是风格与布局方向图，不把整张图片作为界面背景，也不把生成文字作为实际 UI 字体。

## 参考图

- F:/UnityGame/Chimera/Docs/UI/warehouse-theme-concepts-20260927/A-light-industrial.png
- F:/UnityGame/Chimera/Docs/UI/warehouse-theme-concepts-20260927/B-ivory-workshop.png

## 最终提示词

调用参数：referenced_image_paths 为上面两张参考图；transparent_background=false。每张实际 prompt 为公共部分拼接对应图稿部分。

### 公共部分

```text
Use case: ui-mockup. Create one simple polished UI design approval sheet for Chimera, a Chinese top-down pixel-art colony management and mech defense game. Reference image 1 supplies the SIMPLE thin rectilinear frame treatment; reference image 2 supplies the WARM IVORY and COPPER palette. Combine B color with A simple borders. Do not copy warehouse layout unless this sheet explicitly requests warehouse.
Visual rules for ALL screens in this family: flat fully OPAQUE pale ivory #F6EBD6 content; slightly darker warm cream #E8D6B5 headers; dark charcoal brown #3E382F legible Chinese sans serif text; copper #AD663B primary buttons/selection accents; muted green health/progress, muted blue armor. Minimal 1-2 pixel gray-brown outlines with lightly pixel-stepped corners, tiny corner screws at most. MUCH simpler than reference B: no ornamental wiring, no weathering or scratches, no parchment, no heavy bevels, no brass filigree, no yellow glowing edges, no gradients. Artwork only INSIDE icon slots, clean uninterrupted backgrounds behind text. Preserve existing game's distinctive blue/red/orange top-down symmetrical mech chassis shapes. Small pixel-art icons, clean modern readable typography, no enormous decorated headings. This is a UI STYLE mockup; sample data only. Landscape 1536x1024, accurate alignment and generous usable spacing. No browser chrome. No logistics management interface. No new hunger, sleep, relationships, trading or skill systems. No translucent panels and no ghost old UI textures underneath. All Chinese text crisp, high contrast, not white on ivory. Render requested sheet, not a contact sheet of other screens.

```

### 01 单座仓库

```text
SHEET 01 / 单座仓库. One large centered warehouse window on plain neutral warm gray exterior margin. Title "北区仓库", subtitle "已用容量 24 / 100", close X top right. Match reference warehouse structure exactly: left 65% toolbar with three dropdowns "全部类别", "组件类型", "全部标签" plus search "输入名称查找". Below exactly FOUR columns THREE rows of equal item cards, twelve visible individual chassis/component items, no stack quantity on components, no page controls. Thin overlay vertical scrollbar. First blue chassis card "守望者" selected by thin copper outline; other pixel chassis and components, small quality word tags. Right fixed pane icon + "守望者" + pencil rename button, "原型：工业余晖", "类型：装甲底盘", "品质：精良". Separate lower scroll detail with "基础属性", "耐久 180", "承载 150", "能耗 5", "特殊词条", "结构加固 +12%耐久", short description. Simplify all frames and backgrounds per common rules. No extra windows.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-9196fede-327b-4042-ba6b-332f2469ceed.png

### 02 主界面与三类信息框

```text
SHEET 02 / 主界面与统一信息框. A clearly labelled UI APPROVAL BOARD showing a slim global top HUD strip followed by THREE separate wide bottom-selection-panel examples stacked vertically, NOT three panels simultaneously in-game. Plain neutral warm gray sheet background. Small captions outside each example "居民", "建筑", "机甲". At top global strip: small pixel icons, "废料 500", "生物质 200", "魔石 50", "人口 12/20", buttons "仓库", "名册", "暂停". All three example panels EXACT SAME width 1360 and height 210 and same column anchors: left portrait/icon square ~130, name above it; central status and stats; right two vertical action buttons same x positions; top right small close. Resident: name "维嘉", simple placeholder resident portrait (not elaborate future portrait art); status "工作中 · 组件工厂" as a separate line, health thin green bar and separate label "生命 100 / 100" never overlapped by status, "生产力 1.20 · 纪律 85%", "特性：勤勉", right "下岗", "放逐"; do not add hunger/mood. Building: "组件工厂", factory pixel icon same size/location; "生产中", "员工 2/4 · 产能 2.40", compact production strip "工业引擎 45%", right "工作人员", "派遣居民". Mech: "守望者", top-down blue chassis in SAME icon slot, "待命", separate green health "生命 180 / 180" and blue armor "护甲 120 / 150", right "查看详情", "改装" and small restrained "回收" text below. Equal panel dimensions, identical text baselines and consistent icon positions visibly demonstrate stable switching.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-e1653f73-9f70-4fd6-b1d7-f1cef8ec4caf.png

### 03 居民名册与派遣

```text
SHEET 03 / 居民名册与派遣. Primary large centered opaque window "殖民地居民名册", close top right. Below header readable summary "总人口 12 · 赋闲 4 · 前往工作 2 · 在职 6" and filter button "全部居民". Large scroll list with five uniform clean rows. Each row has NAME left ("维嘉","林恩","阿洛","米娅","诺亚"), below level/status, next line "科技 1.20  血肉 1.00  魔力 0.85" and trait "勤勉" or "沉稳", right copper button "查看". Distinguish statuses with small muted colored dots AND readable text "赋闲","前往工作","在职". No large invented portrait art, no extra search/sort/relationship features. Beneath main window on neutral board two small clearly separated STATE EXAMPLES labelled "派遣模式" and "建筑岗位". Assignment example is a short strip title "派遣居民 · 组件工厂", "岗位 2/4", resident row with "派遣" button. Staff example short strip shows two occupied simple avatar slots with names, one empty "+ 派遣居民", and subdued destructive text "全部遣散". State examples are NOT extra simultaneous overlay windows; clean whitespace separates. Use same simple frame and warm ivory theme everywhere.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-c740d86d-2130-484d-bc49-a47cd06f1243.png

### 04 工厂生产

```text
SHEET 04 / 工厂生产. Show one game screen composition with quiet muted desaturated TOP-DOWN orthographic pixel terrain in upper 55% (not isometric). At bottom a broad clean opaque factory panel occupying lower 40%, consistent icon-left / functional-center / actions-right structure, header "组件工厂", compact factory pixel icon at left and summary "员工 2/4", "产能 2.40", "并行 2/3", "速度 1.24×", "原料 120 · 出货 2/8". Central production functional area: top tabs "底盘" and "组件"; top horizontal shelf of 5 equal item icon cards labelled with short names such as "工业引擎","履带","护甲","机炮","能源核心". Under shelf TWO compact task rows with item icon, name, thin green progress line and readable remaining time, e.g. "工业引擎 45% · 00:32" and "履带组件 18% · 01:10", each small cancel X. A third waiting row "复合装甲 · 等待原料". No new start production modal; cards imply add task. Right fixed buttons "工作人员", "派遣居民", disabled muted "升级" and unobtrusive "拆除"; clear top right panel close. A small separate tooltip shown just ABOVE shelf near selected icon, opaque cream, title "工业引擎", "生产时间 60秒", sample costs "废料 30 · 魔石 5", clean frame. These sample numbers are illustrative. Do not show logistics dashboard. Don't make a full screen factory management redesign.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-0e2e0b96-9fd1-4f63-bb9f-e450f16a78e6.png

### 05 机甲装配与组件选择

```text
SHEET 05 / 机甲装配. One large centered workshop window title "机甲装配", close X top right. Preserve existing THREE COLUMN logic: LEFT 23% named editable text field "守望者" with pencil, stat rows "血量 180 / 180", "护甲 150 / 150", "格挡 12", "质量 5.2t", "移速 3.5 m/s", "能量 8 / 12". CENTER 42% large clear flat ivory canvas showing BLUE SYMMETRICAL TOP-DOWN chassis derived from references, NOT humanoid robot, NOT 3D mech. Around chassis readable small square attachment sockets and thin neat attachment connections, captions "核心","武器","移动","辅助"; small button "更换底盘" below preview. RIGHT 30% selector title "可用组件", tabs "核心","武器","移动","辅助", six distinct component cards in 2 columns ×3 rows, each icon, name, small quality badge; one selected "能源核心". Under cards compact detail summary "品质：精良", "供能 +12", "稳定结构", independent selection state. Bottom footer cancel "取消" left and copper primary "确认装配" right, central small validation line "装配条件满足". No pilot crew management or skill loadout features. Frame/background completely clean opaque, minimally decorated, strong readable dark text.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-81926351-ed2d-44b6-af06-a1e3567917b2.png

### 06 建造菜单

```text
SHEET 06 / 建造面板. One game screen composition upper 58% quiet muted desaturated TOP-DOWN orthographic pixel terrain, small selected base structure, no perspective/isometric buildings. Lower ~37% a broad opaque warm ivory construction panel anchored same bottom region as factory/HUD. Left square icon and name "殖民地基地". Center header "建造", horizontal 3 category tabs "单位建筑", "生产建筑", "防御建筑"; selection underline copper. Under tabs one horizontal row of FIVE generously spaced rectangular icon cards: "组件工厂", "装配工厂", "仓库", "居住舱", "防御塔". Simple coherent pixel building icons, names DARK. Selected "仓库" thin copper border. Right compact detail "仓库", description "存放资源与组件", sample costs "废料 120 · 魔石 10", muted placement hint "选择建筑后，在地图中放置", no new tech tree mechanics or extra resource systems. Top right close X. Add a small standalone hover cost tooltip above a card, same cream clean style. User wants a theme preview, preserve compact in-world building selection feel; do not make this into a full screen dashboard.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-3526246d-6aac-4b9e-9295-5a84de7b5e62.png

### 07 主菜单、暂停与通用控件

```text
SHEET 07 / 主菜单与通用控件. One coherent UI approval board on plain neutral warm gray background, clean grid of separated examples with ample whitespace, all same ivory/copper/simplified frame theme. LEFT HALF tall main menu mock: modest pixel-art title "CHIMERA" and small "殖民地计划", opaque ivory menu card containing two large buttons "开始新游戏" and "退出游戏". No invented continue/save/settings functionality. RIGHT TOP smaller pause dialog labelled "暂停", close X, three equal stacked buttons "继续游戏", "返回主菜单", "退出游戏". RIGHT BOTTOM a small sheet section clearly labelled "通用控件样式" with normal copper primary button "确认", neutral secondary "取消", muted disabled button "暂不可用", destructive brick-red text button "拆除"; one name input "守望者", one dropdown "全部类别", tiny quality tags "普通","优秀","精良", an opaque tooltip with readable dark text "需要在装配厂附近操作". Bottom small notification strip "组件已入库". These are separate component examples, not a new gameplay menu. No logs, no logistics, no fake settings sliders. Show visually clear disabled and hover/selected contrast without low contrast text. Borders particularly simple and light, minimal decoration.
```

内置生成原始文件：C:\Users\20723\.codex\generated_images\01a0dc61-94f3-7512-997d-7adc233ae3b2\exec-7fd9ee4a-3eef-4e28-be59-b69793dec7a8.png

