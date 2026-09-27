"""Package the six Unity renders as an offline, self-contained art review page."""
from pathlib import Path
import base64
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / '.utmp/AssemblyRegression/ScaleStudyResults'
DEST = ROOT / 'ArtReview/ScaleStudy'
DEST.mkdir(parents=True, exist_ok=True)
images = {}
for letter in 'ABC':
    for mode in ('source', '32px'):
        name = f'{letter}-{mode}'
        data = (SOURCE / f'{name}.png').read_bytes()
        images[name] = 'data:image/png;base64,' + base64.b64encode(data).decode()
        (DEST / f'{name}.png').write_bytes(data)
shutil.copy2(SOURCE / 'measurements.txt', DEST / 'measurements.txt')
page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Chimera · 居民 / 机甲 / 工厂比例小样</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#eee7d7;color:#302f29;font:16px/1.6 "Microsoft YaHei",sans-serif}
main{max-width:1000px;margin:24px auto;padding:0 24px}h1{font-size:26px;margin:0 0 6px}p{margin:5px 0 12px}
.intro{color:#625d50}.controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:16px 0}
button{font:inherit;padding:8px 16px;border:1px solid #9c917d;background:#fff9ec;border-radius:5px;cursor:pointer;color:#302f29}
button[aria-pressed=true]{background:#98613b;color:white;border-color:#98613b}button:focus-visible{outline:3px solid #378676}
article{background:#fff9ec;border:1px solid #b6a98e;margin:18px 0;border-radius:6px;overflow:hidden}
.head{padding:12px 18px;display:flex;justify-content:space-between;align-items:center;gap:12px}.title{font-size:19px;font-weight:bold}
.spec{font-size:14px;color:#645f53}.picture{background:#333d40;overflow:auto;text-align:center;line-height:0}
img{display:block;width:896px;max-width:100%;height:auto;image-rendering:pixelated;margin:auto}
body.zoom img{width:1792px;max-width:none}.caption{padding:9px 18px;font-size:14px}
.hint{border-left:4px solid #a77745;background:#e4dbc8;padding:10px 14px;margin-bottom:18px}
footer{font-size:14px;color:#615e53;padding:0 0 24px}strong{color:#34392d}.mode-note{min-height:26px}
@media(max-width:640px){main{padding:0 10px}.head{display:block}.spec{margin-top:3px}}
</style><main>
<h1>居民 × 完整机甲 × 工厂：比例对照</h1>
<p class="intro">同一居民、同一套装配、同一视角。每格在原始截图中均为 64 屏幕像素，居民尺寸保持不变。</p>
<div class="controls"><span>建筑细节：</span><button id="source" aria-pressed="true" onclick="setMode('source')">现有素材</button>
<button id="32px" aria-pressed="false" onclick="setMode('32px')">32 像素 / 格试样</button>
<button id="zoom" aria-pressed="false" onclick="setZoom()">放大检查 ×2</button></div>
<p class="mode-note" id="modeNote">当前：只改变尺寸，建筑沿用原图，便于先判断物体大小。</p>
<article><div class="head"><span class="title">A · 当前比例</span><span class="spec">机甲 0.7 倍 ｜ 工厂 2×2 格 ｜ 机甲约 71 像素 / 格</span></div>
<div class="picture"><img id="A" alt="A：当前居民、完整机甲、二乘二工厂"></div><div class="caption">基准组：居民像素较粗，机甲主体偏紧凑，工厂像一台大型设备。</div></article>
<article><div class="head"><span class="title">B · 适度放大</span><span class="spec">机甲 1.0 倍 ｜ 工厂 3×3 格 ｜ 机甲 50 像素 / 格</span></div>
<div class="picture"><img id="B" alt="B：居民不变、适度放大的机甲、三乘三工厂"></div><div class="caption">机甲比当前大约 43%；空间压力较小，但机甲与居民仍保留像素颗粒差异。</div></article>
<article><div class="head"><span class="title">C · 32 像素密度参考</span><span class="spec">机甲 1.5625 倍 ｜ 工厂 4×4 格 ｜ 机甲 32 像素 / 格</span></div>
<div class="picture"><img id="C" alt="C：居民不变、与居民同像素密度的机甲、四乘四工厂"></div><div class="caption">完整机甲比当前大约 2.23 倍。保留手绘细节；需要更宽的道路、更大的装配场地。</div></article>
<div class="hint"><strong>如何挑选：</strong>先看大小关系，再切换建筑试样看像素颗粒。C 是统一密度的参照，不代表所有机甲都必须这么大。</div>
<footer>装配：禁军底盘＋陷阵核心＋厚实护甲＋狙击枪＋链锯＋刀锋。沿用项目插槽位置、安装角度与连接偏移。<br>
这是隔离工程中的静态渲染，物体按画布底边对齐，便于比较尺寸；不模拟战斗瞄准或动画。建筑试样仅作最近邻降采样，尚未人工修整。<br>
工厂格数是本小样的占地提案。正式地图、预制体、PPU、碰撞、寻路和存档均未修改。</footer>
</main><script>
const images=__IMAGES__;
function setMode(mode){for(const id of ['A','B','C'])document.getElementById(id).src=images[id+'-'+mode];
for(const id of ['source','32px'])document.getElementById(id).setAttribute('aria-pressed',String(id===mode));
document.getElementById('modeNote').textContent=mode==='source'?'当前：只改变尺寸，建筑沿用原图，便于先判断物体大小。':'当前：建筑降至约 32 像素 / 格。仅作密度试样；三组人物与机甲保持不变。';}
function setZoom(){const on=document.body.classList.toggle('zoom');document.getElementById('zoom').setAttribute('aria-pressed',String(on));document.getElementById('zoom').textContent=on?'恢复对照视图':'放大检查 ×2';}
setMode(new URLSearchParams(location.search).get('density')==='32'?'32px':'source');
</script></html>'''
(DEST / 'index.html').write_text(page.replace('__IMAGES__', json.dumps(images)), encoding='utf-8')
print('Saved offline scale review:', DEST / 'index.html')
