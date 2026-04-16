% GameFormulasPlot_V3.m
% 奇美拉协议 - 核心数值曲线矩阵可视化 (V3.0 - 完美锚点版)

figure('Name', '奇美拉协议 - 数值引擎', 'Position', [100, 100, 1200, 800]);
sgtitle('奇美拉协议 - 核心数值曲线矩阵 (V3.0 锚点版)', 'FontSize', 18, 'FontWeight', 'bold');

%% 1. 移动速度曲线
subplot(2, 2, 1); hold on; grid on;
mass = linspace(0.5, 20, 100);
engine_powers = [10, 30, 50];
base_speed = 1.0;
for p = engine_powers
    plot(mass, base_speed + (p ./ mass), 'LineWidth', 2, 'DisplayName', ['推力 = ', num2str(p)]);
end
yline(base_speed, 'r--', '基础保底移速 (1.0)', 'LineWidth', 1.5, 'LabelHorizontalAlignment', 'left');
title('移动速度 vs 质量', 'FontSize', 12); xlabel('机甲质量 (Mass)'); ylabel('最终移速'); legend('Location', 'northeast');

%% 2. 硬直时间曲线
subplot(2, 2, 2); hold on; grid on;
impulse = linspace(0, 200, 500);
masses_stagger = [2.0, 5.0, 10.0];
max_stagger = 2.5;
for m = masses_stagger
    delta_v = impulse ./ m;
    stagger = zeros(size(delta_v));
    idx = delta_v >= 2.0; 
    stagger(idx) = max_stagger .* (1 - exp(- (delta_v(idx) .* 0.05) ./ max_stagger));
    plot(impulse, stagger, 'LineWidth', 2, 'DisplayName', ['机甲质量 = ', num2str(m)]);
end
yline(max_stagger, 'r--', '硬直上限 (2.5s)', 'LineWidth', 1.5, 'LabelHorizontalAlignment', 'left');
title('硬直时间 vs 冲击力', 'FontSize', 12); xlabel('受到的武器冲击力 (Impulse)'); ylabel('硬直时间 (秒)'); legend('Location', 'southeast');

%% 3. 冷却缩减曲线 (核心锚点测试)
subplot(2, 2, 3); hold on; grid on;
score = linspace(0, 1000, 200); 
base_cd = 2.0;
min_cd = 0.2;

% 策划锚点
target_score = 100;
target_cd = 1.0;

% 逆向推导常数 K
ratio = (target_cd - min_cd) / (base_cd - min_cd);
decay_constant = -target_score / log(ratio);

% 核心公式
cooldown = min_cd + (base_cd - min_cd) .* exp(-score ./ decay_constant);

plot(score, cooldown, 'Color', [0.5, 0, 0.8], 'LineWidth', 3, 'DisplayName', '指数衰减 (穿过锚点)');

% 绘制锚点十字星
plot(target_score, target_cd, 'r+', 'MarkerSize', 15, 'LineWidth', 2, 'DisplayName', '策划基准点 (100, 1.0s)');

yline(min_cd, 'r--', '极限射速上限 (0.2s)', 'LineWidth', 1.5, 'LabelHorizontalAlignment', 'left');
title('射速冷却 vs 攻速评分 (基准锚点锁定)', 'FontSize', 12); xlabel('武器加速评分 (Attack Speed Score)'); ylabel('实际冷却时间 (秒)'); legend('Location', 'northeast');

%% 4. 动能冲撞伤害曲线
subplot(2, 2, 4); hold on; grid on;
velocity = linspace(0, 30, 100);
conv_rate = 2.0;
calc_ram_dmg = @(m_red, v) max(0, 0.5 .* m_red .* (v.^2) .* conv_rate .* (0.5 .* m_red .* (v.^2) .* conv_rate >= 5));
plot(velocity, calc_ram_dmg((2*2)/(2+2), velocity), 'LineWidth', 2, 'DisplayName', '轻型撞轻型');
plot(velocity, calc_ram_dmg((15*15)/(15+15), velocity), 'LineWidth', 2, 'DisplayName', '重型撞重型');
plot(velocity, calc_ram_dmg((15*2)/(15+2), velocity), '-.', 'LineWidth', 2, 'DisplayName', '重型撞轻型');
title('真实动能伤害 vs 相对速度', 'FontSize', 12); xlabel('碰撞相对速度 (m/s)'); ylabel('造成的物理伤害 (HP)'); legend('Location', 'northwest');
hold off;

% MovementSpeed_Dampener.m
% 奇美拉协议 - 移动速度公式优化 (引入质量阻尼常数)

figure('Name', '奇美拉协议 - 移速公式优化对比', 'Position', [200, 200, 900, 600]);
hold on; grid on;

% 设定自变量：质量从 0 到 20 吨
mass = linspace(0, 20, 200); 
base_speed = 1.0;

% 👇 策划调控核心参数：质量阻尼常数
% 这个值越大，整体曲线越平缓，轻重机甲的速度差越小
mass_dampener = 5.0; 

% ==========================================
% 1. 绘制旧公式 (断崖式) 作为反面教材对比
% 公式: Speed = 1.0 + (Power / max(mass, 0.5))
% ==========================================
% 我们以引擎推力 P=30 为例来看旧公式的崩溃表现
old_speed_30 = base_speed + (30 ./ max(mass, 0.5));
plot(mass, old_speed_30, 'Color', [0.7 0.7 0.7], 'LineStyle', '--', 'LineWidth', 2, ...
    'DisplayName', '旧公式 (推力=30, 前期断崖式下跌)');

% ==========================================
% 2. 绘制新公式 (平滑阻尼版)
% 公式: Speed = 1.0 + (Power / (mass + mass_dampener))
% ==========================================
engine_powers = [10, 30, 50];
colors = lines(length(engine_powers)); % 获取 MATLAB 默认的优美配色

for i = 1:length(engine_powers)
    p = engine_powers(i);
    
    % 新公式：分母强制加上了虚拟的阻尼质量
    new_speed = base_speed + (p ./ (mass + mass_dampener));
    
    plot(mass, new_speed, 'Color', colors(i,:), 'LineWidth', 3, ...
        'DisplayName', ['新公式 (推力=', num2str(p), ')']);
end

% 绘制基础保底移速线
yline(base_speed, 'r-', '基础保底移速 (1.0)', 'LineWidth', 1.5, 'LabelHorizontalAlignment', 'left');

% 图表美化与标签
title(['移动速度 vs 质量 (当前质量阻尼常数 C = ', num2str(mass_dampener), ')'], 'FontSize', 15, 'FontWeight', 'bold');
xlabel('机甲真实质量 (Mass)', 'FontSize', 12);
ylabel('最终移速 (Speed)', 'FontSize', 12);
legend('Location', 'northeast', 'FontSize', 11);

% 限制 Y 轴的显示范围，切掉旧公式那毫无意义的 60+ 尖峰，聚焦于实际游戏区间
ylim([0, 12]); 
xlim([0, 20]);

% 优化坐标轴边框
ax = gca;
ax.LineWidth = 1.2;
ax.FontSize = 11;

hold off;