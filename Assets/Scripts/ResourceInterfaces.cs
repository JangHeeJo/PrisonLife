using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public interface IResourceReceiver
{
    bool CanReceive(ResourceType type, int amount);
    bool TryReceive(ResourceType type, int amount);
}

public interface IResourceProvider
{
    bool CanProvide(ResourceType type, int amount);
    bool TryProvide(ResourceType type, int amount);
}