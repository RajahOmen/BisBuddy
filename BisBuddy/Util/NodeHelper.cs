using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;

namespace BisBuddy.Util;

//Code 'borrowed'+modified from daily duties (found in TalkCopy)
public unsafe class BaseNode
{
    private readonly AtkUnitBase* node;

    public BaseNode(AtkUnitBase* node)
    {
        this.node = node;
    }

    public AtkResNode* GetRootNode()
    {
        if (node == null) return null;

        return node->RootNode;
    }

    public T* GetNode<T>(uint id) where T : unmanaged
    {
        if (node == null) return null;

        var targetNode = (T*)node->GetNodeById(id);

        return targetNode;
    }

    public ComponentNode GetComponentNode(uint id)
    {
        if (node == null) return new ComponentNode(null);

        var targetNode = (AtkComponentNode*)node->GetNodeById(id);

        return new ComponentNode(targetNode);
    }

    public T* GetNestedNode<T>(params uint[] idList) where T : unmanaged
    {
        if (idList.Length == 0) return null;
        if (idList.Length == 1) return GetNode<T>(idList[0]);

        var startingComponentNode = GetComponentNode(idList[0]);
        var resultNode = startingComponentNode.GetNestedNode<T>(idList[1..]);

        return resultNode;
    }


    public ComponentNode GetNestedComponentNode(params uint[] idList)
    {
        uint index = 0;

        ComponentNode startingNode = new ComponentNode(null);

        do
        {
            startingNode = GetComponentNode(idList[index]);
        } while (++index < idList.Length);

        return startingNode;
    }
}

public unsafe class ComponentNode
{
    private readonly AtkComponentNode* node;
    private readonly AtkComponentBase* componentBase;

    public ComponentNode(AtkComponentNode* node)
    {
        this.node = node;
        componentBase = node == null ? null : node->Component;
    }

    public ComponentNode GetComponentNode(uint id)
    {
        if (componentBase == null) return new ComponentNode(null);

        var targetNode = Node.GetNodeByID<AtkComponentNode>(componentBase->UldManager, id);

        return new ComponentNode(targetNode);
    }

    public T* GetNode<T>(uint id) where T : unmanaged
    {
        if (componentBase == null) return null;
        return Node.GetNodeByID<T>(componentBase->UldManager, id);
    }

    public List<ComponentNode> GetComponentNodes()
    {
        var componentNodes = new List<ComponentNode>();
        if (componentBase == null) return componentNodes;

        for (var i = 0; i < componentBase->UldManager.NodeListCount; i++)
        {
            var currentNode = componentBase->UldManager.NodeList[i]->GetAsAtkComponentNode();
            if (currentNode == null) continue; // Skip if it's not a component node
            componentNodes.Add(new ComponentNode(currentNode));
        }
        return componentNodes;
    }
    public T* GetNestedNode<T>(params uint[] idList) where T : unmanaged
    {
        if (idList.Length == 0) return null;
        if (idList.Length == 1) return GetNode<T>(idList[0]);

        var componentNode = GetComponentNode(idList[0]);
        return componentNode.GetNestedNode<T>(idList[1..]);
    }

    public AtkComponentNode* GetPointer()
    {
        return node;
    }

    public int GetChildCount()
    {
        return componentBase->UldManager.NodeListCount;
    }
}

public static unsafe class Node
{
    public static T* GetNodeByID<T>(AtkUldManager uldManager, uint nodeId) where T : unmanaged
    {
        for (var i = 0; i < uldManager.NodeListCount; i++)
        {
            var currentNode = uldManager.NodeList[i];

            if (currentNode->NodeId != nodeId) continue;

            return (T*)currentNode;
        }

        return null;
    }
}
