# 单座仓库 UI 主题概念

日期：2026-09-27。生成方式：内置 image_gen 工具，三次独立生成；不是 CLI/API 模式。

这三张图片仅用于选择视觉主题，尚未修改 Unity 代码、场景或预制体。

## 已对齐需求

- 展示某一座仓库的库存，标题显示当前占用与最大容量（具体容量单位沿用后续核实的数据定义）。
- 左侧类别、组件类型、标签筛选与名称搜索，搜索支持自定义名和原型名。
- 左侧可见四列三行，垂直滚动，无分页。
- 右侧固定物品详情；名称旁铅笔用于给单个底盘/组件实例改名，保留原型名。
- 详情内容独立滚动；滚动条仅在滚动时出现，淡出时不改变布局。效果图展示滚动条可见状态，无法演示动效。
- 仓库与物流/未来工作管理保持分开。
- 窗口底板不透明，避免下层旧界面透出；正式实现需要处理旧节点与窗口生命周期。

## 方案

- A-light-industrial.png：浅灰工业，石墨色文字、青绿强调、少量黄铜角件。
- B-ivory-workshop.png：米白工坊，暖色底板、铜橙强调，较接近现有美术。
- C-blue-gray-console.png：灰蓝控制台，浅蓝灰框、米白内容、琥珀强调。

三个方案遵循相同布局结构，独立生成导致部分图标、文字与装饰有差异。图中的品质称谓、名称、属性和容量仅用于展示，不代表已实现的数据或数值设计。正式制作使用项目原始图标和真实文本，不将整张概念图直接作为交互界面。

## 参考素材

1. E:/xwechat_files/wxid_0x5dlnepnokj22_f06c/temp/RWTemp/2026-09/9d6a6d20fc90c0bac8bd092eb15dd718/630e2b25e8dec9fa41b204d331c887bb.jpg
2. F:/UnityGame/Chimera/Assets/Art/Backgrounds/底盘详情页.png
3. F:/UnityGame/Chimera/Assets/Art/Mechs/HandDrawn/底盘1.png
4. F:/UnityGame/Chimera/Assets/Art/Mechs/HandDrawn/底盘2.png

## 最终提示词

每次实际调用的 prompt 为下方公共部分加对应主题段，referenced_image_paths 为上述四张图，transparent_background=false。

### 公共部分

```text
Use case: ui-mockup. Transform the hand drawn warehouse UI wireframe in reference image 1 into ONE polished, readable game interface concept, landscape 1536x1024. Image 1 dictates the layout. Image 2 is the game's existing hand-drawn pixel UI style reference: cream panels with small mechanical irregularities and colored edge ornament. Images 3 and 4 are the game's real pixel-art chassis icons: reuse their silhouettes and pixel-art appearance for the blue and red chassis cards. This is a Chinese top-down colony management/mech defense game, not a web dashboard.
A single large opaque warehouse window fills almost the entire image, viewed straight on. Outer margin is a quiet neutral solid field, no game world or other windows underneath. Maintain high contrast DARK readable Chinese text on light content surfaces. Crisp restrained pixel-stepped frames, modest corner rivets, no exaggerated machinery, no fantasy parchment scrolls, no neon, no dark mode.
EXACT LAYOUT: top bar title at left "北区仓库" and "已用容量 24 / 100"; close X at far top right. Under title, LEFT TWO THIRDS has one horizontal toolbar of 3 dropdowns "全部类别", "组件类型", "全部标签" followed by a magnifier search field "输入名称查找". Below that toolbar is a scrollable grid showing EXACTLY FOUR COLUMNS and THREE ROWS, total TWELVE equal rectangular cards; no fourth row, no pagination arrows or page numbers. Grid occupies left 67%; right 30% is a fixed tall detail pane, gap 3%. Thin overlay scrollbar inside right edge of grid shows its scrolling state and takes no layout space. No footer toolbar.
Each card: ample space, crisp centered small pixel-art item, readable name at bottom, small quality word/badge, restrained quality-colored edge accent. Each chassis/component card is ONE individual item; do not show stacks/count badges for components. First card selected with clear teal/copper/amber outline suited to theme; name "守望者", blue chassis icon, quality "精良". Remaining eleven cards are distinct pixel-art chassis/industrial component icons: red chassis, orange chassis, engine, weapon, tracks, armor etc, names can be short readable Chinese. Only tiny quality accents use green blue purple, never color whole cards with rarity.
RIGHT detail pane: header has selected blue chassis icon, title "守望者", clearly visible pencil button immediately beside title for renaming THIS ITEM INSTANCE. Under it smaller original model "原型：工业余晖", then "类型：装甲底盘" and "品质：精良". Below header a separate light recessed scroll area (independent thin overlay scrollbar), text hierarchy with "基础属性", stat lines "耐久 180" "承载 150" "能耗 5", then "特殊词条", "结构加固  +12% 耐久", and "物品描述", a short readable Chinese description. This is a style concept; these numbers are illustrative. Comfortable font sizes, generous spacing, no tiny cramped text.
Theme-specific styling follows:

```

### A 浅灰工业

```text
A / 浅灰工业. Warm LIGHT GRAY enamel frame #D9DDD8, off-white content #F2F0E6, graphite text #273432, desaturated TEAL #3B8278 active controls and selections, small aged brass corner fittings. Light utilitarian industrial terminal, calm clear and functional. Existing art's whimsical hand-pixel craft retained very subtly in frame corners. Do not print a theme title or presentation labels outside the UI.
```

### B 米白工坊

```text
B / 米白工坊. Warm IVORY #F1E3C5 and pale oat content #FBF4E4, gray-brown metal frame #8F8271, dark brown-charcoal text #3F392F, COPPER ORANGE #B77043 for selections and controls, small desaturated teal rivets/wires. Inspired most closely by existing cream hand-drawn pixel panels: restrained pixel-cut corners and mechanical detailing, a welcoming lived-in colony workshop, not medieval parchment, not wood panels. Bright airy surfaces, warm but not yellow fog. Do not print a theme title or presentation labels outside the UI.
```

### C 灰蓝控制台

```text
C / 灰蓝控制台. LIGHT/MEDIUM BLUE GRAY #B5C8D2 outer metal frame, pale mist blue #E2EBEB panels and cream #F3F0E1 cards, dark navy #293F4E text, AMBER #CE913C selection and status accents. Brighter than conventional dark sci-fi menus; colony control console with understated pixel-stepped mechanical frame and thin muted red/blue wire cues recalling existing hand-drawn art. No neon, no glass, no transparency, no dark navy backgrounds. Do not print a theme title or presentation labels outside the UI.
```

