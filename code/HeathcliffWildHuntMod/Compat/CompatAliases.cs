// ===== 双版本兼容：全局类型 / 命令别名 =====
// 正式版（OFFICIAL）相对测试版（Beta）改了若干 API 形态。为了让 190+ 业务文件「一行不改」地
// 同时编译两套，这里用 C# 全局 using 别名把不兼容的符号统一重定向到 Compat 下的 shim：
//
//   1) PowerCmd            → PowerCmdShim：吸收 Apply/ModifyAmount「去掉首参 PlayerChoiceContext」的变化
//                            （94 处调用点保持旧写法 PowerCmd.Apply(ctx, ...) 不动）。
//   2) ICombatStateCompat  → 测试版的接口 ICombatState / 正式版的具体类 CombatState
//                            （正式版删了该接口；两者都暴露 .Creatures，业务只用到它）。
//
// 业务侧调用点全部用「无命名空间限定」的 PowerCmd / ICombatStateCompat，故别名优先生效；
// shim 内部如需触达「真正的」游戏 PowerCmd，则一律写全限定名 MegaCrit.Sts2.Core.Commands.PowerCmd。

global using PowerCmd = HeathcliffWildHuntMod.Compat.PowerCmdShim;

#if OFFICIAL
// 正式版：ICombatState 接口已删除，_state 字段实际类型即 CombatState
global using ICombatStateCompat = MegaCrit.Sts2.Core.Combat.CombatState;
#else
// 测试版：沿用原接口
global using ICombatStateCompat = MegaCrit.Sts2.Core.Combat.ICombatState;
#endif
