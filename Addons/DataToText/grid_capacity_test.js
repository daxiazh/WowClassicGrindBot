/**
 * 黑白网格编码容量评估和验证
 *
 * 目标：验证能否在合理尺寸的网格中存储DataToColor的108个字段
 */

// ============================================================
// 1. DataToColor字段定义和数据大小估算
// ============================================================

const DATACOLOR_FIELDS = [
    // 基础信息 (0-9)
    { idx: 0, name: "Init Flag", maxValue: 1, bits: 1 },
    { idx: 1, name: "Player X", maxValue: 999.99 * 10, bits: 24 },  // float*10 需要24位
    { idx: 2, name: "Player Y", maxValue: 999.99 * 10, bits: 24 },
    { idx: 3, name: "Player Facing", maxValue: 6.28 * 100000, bits: 24 },  // 弧度*100000
    { idx: 4, name: "Map ID", maxValue: 9999, bits: 14 },
    { idx: 5, name: "Player Level", maxValue: 255, bits: 8 },
    { idx: 6, name: "Corpse X", maxValue: 999.99 * 10, bits: 24 },
    { idx: 7, name: "Corpse Y", maxValue: 999.99 * 10, bits: 24 },
    { idx: 8, name: "Bits1", maxValue: 16777215, bits: 24 },  // 24个布尔标志
    { idx: 9, name: "Bits2", maxValue: 16777215, bits: 24 },

    // 玩家属性 (10-15)
    { idx: 10, name: "Player MaxHP", maxValue: 999999, bits: 20 },
    { idx: 11, name: "Player HP", maxValue: 999999, bits: 20 },
    { idx: 12, name: "Player MaxPower", maxValue: 999999, bits: 20 },
    { idx: 13, name: "Player Power", maxValue: 999999, bits: 20 },
    { idx: 14, name: "Player MaxMana/Runes", maxValue: 999999, bits: 20 },
    { idx: 15, name: "Player Mana/RuneDetail", maxValue: 999999, bits: 20 },

    // 目标信息 (16-19)
    { idx: 16, name: "Target Name 1-3", maxValue: 90909090, bits: 27 },  // ASCII编码
    { idx: 17, name: "Target Name 4-6", maxValue: 90909090, bits: 27 },
    { idx: 18, name: "Target MaxHP", maxValue: 9999999, bits: 24 },
    { idx: 19, name: "Target HP", maxValue: 9999999, bits: 24 },

    // 背包系统 (20-24)
    { idx: 20, name: "Bag Info", maxValue: 9999999, bits: 24 },
    { idx: 21, name: "Bag Slot", maxValue: 9999999, bits: 24 },
    { idx: 22, name: "Item ID", maxValue: 999999, bits: 20 },
    { idx: 23, name: "Equipment Slot", maxValue: 99, bits: 7 },
    { idx: 24, name: "Equipment ItemID", maxValue: 999999, bits: 20 },

    // 动作条状态 (25-37) - 压缩为位掩码
    { idx: 25, name: "ActionBar Current 1-24", maxValue: 16777215, bits: 24 },
    { idx: 26, name: "ActionBar Current 25-48", maxValue: 16777215, bits: 24 },
    { idx: 27, name: "ActionBar Current 49-72", maxValue: 16777215, bits: 24 },
    { idx: 28, name: "ActionBar Current 73-96", maxValue: 16777215, bits: 24 },
    { idx: 29, name: "ActionBar Current 97-120", maxValue: 16777215, bits: 24 },
    { idx: 30, name: "ActionBar Usable 1-24", maxValue: 16777215, bits: 24 },
    { idx: 31, name: "ActionBar Usable 25-48", maxValue: 16777215, bits: 24 },
    { idx: 32, name: "ActionBar Usable 49-72", maxValue: 16777215, bits: 24 },
    { idx: 33, name: "ActionBar Usable 73-96", maxValue: 16777215, bits: 24 },
    { idx: 34, name: "ActionBar Usable 97-120", maxValue: 16777215, bits: 24 },
    { idx: 35, name: "ActionBar Cost Meta", maxValue: 9999999, bits: 24 },
    { idx: 36, name: "ActionBar Cost Value", maxValue: 9999, bits: 14 },
    { idx: 37, name: "ActionBar Cooldown", maxValue: 99999999, bits: 27 },

    // 宠物信息 (38-39)
    { idx: 38, name: "Pet MaxHP", maxValue: 999999, bits: 20 },
    { idx: 39, name: "Pet HP", maxValue: 999999, bits: 20 },

    // Buff/Debuff (41-42, 55)
    { idx: 40, name: "Spell Range Mask", maxValue: 16777215, bits: 24 },
    { idx: 41, name: "Player Buff Mask", maxValue: 16777215, bits: 24 },
    { idx: 42, name: "Target Debuff Mask", maxValue: 16777215, bits: 24 },

    // 目标详细 (43)
    { idx: 43, name: "Target Level+Class", maxValue: 999999, bits: 20 },

    // 金钱 (44-45)
    { idx: 44, name: "Money Copper", maxValue: 999999, bits: 20 },
    { idx: 45, name: "Money Gold", maxValue: 999999, bits: 20 },

    // 其他 (46-54)
    { idx: 46, name: "Race+Class+Version", maxValue: 9999999, bits: 24 },
    { idx: 47, name: "UI Error Time", maxValue: 16777215, bits: 24 },
    { idx: 48, name: "Shapeshift Form", maxValue: 255, bits: 8 },
    { idx: 49, name: "Range", maxValue: 99999, bits: 17 },
    { idx: 50, name: "Player XP", maxValue: 9999999, bits: 24 },
    { idx: 51, name: "Player MaxXP", maxValue: 9999999, bits: 24 },
    { idx: 52, name: "UI Error Msg", maxValue: 16777215, bits: 24 },
    { idx: 53, name: "Player Casting SpellID", maxValue: 999999, bits: 20 },
    { idx: 54, name: "Durability+ComboPoints", maxValue: 99999, bits: 17 },

    // Buff/Debuff计数 (55)
    { idx: 55, name: "Buff/Debuff Counts", maxValue: 99999999, bits: 27 },

    // 目标GUID和NPC (56-59)
    { idx: 56, name: "Target NPC ID", maxValue: 999999, bits: 20 },
    { idx: 57, name: "Target GUID", maxValue: 16777215, bits: 24 },
    { idx: 58, name: "Target Casting SpellID", maxValue: 999999, bits: 20 },
    { idx: 59, name: "Target's Target", maxValue: 99, bits: 7 },

    // 战斗时间戳 (60-63)
    { idx: 60, name: "Last AutoShot", maxValue: 16777215, bits: 24 },
    { idx: 61, name: "Last Melee Swing", maxValue: 16777215, bits: 24 },
    { idx: 62, name: "Last Cast Event", maxValue: 16777215, bits: 24 },
    { idx: 63, name: "Last Cast SpellID", maxValue: 999999, bits: 20 },

    // 战斗日志 (64-67)
    { idx: 64, name: "Damage Done", maxValue: 9999999, bits: 24 },
    { idx: 65, name: "Damage Taken", maxValue: 9999999, bits: 24 },
    { idx: 66, name: "Creature Died", maxValue: 999999, bits: 20 },
    { idx: 67, name: "Miss Type", maxValue: 99, bits: 7 },

    // GUID相关 (68-69)
    { idx: 68, name: "Pet GUID", maxValue: 16777215, bits: 24 },
    { idx: 69, name: "Pet Target GUID", maxValue: 16777215, bits: 24 },

    // 施法计数 (70)
    { idx: 70, name: "Cast Counter", maxValue: 9999, bits: 14 },

    // 法术书/天赋 (71-72)
    { idx: 71, name: "Spellbook Queue", maxValue: 999999, bits: 20 },
    { idx: 72, name: "Talent Queue", maxValue: 9999999, bits: 24 },

    // Gossip (73)
    { idx: 73, name: "Gossip Queue", maxValue: 999, bits: 10 },

    // 其他战斗数据 (74-76)
    { idx: 74, name: "Custom Trigger", maxValue: 16777215, bits: 24 },
    { idx: 75, name: "Melee Attack Speed", maxValue: 999999, bits: 20 },
    { idx: 76, name: "Remain Cast Time", maxValue: 99999, bits: 17 },

    // 焦点目标 (77-78)
    { idx: 77, name: "Focus GUID", maxValue: 16777215, bits: 24 },
    { idx: 78, name: "Focus Target GUID", maxValue: 16777215, bits: 24 },

    // 玩家Buff详情 (79-80)
    { idx: 79, name: "Player Buff TextureID", maxValue: 9999999, bits: 24 },
    { idx: 80, name: "Player Buff Duration", maxValue: 99999, bits: 17 },

    // 目标Debuff详情 (81-82)
    { idx: 81, name: "Target Debuff TextureID", maxValue: 9999999, bits: 24 },
    { idx: 82, name: "Target Debuff Duration", maxValue: 99999, bits: 17 },

    // 目标Buff详情 (83-84)
    { idx: 83, name: "Target Buff TextureID", maxValue: 9999999, bits: 24 },
    { idx: 84, name: "Target Buff Duration", maxValue: 99999, bits: 17 },

    // 鼠标悬停 (85-87)
    { idx: 85, name: "Mouseover Level+Class", maxValue: 999999, bits: 20 },
    { idx: 86, name: "Mouseover NPC ID", maxValue: 999999, bits: 20 },
    { idx: 87, name: "Mouseover GUID", maxValue: 16777215, bits: 24 },

    // 其他 (88-96)
    { idx: 88, name: "Ranged Damage", maxValue: 99999, bits: 17 },
    { idx: 89, name: "Focus MaxHP", maxValue: 999999, bits: 20 },
    { idx: 90, name: "Focus HP", maxValue: 999999, bits: 20 },
    { idx: 91, name: "Focus Buff Mask", maxValue: 16777215, bits: 24 },
    { idx: 92, name: "Focus Buff TextureID", maxValue: 9999999, bits: 24 },
    { idx: 93, name: "Focus Buff Duration", maxValue: 99999, bits: 17 },
    { idx: 94, name: "Last Cast GCD", maxValue: 9999, bits: 14 },
    { idx: 95, name: "GCD Remain", maxValue: 9999, bits: 14 },
    { idx: 96, name: "SpellQueue+Lag", maxValue: 99999999, bits: 27 },

    // 拾取 (97)
    { idx: 97, name: "Loot Info", maxValue: 999, bits: 10 },

    // 聊天 (98-99)
    { idx: 98, name: "Chat Message Data", maxValue: 9999999, bits: 24 },
    { idx: 99, name: "Chat Message Meta", maxValue: 99999999, bits: 27 },

    // Bits3 (100)
    { idx: 100, name: "Bits3", maxValue: 16777215, bits: 24 },

    // SoftInteract (101-103)
    { idx: 101, name: "SoftInteract GUID", maxValue: 16777215, bits: 24 },
    { idx: 102, name: "SoftInteract NPC ID", maxValue: 999999, bits: 20 },
    { idx: 103, name: "SoftInteract Type", maxValue: 9, bits: 4 },

    // 玩家Debuff详情 (104-105)
    { idx: 104, name: "Player Debuff TextureID", maxValue: 9999999, bits: 24 },
    { idx: 105, name: "Player Debuff Duration", maxValue: 99999, bits: 17 },

    // 全局时间 (106)
    { idx: 106, name: "Global Time", maxValue: 16777215, bits: 24 },

    // 元数据 (107)
    { idx: 107, name: "Metadata", maxValue: 9999999, bits: 24 }
];

// ============================================================
// 2. 计算总数据量
// ============================================================

function calculateDataSize() {
    let totalBits = 0;
    let maxBitsPerField = 0;

    console.log("=".repeat(80));
    console.log("DataToColor 字段数据量分析");
    console.log("=".repeat(80));
    console.log("");

    for (const field of DATACOLOR_FIELDS) {
        totalBits += field.bits;
        if (field.bits > maxBitsPerField) {
            maxBitsPerField = field.bits;
        }
    }

    console.log(`字段总数: ${DATACOLOR_FIELDS.length}`);
    console.log(`总比特数: ${totalBits} bits`);
    console.log(`总字节数: ${Math.ceil(totalBits / 8)} bytes`);
    console.log(`最大单字段位数: ${maxBitsPerField} bits`);
    console.log("");

    // 添加CRC32校验 (32 bits)
    const crc32Bits = 32;
    const totalWithCRC = totalBits + crc32Bits;

    console.log(`加上CRC32校验 (32 bits):`);
    console.log(`  总比特数: ${totalWithCRC} bits`);
    console.log(`  总字节数: ${Math.ceil(totalWithCRC / 8)} bytes`);
    console.log("");

    return {
        fieldCount: DATACOLOR_FIELDS.length,
        totalBits: totalBits,
        totalBytes: Math.ceil(totalBits / 8),
        totalBitsWithCRC: totalWithCRC,
        totalBytesWithCRC: Math.ceil(totalWithCRC / 8),
        maxBitsPerField: maxBitsPerField
    };
}

// ============================================================
// 3. 网格容量计算
// ============================================================

function calculateGridCapacity(gridSize, errorCorrectionPercent = 0) {
    // 网格总单元格数
    const totalCells = gridSize * gridSize;

    // 保留边框用于定位标记 (类似QR码的定位图案)
    // 四个角各保留 3x3 的定位标记
    const cornerMarkerSize = 3;
    const cornerMarkers = 4 * (cornerMarkerSize * cornerMarkerSize);

    // 保留一行/列用于同步和校准
    const syncMarkers = (gridSize * 2) - 4; // 上边和左边，减去角落重复

    // 可用于数据的单元格数
    const dataCells = totalCells - cornerMarkers - syncMarkers;

    // 减去纠错冗余
    const dataCellsAfterEC = Math.floor(dataCells * (1 - errorCorrectionPercent / 100));

    // 每个单元格存储1位
    const capacityBits = dataCellsAfterEC;
    const capacityBytes = Math.floor(capacityBits / 8);

    return {
        gridSize: gridSize,
        totalCells: totalCells,
        cornerMarkers: cornerMarkers,
        syncMarkers: syncMarkers,
        dataCells: dataCells,
        errorCorrectionPercent: errorCorrectionPercent,
        dataCellsAfterEC: dataCellsAfterEC,
        capacityBits: capacityBits,
        capacityBytes: capacityBytes,
        pixelSize: gridSize  // 假设每个单元格是1个像素
    };
}

// ============================================================
// 4. 验证不同网格尺寸
// ============================================================

function testGridSizes() {
    const dataSize = calculateDataSize();
    const requiredBits = dataSize.totalBitsWithCRC;

    console.log("=".repeat(80));
    console.log("黑白网格编码容量测试");
    console.log("=".repeat(80));
    console.log("");
    console.log(`需要存储: ${requiredBits} bits (${dataSize.totalBytesWithCRC} bytes)`);
    console.log("");

    const gridSizes = [32, 48, 64, 80, 96, 112, 128];
    const errorCorrectionLevels = [0, 10, 20, 30];

    console.log("测试不同网格尺寸和纠错等级:");
    console.log("-".repeat(80));
    console.log("");

    const results = [];

    for (const gridSize of gridSizes) {
        for (const ecLevel of errorCorrectionLevels) {
            const capacity = calculateGridCapacity(gridSize, ecLevel);
            const sufficient = capacity.capacityBits >= requiredBits;
            const utilization = ((requiredBits / capacity.capacityBits) * 100).toFixed(1);

            results.push({
                gridSize: gridSize,
                ecLevel: ecLevel,
                capacity: capacity,
                sufficient: sufficient,
                utilization: utilization
            });

            const status = sufficient ? "✅ 足够" : "❌ 不足";
            const spare = capacity.capacityBits - requiredBits;

            console.log(`${gridSize}x${gridSize} 网格, ${ecLevel}% 纠错:`);
            console.log(`  容量: ${capacity.capacityBits} bits (${capacity.capacityBytes} bytes)`);
            console.log(`  状态: ${status}`);
            if (sufficient) {
                console.log(`  利用率: ${utilization}%`);
                console.log(`  剩余: ${spare} bits (${Math.floor(spare / 8)} bytes)`);
            } else {
                console.log(`  缺少: ${-spare} bits (${Math.ceil(-spare / 8)} bytes)`);
            }
            console.log("");
        }
    }

    return results;
}

// ============================================================
// 5. 推荐方案
// ============================================================

function recommendSolution() {
    const dataSize = calculateDataSize();
    const results = testGridSizes();

    console.log("=".repeat(80));
    console.log("推荐方案");
    console.log("=".repeat(80));
    console.log("");

    // 找到最小的足够尺寸（带20%纠错）
    const withEC20 = results.filter(r => r.ecLevel === 20 && r.sufficient);

    if (withEC20.length > 0) {
        const recommended = withEC20[0];
        console.log("✅ 推荐方案: 黑白网格编码完全可行！");
        console.log("");
        console.log(`推荐网格尺寸: ${recommended.gridSize}x${recommended.gridSize}`);
        console.log(`纠错等级: ${recommended.ecLevel}%`);
        console.log(`容量: ${recommended.capacity.capacityBits} bits`);
        console.log(`利用率: ${recommended.utilization}%`);
        console.log(`显示尺寸: ${recommended.gridSize}x${recommended.gridSize} 像素`);
        console.log("");
        console.log("优势:");
        console.log("  - 完全避免颜色gamma问题");
        console.log("  - 内置20%纠错能力");
        console.log("  - 尺寸合理，不占用太多屏幕空间");
        console.log("  - 扫描速度快");
        console.log("");
    } else {
        console.log("❌ 警告: 没有找到合适的网格尺寸（带20%纠错）");
    }

    // 显示不同场景的建议
    console.log("不同场景建议:");
    console.log("-".repeat(80));

    const scenarios = [
        { name: "最小尺寸 (无纠错)", ecLevel: 0 },
        { name: "平衡方案 (10% 纠错)", ecLevel: 10 },
        { name: "推荐方案 (20% 纠错)", ecLevel: 20 },
        { name: "高可靠 (30% 纠错)", ecLevel: 30 }
    ];

    for (const scenario of scenarios) {
        const matches = results.filter(r => r.ecLevel === scenario.ecLevel && r.sufficient);
        if (matches.length > 0) {
            const best = matches[0];
            console.log(`${scenario.name}:`);
            console.log(`  网格: ${best.gridSize}x${best.gridSize} (${best.gridSize}px 正方形)`);
            console.log(`  容量: ${best.capacity.capacityBits} bits`);
            console.log(`  利用率: ${best.utilization}%`);
            console.log("");
        }
    }
}

// ============================================================
// 6. 编码格式设计
// ============================================================

function designEncodingFormat() {
    console.log("=".repeat(80));
    console.log("黑白网格编码格式设计");
    console.log("=".repeat(80));
    console.log("");

    console.log("格式结构:");
    console.log("-".repeat(80));
    console.log("");
    console.log("1. 定位标记 (4个角落, 各3x3):");
    console.log("   ┌─────┐       ┌─────┐");
    console.log("   │█ █ █│       │█ █ █│");
    console.log("   │ █ █ │  ...  │ █ █ │");
    console.log("   │█ █ █│       │█ █ █│");
    console.log("   └─────┘       └─────┘");
    console.log("");
    console.log("2. 同步图案 (顶部和左侧边缘):");
    console.log("   交替黑白单元格，用于校准和对齐");
    console.log("");
    console.log("3. 数据区域:");
    console.log("   - 头部 (32 bits): CRC32 校验码");
    console.log("   - 数据 (~2000 bits): 所有108个字段，按位打包");
    console.log("   - 纠错 (可选): Reed-Solomon 或简单奇偶校验");
    console.log("");
    console.log("4. 读取顺序:");
    console.log("   从左到右，从上到下，按行扫描");
    console.log("");
}

// ============================================================
// 7. 实际示例
// ============================================================

function simulateEncoding() {
    console.log("=".repeat(80));
    console.log("编码示例");
    console.log("=".repeat(80));
    console.log("");

    // 模拟编码几个字段
    const sampleData = {
        playerHP: 1234,
        playerMaxHP: 5678,
        playerLevel: 60,
        targetHP: 890,
        inCombat: true,
        isMounted: false
    };

    console.log("示例数据:");
    console.log(JSON.stringify(sampleData, null, 2));
    console.log("");

    // 转为二进制
    function toBinary(value, bits) {
        return value.toString(2).padStart(bits, '0');
    }

    const binary = [
        toBinary(sampleData.playerHP, 20),
        toBinary(sampleData.playerMaxHP, 20),
        toBinary(sampleData.playerLevel, 8),
        toBinary(sampleData.targetHP, 20),
        toBinary(sampleData.inCombat ? 1 : 0, 1),
        toBinary(sampleData.isMounted ? 1 : 0, 1)
    ].join('');

    console.log("二进制编码:");
    console.log(binary);
    console.log(`长度: ${binary.length} bits`);
    console.log("");

    console.log("在网格中的表示 (█=1, ░=0):");
    const gridSize = 10;
    for (let i = 0; i < Math.min(gridSize * gridSize, binary.length); i++) {
        process.stdout.write(binary[i] === '1' ? '█' : '░');
        if ((i + 1) % gridSize === 0) {
            console.log("");
        }
    }
    console.log("");
}

// ============================================================
// 主程序
// ============================================================

console.log("\n");
console.log("█".repeat(80));
console.log("  黑白网格编码容量评估 - DataToColor字段存储验证");
console.log("█".repeat(80));
console.log("\n");

// 执行所有测试
calculateDataSize();
testGridSizes();
recommendSolution();
designEncodingFormat();
simulateEncoding();

console.log("=".repeat(80));
console.log("结论");
console.log("=".repeat(80));
console.log("");
console.log("✅ 黑白网格编码方案完全可行！");
console.log("");
console.log("关键发现:");
console.log("  1. 108个字段只需要 ~2000 bits (~250 bytes)");
console.log("  2. 64x64网格 (带20%纠错) 就足够存储所有数据");
console.log("  3. 64x64像素非常小，不会占用太多屏幕空间");
console.log("  4. 完全避免了颜色gamma/亮度/对比度问题");
console.log("  5. 扫描速度会比OCR文本快得多");
console.log("");
console.log("下一步:");
console.log("  1. 在Lua中实现网格生成代码");
console.log("  2. 在C#中实现网格扫描和解码");
console.log("  3. 测试不同显示器和gamma设置下的鲁棒性");
console.log("");
