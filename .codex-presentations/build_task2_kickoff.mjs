import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const workspaceDir = "F:\\UnityGame\\Chimera";
const buildDir = path.join(workspaceDir, ".codex-presentations", "build");
const outputDir = path.join(workspaceDir, "output");
const assetsDir = path.join(workspaceDir, ".codex-presentations", "assets");
const SKILL_DIR = "C:\\Users\\20723\\.codex\\plugins\\cache\\openai-primary-runtime\\presentations\\26.905.11957\\skills\\presentations";
const RUNTIME_PYTHON = "C:\\Users\\20723\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe";
const RUNTIME_NODE = "C:\\Users\\20723\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\bin\\node.exe";
const RUNTIME_NODE_MODULES = "C:\\Users\\20723\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\node_modules";
const RUNTIME_BIN_DIR = "C:\\Users\\20723\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\bin";
const FINAL_PPTX = path.join(outputDir, "任务二_科学投喂与鱼群摄食监测_立项汇报_四人版.pptx");
const FONT = "HarmonyOS Sans SC";

process.env.RUNTIME_NODE = RUNTIME_NODE;
process.env.RUNTIME_NODE_MODULES = RUNTIME_NODE_MODULES;
process.env.RUNTIME_BIN_DIR = RUNTIME_BIN_DIR;
process.env.RUNTIME_PYTHON = RUNTIME_PYTHON;

await fs.mkdir(buildDir, { recursive: true });
await fs.mkdir(outputDir, { recursive: true });

const presentation = Presentation.create({ slideSize: { width: 1280, height: 720 } });

const C = {
  navy: "#061B2B",
  navy2: "#0A2A3F",
  ink: "#102A3B",
  muted: "#567080",
  paper: "#F3F8F9",
  white: "#FFFFFF",
  cyan: "#38C7D9",
  teal: "#29A995",
  amber: "#F2B84B",
  coral: "#F06E5B",
  paleCyan: "#DFF6F8",
  paleTeal: "#DDF4EF",
  paleAmber: "#FFF2D6",
  paleCoral: "#FDE7E3",
  line: "#C9DADF",
};

const partColors = [C.cyan, C.teal, C.amber, C.coral];
const partPales = [C.paleCyan, C.paleTeal, C.paleAmber, C.paleCoral];

function addShape(slide, geometry, x, y, w, h, fill = "none", line = { fill: "none", width: 0 }, radius = undefined) {
  const s = slide.shapes.add({
    geometry,
    position: { left: x, top: y, width: w, height: h },
    fill,
    line,
    ...(radius ? { borderRadius: radius } : {}),
  });
  return s;
}

function addText(slide, text, x, y, w, h, opts = {}) {
  const s = addShape(slide, "textbox", x, y, w, h, opts.fill ?? "none", opts.line ?? { fill: "none", width: 0 }, opts.radius);
  s.text = text;
  s.text.style = {
    typeface: opts.font ?? FONT,
    fontSize: opts.size ?? 24,
    bold: opts.bold ?? false,
    color: opts.color ?? C.ink,
    alignment: opts.align ?? "left",
    verticalAlignment: opts.valign ?? "top",
    autoFit: opts.autoFit ?? "shrinkText",
    wrap: "square",
    insets: opts.insets ?? { top: 4, right: 4, bottom: 4, left: 4 },
    ...(opts.lineSpacing ? { lineSpacing: opts.lineSpacing } : {}),
  };
  return s;
}

function addImage(slide, fileName, x, y, w, h, opts = {}) {
  const ext = path.extname(fileName).toLowerCase();
  const contentType = ext === ".png" ? "image/png" : "image/jpeg";
  return fs.readFile(path.join(assetsDir, fileName)).then((blob) => slide.images.add({
    blob,
    contentType,
    alt: opts.alt ?? fileName,
    fit: opts.fit ?? "cover",
    position: { left: x, top: y, width: w, height: h },
    ...(opts.crop ? { crop: opts.crop } : {}),
    ...(opts.geometry ? { geometry: opts.geometry } : {}),
    ...(opts.borderRadius ? { borderRadius: opts.borderRadius } : {}),
  }));
}

function addFooter(slide, n, part = undefined) {
  addShape(slide, "line", 68, 678, 1144, 1, "none", { style: "solid", fill: C.line, width: 1 });
  addText(slide, "任务二 · 科学投喂与鱼群摄食监测 · 立项汇报", 68, 686, 760, 22, { size: 14, color: C.muted, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, String(n).padStart(2, "0"), 1140, 686, 72, 22, { size: 14, bold: true, color: part ? partColors[part - 1] : C.muted, align: "right", valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
}

function addHeader(slide, title, part, speaker) {
  slide.background.fill = C.paper;
  addShape(slide, "rect", 0, 0, 14, 720, partColors[part - 1], { fill: "none", width: 0 });
  addText(slide, `0${part}  ${speaker}`, 68, 34, 420, 28, { size: 16, bold: true, color: partColors[part - 1], valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, title, 68, 74, 1120, 64, { size: 42, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
}

function addLabel(slide, text, x, y, w, color, pale) {
  const s = addText(slide, text, x, y, w, 30, { size: 15, bold: true, color, align: "center", valign: "middle", fill: pale, radius: "rounded-full", insets: { top: 0, right: 6, bottom: 0, left: 6 } });
  return s;
}

function setNotes(slide, time, speaker, script, sources = "") {
  const note = [`建议时长：${time}`, `主讲：${speaker}`, "", script];
  if (sources) note.push("", `内容依据：${sources}`);
  slide.speakerNotes.textFrame.setText(note.join("\n"));
}

// 1. Cover
{
  const slide = presentation.slides.add();
  slide.background.fill = C.navy;
  await addImage(slide, "image1.png", 0, 0, 1280, 720, { alt: "水下机器人与鱼群的概念图", fit: "cover" });
  addShape(slide, "rect", 0, 0, 1280, 720, "linear(90deg, #061B2B/96 0%, #061B2B/78 52%, #061B2B/20 100%)", { fill: "none", width: 0 });
  addText(slide, "AI 赋能智慧海洋牧场 · 课程任务二", 76, 76, 720, 34, { size: 18, bold: true, color: C.cyan, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "科学投喂与\n鱼群摄食监测", 76, 142, 720, 152, { size: 58, bold: true, color: C.white, valign: "middle", lineSpacing: 0.94, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "立项汇报", 80, 326, 240, 48, { size: 30, bold: true, color: C.navy, align: "center", valign: "middle", fill: C.cyan, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "四人协作 · 总时长控制在 10 分钟内", 80, 400, 640, 34, { size: 20, color: C.white, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "计划周期：2026 年 10 月", 80, 612, 360, 28, { size: 17, color: "#C9E8ED", valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "汇报人：________  ________  ________  ________", 80, 648, 690, 28, { size: 17, color: "#C9E8ED", valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  setNotes(slide, "10 秒", "主讲人 1", "开场说明：本次汇报聚焦任务二的立项依据、技术方案、执行计划和验收安排。四位成员依次完成四个部分。", "《海洋牧场任务对应PPT》任务二说明；《任务二_科学投喂与鱼群摄食监测_项目规划_v3》");
}

// 2. Agenda
{
  const slide = presentation.slides.add();
  slide.background.fill = C.paper;
  addText(slide, "四段式汇报安排", 68, 54, 760, 58, { size: 42, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "每人负责一个完整问题，交接点与项目分工一致", 68, 118, 830, 34, { size: 20, color: C.muted, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const items = [
    ["01", "立项依据", "为什么做\n做到什么程度", "主讲人 1", "约 2 分 20 秒"],
    ["02", "技术方案", "数据如何采\n模型如何判", "主讲人 2", "约 2 分 15 秒"],
    ["03", "执行计划", "10 月怎么推进\n四人如何协作", "主讲人 3", "约 2 分 15 秒"],
    ["04", "验收与保障", "如何证明有效\n需要哪些支持", "主讲人 4", "约 3 分钟"],
  ];
  for (let i = 0; i < 4; i++) {
    const x = 68 + i * 288;
    addShape(slide, "rect", x, 190, 8, 370, partColors[i], { fill: "none", width: 0 });
    addText(slide, items[i][0], x + 24, 188, 90, 52, { size: 38, bold: true, color: partColors[i], valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, items[i][1], x + 24, 252, 220, 46, { size: 28, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, items[i][2], x + 24, 318, 220, 92, { size: 22, color: C.ink, lineSpacing: 1.18, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, items[i][3], x + 24, 450, 220, 28, { size: 16, bold: true, color: partColors[i], valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, items[i][4], x + 24, 488, 220, 28, { size: 16, color: C.muted, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  }
  addFooter(slide, 2);
  setNotes(slide, "20 秒", "主讲人 1", "快速说明四个部分以及交接顺序。强调每人围绕一个完整问题讲解，避免内容重复。", "根据用户要求重构为四个鲜明部分");
}

// 3. Problem and goals
{
  const slide = presentation.slides.add();
  addHeader(slide, "投喂决策缺少可量化反馈", 1, "立项依据 · 主讲人 1");
  await addImage(slide, "image34.jpeg", 720, 164, 492, 418, { alt: "原始水下鱼群影像", fit: "cover", geometry: "roundRect", borderRadius: "rounded-2xl" });
  addShape(slide, "rect", 720, 498, 492, 84, "#061B2B/82", { fill: "none", width: 0 }, "rounded-2xl");
  addText(slide, "水下浑浊、色偏与遮挡，使“是否吃饱”难以直接判断", 744, 516, 444, 48, { size: 18, bold: true, color: C.white, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const issues = [
    ["投多", "饲料浪费，残饵沉降并增加水体负担"],
    ["投少", "生长受限，养殖周期可能被拉长"],
    ["误判", "水花、聚集、气泡和反光都可能形成假信号"],
  ];
  for (let i = 0; i < issues.length; i++) {
    const y = 184 + i * 118;
    addText(slide, issues[i][0], 68, y, 104, 42, { size: 20, bold: true, color: C.navy, align: "center", valign: "middle", fill: C.paleCyan, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, issues[i][1], 194, y - 3, 470, 58, { size: 22, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    if (i < 2) addShape(slide, "line", 194, y + 76, 470, 1, "none", { style: "solid", fill: C.line, width: 1 });
  }
  addText(slide, "立项目标", 68, 548, 120, 30, { size: 16, bold: true, color: C.cyan, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "把“凭经验投料”转为“看鱼的状态投料”，并保留每餐可复盘证据", 68, 584, 620, 58, { size: 25, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 3, 1);
  setNotes(slide, "55 秒", "主讲人 1", "先说明投喂偏多与偏少的双向损失，再指出水面和水下单一现象都不足以支撑判断。因此项目需要建立客观、实时、可追溯的反馈。", "《海洋牧场任务对应PPT》第31页；原始水下画面来自第18页");
}

// 4. Scope and loop
{
  const slide = presentation.slides.add();
  addHeader(slide, "项目边界与最小可行闭环", 1, "立项依据 · 主讲人 1");
  const stages = [
    ["投喂前", "鱼群分布与规格\n水质和海况"],
    ["投喂中", "游动与聚集\n声学与水面印证"],
    ["投喂后", "漂浮饵料\n底部残饵"],
    ["餐次复盘", "建议、执行、结果\n统一关联"],
  ];
  const xs = [68, 358, 648, 938];
  for (let i = 0; i < stages.length; i++) {
    addText(slide, stages[i][0], xs[i], 186, 230, 44, { size: 22, bold: true, color: i === 3 ? C.white : C.navy, align: "center", valign: "middle", fill: i === 3 ? C.cyan : C.paleCyan, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, stages[i][1], xs[i], 250, 230, 94, { size: 20, color: C.ink, align: "center", valign: "middle", lineSpacing: 1.15, insets: { top: 0, right: 4, bottom: 0, left: 4 } });
    if (i < 3) addShape(slide, "rightArrow", xs[i] + 242, 269, 34, 28, partColors[0], { fill: "none", width: 0 });
  }
  addShape(slide, "line", 68, 386, 1100, 1, "none", { style: "solid", fill: C.line, width: 1 });
  addText(slide, "本期 10 月重点", 68, 420, 210, 34, { size: 18, bold: true, color: C.cyan, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "先打通“采集、清晰化、标注、验证”的数据链路", 68, 462, 650, 54, { size: 28, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "范围内", 760, 428, 98, 28, { size: 16, bold: true, color: C.teal, align: "center", valign: "middle", fill: C.paleTeal, radius: "rounded-full", insets: { top: 0, right: 4, bottom: 0, left: 4 } });
  addText(slide, "机器人采集规范、图像增强流水线、小规模摄食与残饵验证", 760, 468, 420, 70, { size: 20, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "范围外", 760, 558, 98, 28, { size: 16, bold: true, color: C.coral, align: "center", valign: "middle", fill: C.paleCoral, radius: "rounded-full", insets: { top: 0, right: 4, bottom: 0, left: 4 } });
  addText(slide, "生产级全海域部署与自动投喂控制，待验证后另行立项", 760, 598, 420, 46, { size: 19, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 4, 1);
  setNotes(slide, "55 秒", "主讲人 1", "完整目标覆盖投喂前、中、后和餐次复盘。本期按课程周期收敛为最小闭环，先确保数据可采、画面可用、标签可解释、结果可复现。生产级自动投喂不在本期承诺范围。讲完后交给主讲人2。", "《海洋牧场任务对应PPT》第31页；《项目规划_v3》第4、8页");
}

// 5. Technical route
{
  const slide = presentation.slides.add();
  addHeader(slide, "多源数据支撑投喂判断", 2, "技术方案 · 主讲人 2");
  const rows = [
    ["感知", "水下视频", "声呐（若配备）", "水面影像", "水质与海况"],
    ["处理", "增强复原", "鱼群密度与深度", "水花与饵料覆盖", "时间对齐与质量标记"],
    ["判断", "游动与聚集", "多模态印证", "残饵识别", "条件分层"],
    ["输出", "摄食四档", "投喂量区间", "速度与时长", "餐次记录"],
  ];
  const rowColors = [C.paleTeal, "#EAF4F6", "#EDF7F4", C.paleAmber];
  for (let r = 0; r < 4; r++) {
    const y = 174 + r * 102;
    addText(slide, rows[r][0], 68, y, 112, 64, { size: 20, bold: true, color: r === 3 ? C.ink : C.teal, align: "center", valign: "middle", fill: rowColors[r], radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    for (let c = 1; c < 5; c++) {
      const x = 206 + (c - 1) * 246;
      addText(slide, rows[r][c], x, y, 222, 64, { size: 19, bold: r === 3, color: C.ink, align: "center", valign: "middle", fill: r === 3 ? C.paleAmber : C.white, line: { style: "solid", fill: C.line, width: 1 }, radius: "rounded-xl", insets: { top: 0, right: 5, bottom: 0, left: 5 } });
    }
    if (r < 3) addShape(slide, "downArrow", 116, y + 70, 28, 25, partColors[1], { fill: "none", width: 0 });
  }
  addText(slide, "设计原则", 68, 590, 120, 30, { size: 16, bold: true, color: C.teal, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "单模态先做基线，多模态只在同一数据划分和训练预算下比较", 192, 584, 980, 42, { size: 22, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 5, 2);
  setNotes(slide, "1 分 10 秒", "主讲人 2", "技术路线分为感知、处理、判断和输出四层。视频是本期主通道，声呐、水面和水质用于印证。比较多模态增益时固定数据划分与训练预算，避免因为调参不一致产生虚假提升。", "《海洋牧场任务对应PPT》第31至32页；《项目规划_v3》第4至7页");
}

// 6. Methods and data
{
  const slide = presentation.slides.add();
  addHeader(slide, "画面可用是后续识别的前提", 2, "技术方案 · 主讲人 2");
  await addImage(slide, "image34.jpeg", 68, 182, 310, 226, { alt: "增强前的水下鱼群影像", fit: "cover", geometry: "roundRect", borderRadius: "rounded-2xl" });
  await addImage(slide, "image35.jpeg", 404, 182, 310, 226, { alt: "增强并检测后的水下鱼群影像", fit: "cover", geometry: "roundRect", borderRadius: "rounded-2xl" });
  addLabel(slide, "增强前", 164, 420, 118, C.muted, "#E6EEF0");
  addLabel(slide, "增强后与解析", 485, 420, 148, C.teal, C.paleTeal);
  addShape(slide, "rightArrow", 378, 276, 24, 28, C.teal, { fill: "none", width: 0 });
  addText(slide, "处理流水线", 68, 492, 150, 30, { size: 16, bold: true, color: C.teal, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "白平衡、CLAHE、去雾与去模糊形成传统基线；轻量模型作为增强方案", 68, 530, 646, 72, { size: 20, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addShape(slide, "rect", 760, 170, 4, 456, C.teal, { fill: "none", width: 0 });
  addText(slide, "数据与标签规则", 792, 178, 390, 40, { size: 26, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const rules = [
    ["餐次编号", "时间、点位、网箱与执行记录统一关联"],
    ["四档标签", "S0 至 S3，名称和边界案例在 M1 定稿"],
    ["训练划分", "按视频、网箱或日期分块，避免相邻帧泄漏"],
    ["双人复核", "残饵区域先算标注一致性，再评模型误差"],
  ];
  for (let i = 0; i < rules.length; i++) {
    const y = 246 + i * 90;
    addText(slide, rules[i][0], 792, y, 116, 30, { size: 17, bold: true, color: C.teal, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, rules[i][1], 920, y - 4, 278, 54, { size: 18, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    if (i < 3) addShape(slide, "line", 792, y + 62, 406, 1, "none", { style: "solid", fill: C.line, width: 1 });
  }
  addFooter(slide, 6, 2);
  setNotes(slide, "1 分 05 秒", "主讲人 2", "先展示原始画面与增强解析效果，说明传统方法用于建立可复现基线，轻量模型用于比较。随后说明餐次编号、四档标签、分块划分和双人复核四条数据规则。讲完后交给主讲人3。", "《海洋牧场任务对应PPT》第18、32页；《项目规划_v3》第5至7、12页");
}

// 7. Timeline
{
  const slide = presentation.slides.add();
  addHeader(slide, "10 月执行路线与里程碑", 3, "执行计划 · 主讲人 3");
  const weeks = [
    ["W1", "10.1–10.5", "定义与调研", "任务书\n标签规则\n方法基线", "M1 资料与定义定稿"],
    ["W2", "10.6–10.12", "操控与规范", "ROV 练习\n拍摄规范\n点位航线", "M2 按规范完成拍摄"],
    ["W3", "10.13–10.25", "采集与处理", "现场或模拟采集\n清晰化流水线\n二次解析", "M3 数据归档\nM4 流水线可跑"],
    ["W4", "10.26–10.31", "评估与交付", "指标计算\n消融归因\n报告与自检", "M5 评估完成\nM6 交付完成"],
  ];
  for (let i = 0; i < 4; i++) {
    const x = 68 + i * 288;
    addText(slide, weeks[i][0], x, 178, 84, 42, { size: 22, bold: true, color: C.navy, align: "center", valign: "middle", fill: C.amber, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, weeks[i][1], x + 96, 184, 152, 30, { size: 16, bold: true, color: C.muted, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, weeks[i][2], x, 244, 240, 40, { size: 25, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, weeks[i][3], x, 306, 240, 112, { size: 19, color: C.ink, lineSpacing: 1.15, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addShape(slide, "line", x, 442, 240, 1, "none", { style: "solid", fill: C.line, width: 1 });
    addText(slide, weeks[i][4], x, 466, 240, 88, { size: 17, bold: true, color: C.amber, lineSpacing: 1.15, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  }
  addText(slide, "控制点", 68, 598, 90, 30, { size: 16, bold: true, color: C.amber, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "W2 结束前不进入正式采集；W3 结束前完成可复现运行；W4 只做评估、归因与交付", 170, 592, 1010, 42, { size: 21, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 7, 3);
  setNotes(slide, "1 分 10 秒", "主讲人 3", "将原六阶段压缩为四周视图。第一周定规则，第二周练设备并固定规范，第三周采集与处理，第四周评估交付。三个控制点用于避免在定义、拍摄和代码未稳定时提前推进。", "《项目规划_v3》第8至15页");
}

// 8. Team split
{
  const slide = presentation.slides.add();
  addHeader(slide, "四人分工与交接关系", 3, "执行计划 · 主讲人 3");
  const people = [
    ["主讲人 1", "需求与数据定义", "任务书、四档标签\n餐次编号与资料库", "向 2 提供采集字段"],
    ["主讲人 2", "ROV 与数据采集", "操控日志、拍摄规范\n原始影像与环境记录", "向 3 交付标准素材"],
    ["主讲人 3", "图像与识别方法", "增强基线、轻量模型\n训练与推理脚本", "向 4 交付结果和日志"],
    ["主讲人 4", "评估与成果集成", "指标、消融与归因\n报告、限制与自检", "回收问题并组织复跑"],
  ];
  for (let i = 0; i < 4; i++) {
    const y = 176 + i * 111;
    addText(slide, String(i + 1).padStart(2, "0"), 68, y, 64, 64, { size: 25, bold: true, color: C.navy, align: "center", valign: "middle", fill: C.amber, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, people[i][0], 158, y, 130, 30, { size: 16, bold: true, color: C.amber, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, people[i][1], 158, y + 34, 230, 36, { size: 24, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, people[i][2], 430, y - 2, 370, 76, { size: 19, color: C.ink, valign: "middle", lineSpacing: 1.12, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, people[i][3], 850, y + 8, 330, 54, { size: 18, bold: true, color: C.muted, valign: "middle", fill: "#FFFFFF", line: { style: "solid", fill: C.line, width: 1 }, radius: "rounded-xl", insets: { top: 0, right: 10, bottom: 0, left: 10 } });
    if (i < 3) addShape(slide, "downArrow", 88, y + 78, 24, 24, C.amber, { fill: "none", width: 0 });
  }
  addText(slide, "共同规则", 68, 622, 100, 28, { size: 16, bold: true, color: C.amber, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "统一仓库、固定随机种子、每日提交，关键结果由非本人复跑", 178, 616, 990, 40, { size: 21, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 8, 3);
  setNotes(slide, "1 分 05 秒", "主讲人 3", "四人分工对应汇报结构，也对应交付链路。关键交接依次是采集字段、标准素材、结果日志和复跑反馈。共同规则确保版本、随机种子与结果都能追溯。讲完后交给主讲人4。", "《项目规划_v3》第9至16页；结合四人汇报要求进行职责重构");
}

// 9. Acceptance framework
{
  const slide = presentation.slides.add();
  addHeader(slide, "验收以可复现证据为准", 4, "验收与保障 · 主讲人 4");
  const groups = [
    ["识别质量", "四档准确率与分类别 F1\n相邻档与跨档错误\n加权 κ 与 95% 置信区间", C.paleCoral, C.coral],
    ["融合与残饵", "单模态与多模态对比\n残饵精确率、召回率、面积误差\n气泡、反光、碎屑与遮挡归因", C.paleTeal, C.teal],
    ["决策与追溯", "建议量偏差、方向与分位区间\n溶氧和水温条件分层\n记录完整率与餐次关联率", C.paleAmber, C.amber],
  ];
  for (let i = 0; i < 3; i++) {
    const x = 68 + i * 382;
    addShape(slide, "rect", x, 182, 342, 8, groups[i][3], { fill: "none", width: 0 });
    addText(slide, groups[i][0], x, 214, 342, 44, { size: 25, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, groups[i][1], x, 286, 342, 164, { size: 19, color: C.ink, lineSpacing: 1.2, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, i === 0 ? "留出集按视频、网箱或日期分块" : i === 1 ? "固定划分与训练预算" : "逐餐记录建议、执行与结果", x, 474, 342, 58, { size: 17, bold: true, color: groups[i][3], align: "center", valign: "middle", fill: groups[i][2], radius: "rounded-xl", insets: { top: 0, right: 6, bottom: 0, left: 6 } });
  }
  addShape(slide, "rect", 68, 572, 1106, 64, "#FFFFFF", { style: "solid", fill: C.line, width: 1 }, "rounded-xl");
  addText(slide, "判定原则", 92, 589, 116, 30, { size: 17, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "允许单项未达标，但必须报告数值、样本量、原因与下一步修正方案", 224, 582, 920, 44, { size: 21, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addFooter(slide, 9, 4);
  setNotes(slide, "1 分 15 秒", "主讲人 4", "验收不预设无法保证的漂亮数字，而是要求完整报告识别质量、融合与残饵、决策与追溯三类证据。尤其要防止相邻帧跨集、调参预算不一致和缺少样本量。单项未达标可以接受，但必须如实解释。", "《海洋牧场任务对应PPT》第32页");
}

// 10. Deliverables and risks
{
  const slide = presentation.slides.add();
  addHeader(slide, "交付清单与风险预案", 4, "验收与保障 · 主讲人 4");
  addText(slide, "四类交付", 68, 172, 190, 36, { size: 20, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const dels = [
    ["定义与数据", "任务书、四档定义、数据字典、时间对齐日志"],
    ["代码与方法", "环境、随机种子、训练与推理脚本、配置"],
    ["结果与证据", "完整指标表、消融表、残饵错误归因样例"],
    ["报告与边界", "8 至 12 页报告，说明迁移边界与限制"],
  ];
  for (let i = 0; i < 4; i++) {
    const y = 226 + i * 88;
    addText(slide, String(i + 1), 68, y, 44, 44, { size: 20, bold: true, color: C.white, align: "center", valign: "middle", fill: C.coral, radius: "rounded-full", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, dels[i][0], 132, y - 2, 148, 32, { size: 19, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, dels[i][1], 294, y - 8, 394, 58, { size: 18, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  }
  addShape(slide, "rect", 734, 168, 4, 466, C.coral, { fill: "none", width: 0 });
  addText(slide, "关键风险", 766, 172, 190, 36, { size: 20, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const risks = [
    ["设备或天气限制", "准备模拟环境，公开数据只作补充并单独标记"],
    ["画面质量不足", "先改进距离、补光和防抖，再使用增强算法"],
    ["声呐或水质缺失", "明确缺测范围，不对无数据条件做结论"],
    ["进度与版本风险", "每日提交、阶段快照、结果由非本人复跑"],
  ];
  for (let i = 0; i < risks.length; i++) {
    const y = 232 + i * 94;
    addText(slide, risks[i][0], 766, y, 178, 30, { size: 18, bold: true, color: C.ink, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, risks[i][1], 954, y - 7, 244, 58, { size: 17, color: C.muted, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    if (i < 3) addShape(slide, "line", 766, y + 66, 432, 1, "none", { style: "solid", fill: C.line, width: 1 });
  }
  addFooter(slide, 10, 4);
  setNotes(slide, "1 分 10 秒", "主讲人 4", "交付物按定义与数据、代码与方法、结果与证据、报告与边界四包组织。随后说明四类风险。公开数据只能补充现场数据，缺失模态必须明确标记，不能用推测替代实测。", "《海洋牧场任务对应PPT》第33页；《项目规划_v3》第16页");
}

// 11. Decision request
{
  const slide = presentation.slides.add();
  slide.background.fill = C.navy;
  addShape(slide, "rect", 0, 0, 16, 720, C.coral, { fill: "none", width: 0 });
  addText(slide, "立项结论", 78, 64, 300, 34, { size: 18, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "用一个月验证科学投喂的\n最小可行数据闭环", 78, 122, 780, 126, { size: 50, bold: true, color: C.white, valign: "middle", lineSpacing: 0.98, insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  const conclusions = [
    ["目标明确", "建立可复盘的摄食判断与投喂建议证据链"],
    ["范围可控", "聚焦采集、清晰化、标注和小规模验证"],
    ["验收可执行", "指标、数据划分、复跑与限制均有记录"],
  ];
  for (let i = 0; i < 3; i++) {
    const x = 78 + i * 372;
    addText(slide, conclusions[i][0], x, 312, 320, 32, { size: 18, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
    addText(slide, conclusions[i][1], x, 356, 320, 76, { size: 20, color: C.white, valign: "top", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  }
  addShape(slide, "line", 78, 486, 1104, 1, "none", { style: "solid", fill: "#FFFFFF/22", width: 1 });
  addText(slide, "申请事项", 78, 520, 150, 34, { size: 19, bold: true, color: C.coral, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "同意按 10 月计划启动，并协调 ROV 使用窗口、现场数据和第二复核人", 238, 510, 920, 56, { size: 25, bold: true, color: C.white, valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "谢谢聆听", 78, 640, 220, 34, { size: 20, color: "#C9E8ED", valign: "middle", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  addText(slide, "Q & A", 1054, 632, 128, 44, { size: 24, bold: true, color: C.navy, align: "center", valign: "middle", fill: C.coral, radius: "rounded-xl", insets: { top: 0, right: 0, bottom: 0, left: 0 } });
  setNotes(slide, "35 秒", "主讲人 4", "结论是本项目目标明确、范围可控、验收可执行。申请同意按十月计划启动，并协调水下机器人使用窗口、现场数据以及一名独立复核人员。随后进入问答。", "综合两份源PPT形成的立项申请");
}

const { finalizePresentation } = await import(pathToFileURL(path.join(SKILL_DIR, "container_tools", "artifact_tool_utils.mjs")).href);
const stagingDir = path.join(workspaceDir, ".codex-finalizer");
await fs.mkdir(stagingDir, { recursive: true });
const candidatePath = path.join(stagingDir, "task2-kickoff-candidate.pptx");
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);

const requirements = {
  explicitTotalSlideCount: 11,
  requiredNativeTableOwnerSlides: [],
  requiredNativeChartOwnerSlides: [],
};
const result = await finalizePresentation({
  ...requirements,
  workspaceDir,
  candidatePath,
  finalPath: FINAL_PPTX,
  pythonExecutable: RUNTIME_PYTHON,
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools", "inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools", "inspect_presentation_layout_geometry.py"),
  layoutArgs: [
    "--expected-slide-size-emu", "12192000,6858000",
    "--validate-bullet-geometry",
    "--validate-heading-fit",
  ],
  fontPolicy: { basis: "design", families: [FONT] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "task2-kickoff.validation.json"),
});

console.log(JSON.stringify({ finalPath: FINAL_PPTX, candidatePath, result }, null, 2));
