using System.Collections.Generic;

/// <summary>
/// GameSaveData 안의 IAP 권한 상태를 조회/변경하는 유틸리티입니다.
/// 실제 구매 처리 코드와 게임 효과 적용 코드를 분리하기 위해 사용합니다.
/// </summary>
public static class IapEntitlementState
{
    public static bool IsActive(GameSaveData data, string productId)
    {
        if (data == null || string.IsNullOrEmpty(productId))
            return false;

        if (data.iapEntitlements == null)
            return false;

        for (int i = 0; i < data.iapEntitlements.Count; i++)
        {
            IapEntitlementSaveData entitlement = data.iapEntitlements[i];

            if (entitlement == null)
                continue;

            if (entitlement.productId == productId)
                return entitlement.active;
        }

        return false;
    }

    public static void SetActive(GameSaveData data, string productId, bool active)
    {
        if (data == null || string.IsNullOrEmpty(productId))
            return;

        if (data.iapEntitlements == null)
            data.iapEntitlements = new List<IapEntitlementSaveData>();

        IapEntitlementSaveData target = null;

        for (int i = 0; i < data.iapEntitlements.Count; i++)
        {
            IapEntitlementSaveData entitlement = data.iapEntitlements[i];

            if (entitlement == null)
                continue;

            if (entitlement.productId == productId)
            {
                target = entitlement;
                break;
            }
        }

        if (target == null)
        {
            target = new IapEntitlementSaveData
            {
                productId = productId,
                active = false
            };

            data.iapEntitlements.Add(target);
        }

        target.active = active;
    }
}
