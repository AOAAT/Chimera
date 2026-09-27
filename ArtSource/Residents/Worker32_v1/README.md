# 居民小人：保留的初版源稿

当前游戏使用 `Assets/Art/Residents/Worker48.png`：由本目录放大源稿重新采样为八张 48×48 帧（192×96 图集），50 PPU，轮廓不超过 36×44 像素。旧的 Worker32 图集保留用于回溯，下面的 32 像素规格和生成提示词是初版记录。

当前重新整理菜单：`Tools → Chimera → 居民美术 → 从源稿生成48像素精灵（50PPU）`。并未重新手工绘制像素，未来仍可对游戏用纹理做逐像素修整。

使用内置 imagegen 生成，参考此前对比图的 B 大头工装比例。本目录保存美术源稿与提示词，游戏使用下方说明中的原生精灵图集。

`worker-directions-carry-concept.png` 为透明底放大源稿，4 列 × 2 行。上排从左到右为正面、左侧、右侧、背面；下排为相应方向的搬运姿态。不是行走动画序列。

目标规格：每帧 32×32，人物与箱子不超过一格。源稿保留放大尺寸，不能把原图像素尺寸直接当成 32×32 使用。

正式精灵已通过 Unity 导入工具整理至 `Assets/Art/Residents/Worker32.png`：128×64 图集，八张原生 32×32 精灵，按原画比例缩放，轮廓不超过 24×28；统一底部两像素边距和脚底定位点。最近邻采样，透明边缘规范为 0/255，无压缩、无 mipmap，使用 Point 过滤与 32 PPU。

`Assets/Resources/Residents/WorkerSprites.asset` 保存站立和搬运的四方向引用，可在 Inspector 中换成后续素材。`Resident_Base.prefab` 已保存新图和表现组件；角色详情、岗位卡片同步使用正面图。未加入随机外貌和逐帧步行动画。

初版使用了 32 像素整理工具，现已由上方 48 像素菜单替代。菜单从本目录源稿重新整理图集与预制体；不要在手工修改正式图集后无意重跑。导入保持精灵子资源标识，避免反复生成造成引用失效。

## 生成提示词

Use case: stylized-concept.
Asset type: clean transparent-background PIXEL SPRITE SHEET for a top-down colony simulation game.
The supplied image is STYLE REFERENCE ONLY. Use its B big-head worker proportions with restrained industrial work-clothes details. This is a NEW simplified low-resolution sprite asset, NOT another comparison poster.

CRITICAL pixel specification: create a sheet representing exactly 4 columns x 2 rows of LOGICAL 32 x 32 pixel tiles (128 x 64 logical pixels total). Display the sheet enlarged by an exact uniform nearest-neighbor integer scale so every logical pixel is a LARGE, FLAT, UNIFORM SQUARE. Each character must be drawn using ONLY about 20–24 logical pixels in width and 26–28 logical pixels in height, with transparent padding, completely contained in its own tile. Minimum feature = ONE LARGE LOGICAL PIXEL. No smaller pixels within those pixels, no smooth painting, no gradients, no anti-aliasing. This is extremely compact game sprite artwork, much simpler and lower resolution than the reference. Make the underlying coarse 32x32-per-frame pixel grid absolutely obvious. Do not draw high-resolution character illustrations with a pixelated texture.

Character identity CONSISTENT across all 8 cells: adult colony worker, oversized head about 45% of height, short dark chestnut hair with simple side-part silhouette, warm medium skin, dusty slate-blue mechanic coveralls, tiny orange shoulder patch, dark belt, short legs and dark boots, no helmet, no weapons. Facial features only two simple dark eye pixels where visible. 12 to 16 colors maximum for entire sheet, opaque color clusters, dark charcoal one-logical-pixel outline. Compact friendly ADULT proportions. View suitable for an overhead orthographic square-tile colony map: visible hair top, head and shoulders; upright readable pawn sprite, no isometric view. Strong distinct silhouette at tiny scale.

Sheet EXACT content:
ROW 1 four equally sized cells: front-facing idle; left-facing idle; right-facing idle; back-facing idle.
ROW 2 four equally sized cells: front-facing carrying a small brown supply crate against chest; left-facing carrying same crate; right-facing carrying same crate; back-facing carrying same crate, arms suggest holding in front and crate mostly occluded.
Uniform character scale and aligned foot baseline across all eight cells. Each cell has the same fixed bottom-center pivot. Hair, hands, shoes and crate must remain inside the logical 32x32 tile, with 2 or more transparent logical pixels below the feet. Front and back have consistent head size. Side silhouettes should look distinctly sideways. Avoid accessories or hairstyle variations between frames.
Actual alpha-transparent background throughout. No ground shadows, no scenery, no tile grid lines, no labels, no numbers, no headings, no frames, no watermark, no glow. Even gutters supplied by transparent tile padding only. All eight complete characters visible and separated, no cropping, no blending between cells.
Prioritize truly coarse readable small-pixel artwork over decorative detail. This is for replacing a black placeholder person, not a portrait.
