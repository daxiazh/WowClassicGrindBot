#!/usr/bin/env node
/**
 * 黑白网格编码容量评估和验证程序
 *
 * 目的：验证自定义黑白网格是否能容纳DataToColor的108个字段
 */

// ============================================================================
// 1. DataToColor字段定义（108个字段）
// ============================================================================

const FIELD_DEFINITIONS = [
    // 基础信息 (0-9) - 每个字段最多24位
    { id: 0,   name: 'INIT_FLAG',        bits: 24, desc: '初始化标志' },
    { id: 1,   name: 'P_X',              bits: 24, desc: '玩家X坐标 (float*10)' },
    { id: 2,   name: 'P_Y',              bits: 24, desc: '玩家Y坐标 (float*10)' },
    { id: 3,   name: 'P_FACING',         bits: 24, desc: '玩家朝向' },
    { id: 4,   name: 'P_MAP',            bits: 24, desc: '地图ID' },
    { id: 5,   name: 'P_LEVEL',          bits: 24, desc: '玩家等级' },
    { id: 6,   name: 'CORPSE_X',         bits: 24, desc: '尸体X坐标' },
    { id: 7,   name: 'CORPSE_Y',         bits: 24, desc: '尸体Y坐标' },
    { id: 8,   name: 'BITS1',            bits: 24, desc: '布尔标志位1' },
    { id: 9,   name: 'BITS2',            bits: 24, desc: '布尔标志位2' },

    // 玩家属性 (10-15)
    { id: 10,  name: 'P_HP_MAX',         bits: 24, desc: '玩家最大生命值' },
    { id: 11,  name: 'P_HP',             bits: 24, desc: '玩家当前生命值' },
    { id: 12,  name: 'P_POWER_MAX',      bits: 24, desc: '玩家最大能量' },
    { id: 13,  name: 'P_POWER',          bits: 24, desc: '玩家当前能量' },
    { id: 14,  name: 'P_MANA_MAX',       bits: 24, desc: '玩家最大法力/符文数' },
    { id: 15,  name: 'P_MANA',           bits: 24, desc: '玩家当前法力/符文详情' },

    // 目标信息 (16-19)
    { id: 16,  name: 'T_NAME_1',         bits: 24, desc: '目标名称前3字符' },
    { id: 17,  name: 'T_NAME_2',         bits: 24, desc: '目标名称后3字符' },
    { id: 18,  name: 'T_HP_MAX',         bits: 24, desc: '目标最大生命值' },
    { id: 19,  name: 'T_HP',             bits: 24, desc: '目标当前生命值' },

    // 背包系统 (20-24)
    { id: 20,  name: 'BAG_INFO',         bits: 24, desc: '背包信息' },
    { id: 21,  name: 'BAG_SLOT',         bits: 24, desc: '背包槽位' },
    { id: 22,  name: 'BAG_ITEM_ID',      bits: 24, desc: '背包物品ID' },
    { id: 23,  name: 'EQUIP_SLOT',       bits: 24, desc: '装备槽位' },
    { id: 24,  name: 'EQUIP_ITEM_ID',    bits: 24, desc: '装备物品ID' },

    // 动作条状态 (25-37)
    { id: 25,  name: 'ACTION_CURRENT_1', bits: 24, desc: '动作条1-24当前状态' },
    { id: 26,  name: 'ACTION_CURRENT_2', bits: 24, desc: '动作条25-48当前状态' },
    { id: 27,  name: 'ACTION_CURRENT_3', bits: 24, desc: '动作条49-72当前状态' },
    { id: 28,  name: 'ACTION_CURRENT_4', bits: 24, desc: '动作条73-96当前状态' },
    { id: 29,  name: 'ACTION_CURRENT_5', bits: 24, desc: '动作条97-120当前状态' },
    { id: 30,  name: 'ACTION_USABLE_1',  bits: 24, desc: '动作条1-24可用性' },
    { id: 31,  name: 'ACTION_USABLE_2',  bits: 24, desc: '动作条25-48可用性' },
    { id: 32,  name: 'ACTION_USABLE_3',  bits: 24, desc: '动作条49-72可用性' },
    { id: 33,  name: 'ACTION_USABLE_4',  bits: 24, desc: '动作条73-96可用性' },
    { id: 34,  name: 'ACTION_USABLE_5',  bits: 24, desc: '动作条97-120可用性' },
    { id: 35,  name: 'ACTION_COST_META', bits: 24, desc: '动作条消耗元数据' },
    { id: 36,  name: 'ACTION_COST',      bits: 24, desc: '动作条消耗值' },
    { id: 37,  name: 'ACTION_CD',        bits: 24, desc: '动作条冷却' },

    // 宠物信息 (38-39)
    { id: 38,  name: 'PET_HP_MAX',       bits: 24, desc: '宠物最大生命值' },
    { id: 39,  name: 'PET_HP',           bits: 24, desc: '宠物当前生命值' },

    // 其他 (40-107)
    { id: 40,  name: 'SPELL_RANGE',      bits: 24, desc: '法术距离检测' },
    { id: 41,  name: 'P_BUFF_MASK',      bits: 24, desc: '玩家Buff掩码' },
    { id: 42,  name: 'T_DEBUFF_MASK',    bits: 24, desc: '目标Debuff掩码' },
    { id: 43,  name: 'T_LEVEL_CLASS',    bits: 24, desc: '目标等级+分类' },
    { id: 44,  name: 'MONEY_COPPER',     bits: 24, desc: '金钱(铜)' },
    { id: 45,  name: 'MONEY_GOLD',       bits: 24, desc: '金钱(金)' },
    { id: 46,  name: 'RACE_CLASS_VER',   bits: 24, desc: '种族+职业+版本' },
    { id: 47,  name: 'UI_ERROR_TIME',    bits: 24, desc: 'UI错误时间' },
    { id: 48,  name: 'SHAPESHIFT',       bits: 24, desc: '变形形态' },
    { id: 49,  name: 'RANGE',            bits: 24, desc: '距离范围' },
    { id: 50,  name: 'P_XP',             bits: 24, desc: '玩家经验值' },
    { id: 51,  name: 'P_XP_MAX',         bits: 24, desc: '玩家升级所需经验' },
    { id: 52,  name: 'UI_ERROR',         bits: 24, desc: 'UI错误消息' },
    { id: 53,  name: 'P_CASTING',        bits: 24, desc: '玩家施法ID' },
    { id: 54,  name: 'DURABILITY_COMBO', bits: 24, desc: '耐久度+连击点' },
    { id: 55,  name: 'AURA_COUNT',       bits: 24, desc: 'Buff/Debuff计数' },
    { id: 56,  name: 'T_NPC_ID',         bits: 24, desc: '目标NPC ID' },
    { id: 57,  name: 'T_GUID',           bits: 24, desc: '目标GUID' },
    { id: 58,  name: 'T_CASTING',        bits: 24, desc: '目标施法ID' },
    { id: 59,  name: 'TARGET_TARGET',    bits: 24, desc: '目标的目标' },
    { id: 60,  name: 'LAST_AUTO_SHOT',   bits: 24, desc: '最后自动射击' },
    { id: 61,  name: 'LAST_MELEE',       bits: 24, desc: '最后近战攻击' },
    { id: 62,  name: 'LAST_CAST_EVENT',  bits: 24, desc: '最后施法事件' },
    { id: 63,  name: 'LAST_CAST_SPELL',  bits: 24, desc: '最后施法法术ID' },
    { id: 64,  name: 'DMG_DONE',         bits: 24, desc: '造成伤害' },
    { id: 65,  name: 'DMG_TAKEN',        bits: 24, desc: '受到伤害' },
    { id: 66,  name: 'CREATURE_DIED',    bits: 24, desc: '怪物死亡' },
    { id: 67,  name: 'MISS_TYPE',        bits: 24, desc: 'Miss类型' },
    { id: 68,  name: 'PET_GUID',         bits: 24, desc: '宠物GUID' },
    { id: 69,  name: 'PET_TARGET_GUID',  bits: 24, desc: '宠物目标GUID' },
    { id: 70,  name: 'CAST_NUM',         bits: 24, desc: '施法计数' },
    { id: 71,  name: 'SPELLBOOK',        bits: 24, desc: '法术书队列' },
    { id: 72,  name: 'TALENT',           bits: 24, desc: '天赋队列' },
    { id: 73,  name: 'GOSSIP',           bits: 24, desc: 'Gossip队列' },
    { id: 74,  name: 'CUSTOM_TRIGGER',   bits: 24, desc: '自定义触发器' },
    { id: 75,  name: 'MELEE_SPEED',      bits: 24, desc: '近战速度' },
    { id: 76,  name: 'CAST_REMAIN',      bits: 24, desc: '剩余施法时间' },
    { id: 77,  name: 'FOCUS_GUID',       bits: 24, desc: '焦点GUID' },
    { id: 78,  name: 'FOCUS_TARGET_GUID',bits: 24, desc: '焦点目标GUID' },
    { id: 79,  name: 'P_BUFF_ID',        bits: 24, desc: '玩家Buff纹理ID' },
    { id: 80,  name: 'P_BUFF_DUR',       bits: 24, desc: '玩家Buff持续时间' },
    { id: 81,  name: 'T_DEBUFF_ID',      bits: 24, desc: '目标Debuff纹理ID' },
    { id: 82,  name: 'T_DEBUFF_DUR',     bits: 24, desc: '目标Debuff持续时间' },
    { id: 83,  name: 'T_BUFF_ID',        bits: 24, desc: '目标Buff纹理ID' },
    { id: 84,  name: 'T_BUFF_DUR',       bits: 24, desc: '目标Buff持续时间' },
    { id: 85,  name: 'MO_LEVEL_CLASS',   bits: 24, desc: '鼠标悬停等级+分类' },
    { id: 86,  name: 'MO_NPC_ID',        bits: 24, desc: '鼠标悬停NPC ID' },
    { id: 87,  name: 'MO_GUID',          bits: 24, desc: '鼠标悬停GUID' },
    { id: 88,  name: 'RANGED_DMG',       bits: 24, desc: '远程伤害' },
    { id: 89,  name: 'FOCUS_HP_MAX',     bits: 24, desc: '焦点最大生命值' },
    { id: 90,  name: 'FOCUS_HP',         bits: 24, desc: '焦点当前生命值' },
    { id: 91,  name: 'FOCUS_BUFF_MASK',  bits: 24, desc: '焦点Buff掩码' },
    { id: 92,  name: 'FOCUS_BUFF_ID',    bits: 24, desc: '焦点Buff纹理ID' },
    { id: 93,  name: 'FOCUS_BUFF_DUR',   bits: 24, desc: '焦点Buff持续时间' },
    { id: 94,  name: 'LAST_GCD',         bits: 24, desc: '最后GCD' },
    { id: 95,  name: 'GCD_REMAIN',       bits: 24, desc: 'GCD剩余时间' },
    { id: 96,  name: 'SPELL_QUEUE_LAG',  bits: 24, desc: 'SpellQueue+延迟' },
    { id: 97,  name: 'LOOT_INFO',        bits: 24, desc: '拾取信息' },
    { id: 98,  name: 'CHAT_MSG_DATA',    bits: 24, desc: '聊天消息数据' },
    { id: 99,  name: 'CHAT_MSG_META',    bits: 24, desc: '聊天消息元数据' },
    { id: 100, name: 'BITS3',            bits: 24, desc: '布尔标志位3' },
    { id: 101, name: 'SOFT_GUID',        bits: 24, desc: 'SoftInteract GUID' },
    { id: 102, name: 'SOFT_NPC_ID',      bits: 24, desc: 'SoftInteract NPC ID' },
    { id: 103, name: 'SOFT_TYPE',        bits: 24, desc: 'SoftInteract类型' },
    { id: 104, name: 'P_DEBUFF_ID',      bits: 24, desc: '玩家Debuff纹理ID' },
    { id: 105, name: 'P_DEBUFF_DUR',     bits: 24, desc: '玩家Debuff持续时间' },
    { id: 106, name: 'GLOBAL_TIME',      bits: 24, desc: '全局时间戳' },
    { id: 107, name: 'METADATA',         bits: 24, desc: '元数据/验证' },
];

// ============================================================================
// 2. 容量计算
// ============================================================================

function calculateDataRequirements() {
    console.log('='.repeat(80));
    console.log('数据需求分析');
    console.log('='.repeat(80));

    const totalFields = FIELD_DEFINITIONS.length;
    const bitsPerField = 24;
    const totalDataBits = totalFields * bitsPerField;
    const totalDataBytes = Math.ceil(totalDataBits / 8);

    console.log(`\n📊 基础数据统计:`);
    console.log(`   字段总数: ${totalFields}`);
    console.log(`   每字段位数: ${bitsPerField} bits`);
    console.log(`   数据总位数: ${totalDataBits} bits`);
    console.log(`   数据总字节: ${totalDataBytes} bytes`);

    // CRC32校验码
    const crc32Bytes = 4;

    // 版本和元数据
    const metadataBytes = 4; // 版本号、字段数等

    // 总需求
    const totalRequiredBytes = totalDataBytes + crc32Bytes + metadataBytes;
    const totalRequiredBits = totalRequiredBytes * 8;

    console.log(`\n📦 额外开销:`);
    console.log(`   CRC32校验: ${crc32Bytes} bytes`);
    console.log(`   元数据: ${metadataBytes} bytes`);
    console.log(`   总需求: ${totalRequiredBytes} bytes (${totalRequiredBits} bits)`);

    return { totalRequiredBytes, totalRequiredBits };
}

// ============================================================================
// 3. 网格尺寸计算
// ============================================================================

function calculateGridSizes(requiredBits) {
    console.log('\n' + '='.repeat(80));
    console.log('黑白网格尺寸分析');
    console.log('='.repeat(80));

    const gridSizes = [];

    // 测试不同的网格尺寸
    for (let size = 10; size <= 100; size += 5) {
        // 定位标记占用 (4个角 + 中心校准点)
        const alignmentMarkers = 9; // 3x3 corners

        // 可用于数据的单元格
        const totalCells = size * size;
        const dataCells = totalCells - alignmentMarkers;

        // 纠错码（使用简单的重复码或校验和）
        const errorCorrectionRatio = 0.15; // 15%纠错能力
        const errorCorrectionCells = Math.ceil(dataCells * errorCorrectionRatio);
        const usableDataCells = dataCells - errorCorrectionCells;

        // 每个单元格1位数据
        const capacityBits = usableDataCells;
        const capacityBytes = Math.floor(capacityBits / 8);

        if (capacityBits >= requiredBits) {
            gridSizes.push({
                size,
                totalCells,
                dataCells,
                errorCorrectionCells,
                usableDataCells,
                capacityBits,
                capacityBytes,
                overhead: ((capacityBits - requiredBits) / requiredBits * 100).toFixed(1),
                pixelSize: size // 像素尺寸
            });
        }
    }

    console.log(`\n需要容纳: ${requiredBits} bits\n`);
    console.log('可行的网格尺寸:');
    console.log('─'.repeat(80));
    console.log('尺寸    总单元格  数据单元  纠错单元  可用单元  容量(bits)  容量(bytes)  富余%');
    console.log('─'.repeat(80));

    gridSizes.slice(0, 10).forEach(g => {
        console.log(
            `${g.size}x${g.size}`.padEnd(8) +
            `${g.totalCells}`.padEnd(10) +
            `${g.dataCells}`.padEnd(10) +
            `${g.errorCorrectionCells}`.padEnd(10) +
            `${g.usableDataCells}`.padEnd(10) +
            `${g.capacityBits}`.padEnd(12) +
            `${g.capacityBytes}`.padEnd(13) +
            `${g.overhead}%`
        );
    });

    return gridSizes;
}

// ============================================================================
// 4. 编码/解码模拟
// ============================================================================

function simulateEncoding(gridSize, data) {
    console.log('\n' + '='.repeat(80));
    console.log(`编码模拟 (${gridSize}x${gridSize} 网格)`);
    console.log('='.repeat(80));

    // 将数据转为位流
    const bitStream = [];
    for (let byte of data) {
        for (let i = 7; i >= 0; i--) {
            bitStream.push((byte >> i) & 1);
        }
    }

    console.log(`\n原始数据: ${data.length} bytes`);
    console.log(`位流长度: ${bitStream.length} bits`);

    // 模拟网格布局
    const grid = Array(gridSize).fill(0).map(() => Array(gridSize).fill(0));

    // 添加定位标记 (四个角)
    const cornerSize = 3;
    // 左上角
    for (let i = 0; i < cornerSize; i++) {
        for (let j = 0; j < cornerSize; j++) {
            grid[i][j] = (i === 1 && j === 1) ? 0 : 1;
        }
    }
    // 右上角
    for (let i = 0; i < cornerSize; i++) {
        for (let j = gridSize - cornerSize; j < gridSize; j++) {
            grid[i][j] = (i === 1 && j === gridSize - 2) ? 0 : 1;
        }
    }
    // 左下角
    for (let i = gridSize - cornerSize; i < gridSize; i++) {
        for (let j = 0; j < cornerSize; j++) {
            grid[i][j] = (i === gridSize - 2 && j === 1) ? 0 : 1;
        }
    }

    // 填充数据
    let bitIndex = 0;
    for (let i = 0; i < gridSize && bitIndex < bitStream.length; i++) {
        for (let j = 0; j < gridSize && bitIndex < bitStream.length; j++) {
            // 跳过定位标记区域
            if ((i < cornerSize && j < cornerSize) ||
                (i < cornerSize && j >= gridSize - cornerSize) ||
                (i >= gridSize - cornerSize && j < cornerSize)) {
                continue;
            }
            grid[i][j] = bitStream[bitIndex++];
        }
    }

    console.log(`填充位数: ${bitIndex} bits`);
    console.log(`利用率: ${(bitIndex / (gridSize * gridSize) * 100).toFixed(1)}%`);

    // 可视化前10x10区域
    console.log(`\n网格可视化 (左上角 10x10):`);
    console.log('─'.repeat(22));
    for (let i = 0; i < Math.min(10, gridSize); i++) {
        let row = '';
        for (let j = 0; j < Math.min(10, gridSize); j++) {
            row += grid[i][j] ? '██' : '  ';
        }
        console.log(row);
    }
    console.log('─'.repeat(22));

    return grid;
}

// ============================================================================
// 5. 主程序
// ============================================================================

function main() {
    console.log('\n');
    console.log('╔═══════════════════════════════════════════════════════════════════════════╗');
    console.log('║         黑白网格编码容量评估 - DataToColor 108字段验证                    ║');
    console.log('╚═══════════════════════════════════════════════════════════════════════════╝');

    // 1. 计算数据需求
    const { totalRequiredBytes, totalRequiredBits } = calculateDataRequirements();

    // 2. 计算可行的网格尺寸
    const gridSizes = calculateGridSizes(totalRequiredBits);

    if (gridSizes.length === 0) {
        console.log('\n❌ 错误: 没有找到可行的网格尺寸!');
        return;
    }

    // 3. 推荐方案
    console.log('\n' + '='.repeat(80));
    console.log('推荐方案');
    console.log('='.repeat(80));

    const recommended = gridSizes[0];
    console.log(`\n✅ 最小可行网格: ${recommended.size}x${recommended.size}`);
    console.log(`   - 容量: ${recommended.capacityBytes} bytes (${recommended.capacityBits} bits)`);
    console.log(`   - 需求: ${totalRequiredBytes} bytes (${totalRequiredBits} bits)`);
    console.log(`   - 富余: ${recommended.overhead}%`);
    console.log(`   - 像素尺寸: ${recommended.pixelSize}x${recommended.pixelSize} = ${recommended.pixelSize * recommended.pixelSize} 像素`);

    // 推荐一个更合适的尺寸（留有余量）
    const ideal = gridSizes.find(g => parseFloat(g.overhead) >= 20) || recommended;
    if (ideal !== recommended) {
        console.log(`\n💡 推荐网格: ${ideal.size}x${ideal.size} (留有${ideal.overhead}%余量)`);
        console.log(`   - 容量: ${ideal.capacityBytes} bytes`);
        console.log(`   - 像素尺寸: ${ideal.pixelSize}x${ideal.pixelSize}`);
    }

    // 4. 编码模拟
    const testData = new Uint8Array(totalRequiredBytes);
    for (let i = 0; i < testData.length; i++) {
        testData[i] = i % 256;
    }
    simulateEncoding(ideal.size, testData);

    // 5. 总结
    console.log('\n' + '='.repeat(80));
    console.log('结论');
    console.log('='.repeat(80));
    console.log(`\n✅ 黑白网格编码完全可行！`);
    console.log(`   - DataToColor的108个字段需要 ${totalRequiredBytes} bytes`);
    console.log(`   - 使用 ${ideal.size}x${ideal.size} 网格可以容纳所有数据`);
    console.log(`   - 包含15%纠错能力`);
    console.log(`   - 在WoW界面上显示为 ${ideal.pixelSize}x${ideal.pixelSize} 像素的黑白方块`);
    console.log(`   - 相比DataToColor的108个彩色像素，黑白网格更抗干扰`);

    console.log(`\n📐 实现建议:`);
    console.log(`   1. 使用 ${ideal.size}x${ideal.size} 网格`);
    console.log(`   2. 每个方块 2-3 像素（总界面大小 ${ideal.size * 2}x${ideal.size * 2} 到 ${ideal.size * 3}x${ideal.size * 3} 像素）`);
    console.log(`   3. 黑色方块: RGB(0,0,0)，白色方块: RGB(255,255,255)`);
    console.log(`   4. 包含四个角的定位标记（3x3黑白图案）`);
    console.log(`   5. C#端使用简单的二值化+网格扫描即可解码`);
    console.log(`   6. 不依赖颜色精确度，只需区分黑白即可`);

    console.log('\n');
}

// 运行
main();
