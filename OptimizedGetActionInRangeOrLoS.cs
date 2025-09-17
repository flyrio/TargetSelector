using System;
using System.Runtime.InteropServices;
using System.Buffers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Numerics;

namespace SamplePlugin.Utils;
public unsafe class OptimizedGetActionInRangeOrLoS
{
    private const int ActorRotationOffset = 0xB0;
    private const int ActorDataSize = 512;

    /// <summary>
    /// (优化版) 使用两段式检查来高效地获取技能可用性，同时绕过角度检测。
    /// </summary>
    /// <param name="actionId">要检查的技能ID。</param>
    /// <param name="source">来源角色 (通常是玩家)。</param>
    /// <param name="target">目标角色。</param>
    /// <returns>返回真实的结果码，同时将565(角度错误)替换为它背后被掩盖的真正状态码。</returns>
    public static long GetActionInRangeOrLoS_Optimized(uint actionId, GameObject* source, GameObject* target)
    {
        if (source == null || target == null)
        {
            return -1; // 对象无效
        }

        long initialResult = ActionManager.GetActionInRangeOrLoS(
            actionId,
            source,
            target
        );

        if (initialResult != 565)
        {
            // 技能可用(0)，或因距离(566)/视线(562)等非角度问题失败。
            // 这是最常见的路径，性能开销极低。
            return initialResult;
        }

        IntPtr pFakeActor = Marshal.AllocHGlobal(ActorDataSize);
        try
        {
            // 克隆数据
            Buffer.MemoryCopy(
                source: (void*)source,
                destination: (void*)pFakeActor,
                destinationSizeInBytes: ActorDataSize,
                sourceBytesToCopy: ActorDataSize
            );

            // 修改克隆体中的角度
            Vector3 sourcePosition = source->Position;
            Vector3 targetPosition = target->Position;

            float dirX = targetPosition.X - sourcePosition.X;
            float dirZ = targetPosition.Z - sourcePosition.Z;

            if (Math.Abs(dirX) > 0.01f || Math.Abs(dirZ) > 0.01f)
            {
                float idealRotation = (float)Math.Atan2(dirX, dirZ);
                *(float*)(pFakeActor + ActorRotationOffset) = idealRotation;
            }

            // 使用伪造的克隆体指针进行第二次调用
            long finalResult = ActionManager.GetActionInRangeOrLoS(
                actionId,
                (GameObject*)pFakeActor,
                (GameObject*)target
            );

            return finalResult;
        }
        catch (Exception ex)
        {
            return -1; // 返回错误码
        }
        finally
        {
            Marshal.FreeHGlobal(pFakeActor);
        }
    }


}
public unsafe class LineOfSightChecker
{
    /// <summary>
    /// 使用底层的 BGCollisionModule 直接检查两个角色之间是否存在视线遮挡。
    /// </summary>
    /// <param name="source">来源角色。</param>
    /// <param name="target">目标角色。</param>
    /// <param name="hitPoint">如果被阻挡，返回碰撞点的坐标。</param>
    /// <returns>如果视线被阻挡，返回 true；否则返回 false。</returns>
    public static unsafe bool IsBlocked(GameObject* source, GameObject* target)
    {
            // 为了更真实的判断，通常会给起点和终点一个运算符“+”对于“Vector3”和“Vector3”类型的操作数具有二义性Y轴上的偏移
            // 例如，从角色胸口位置发射，而不是脚底

        var sourcePos = *source->GetPosition();
        var targetPos = *target->GetPosition();

        sourcePos.Y += 2;
        targetPos.Y += 2;


        var offset = targetPos - sourcePos;
        var maxDist = offset.Magnitude;
        var direction = offset / maxDist;
        // 调用底层的射线投射函数
        // 注意：这个函数期望的距离是一个 float，而不是 byte，更精确
        bool hasHit = BGCollisionModule.RaycastMaterialFilter(sourcePos, direction, out _, maxDist);

        if (hasHit)
        {
            return true; // 有碰撞，视线被阻挡
        }

        return false; // 没有碰撞，视线清晰
    }
}

