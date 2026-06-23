using System;
using UnityEngine;

public class ResourceStorage : MonoBehaviour
{
    [Serializable]
    private class ResourceSlot
    {
        public ResourceType type = default;
        public int capacity = 10;

        [HideInInspector] public int amount;

        public bool IsFull => amount >= capacity;
        public bool IsEmpty => amount <= 0;
    }

    [SerializeField] private ResourceSlot[] slots = Array.Empty<ResourceSlot>();

    public bool CanAdd(ResourceType type, int amount = 1)
    {
        ResourceSlot slot = GetSlot(type);
        return slot != null && slot.amount + amount <= slot.capacity;
    }

    public bool CanRemove(ResourceType type, int amount = 1)
    {
        ResourceSlot slot = GetSlot(type);
        return slot != null && slot.amount >= amount;
    }

    public bool TryAdd(ResourceType type, int amount = 1)
    {
        ResourceSlot slot = GetSlot(type);

        if (slot == null)
            return false;

        if (slot.amount + amount > slot.capacity)
            return false;

        slot.amount += amount;
        return true;
    }

    public bool TryRemove(ResourceType type, int amount = 1)
    {
        ResourceSlot slot = GetSlot(type);

        if (slot == null)
            return false;

        if (slot.amount < amount)
            return false;

        slot.amount -= amount;
        return true;
    }

    public int GetAmount(ResourceType type)
    {
        ResourceSlot slot = GetSlot(type);
        return slot != null ? slot.amount : 0;
    }

    public int GetCapacity(ResourceType type)
    {
        ResourceSlot slot = GetSlot(type);
        return slot != null ? slot.capacity : 0;
    }

    private ResourceSlot GetSlot(ResourceType type)
    {
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].type == type)
                return slots[i];
        }

        return null;
    }
}