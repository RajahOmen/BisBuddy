using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Extensions;
using KamiToolKit.NodeParts;
using KamiToolKit.Nodes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Util;

// 'borrowed'+modified from simpletweaks
// https://github.com/Caraxi/SimpleTweaksPlugin/blob/257ca7cf4105784abf0af720654dac4345f4a619/Utility/UiHelper.Nodes.cs

public static unsafe partial class UiHelper
{
    private static readonly string NineGridTexturePath = "ui/uld/ListItemA_hr1.tex";

    public static unsafe void SetNodeColor(AtkResNode* node, Vector4 color)
    {
        node->AddRed = (short)Math.Round(255 * color.X * color.W);
        node->AddGreen = (short)Math.Round(255 * color.Y * color.W);
        node->AddBlue = (short)Math.Round(255 * color.Z * color.W);
    }


    public record PartInfo(ushort U, ushort V, ushort Width, ushort Height);

    /// <summary>
    /// Makes an image node with allocated and initialized components:<br/>
    /// 1x AtkUldPartsList<br/>
    /// 1x AtkUldPart<br/>
    /// 1x AtkUldAsset<br/>
    /// </summary>
    /// <param name="id">Id of the new node</param>
    /// <param name="partInfo">Texture U,V coordinates and Texture Width,Height</param>
    /// <remarks>Returns null if allocation of any component failed</remarks>
    /// <returns>Fully Allocated AtkImageNode</returns>
    public static AtkImageNode* MakeImageNode(
        uint id,
        PartInfo partInfo
        )
    {
        if (!TryMakeImageNode(id, 0, 0, 0, 0, out var imageNode))
        {
            //logger.Error("Failed to alloc memory for AtkImageNode.");
            return null;
        }

        if (!TryMakePartsList(0, out var partsList))
        {
            //logger.Error("Failed to alloc memory for AtkUldPartsList.");
            FreeImageNode(imageNode);
            return null;
        }

        if (!TryMakePart(partInfo.U, partInfo.V, partInfo.Width, partInfo.Height, out var part))
        {
            //logger.Error("Failed to alloc memory for AtkUldPart.");
            FreePartsList(partsList);
            FreeImageNode(imageNode);
            return null;
        }

        if (!TryMakeAsset(0, out var asset))
        {
            //logger.Error("Failed to alloc memory for AtkUldAsset.");
            FreePart(part);
            FreePartsList(partsList);
            FreeImageNode(imageNode);
        }

        AddAsset(part, asset);
        AddPart(partsList, part);
        AddPartsList(imageNode, partsList);

        return imageNode;
    }

    /// <summary>
    /// Checks if the addon has a valid root and child node.<br/>
    /// Useful for ensuring that an addon is fully loaded before adding new UI nodes to it.
    /// </summary>
    /// <param name="addon">Pointer to addon to check</param>
    public static bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon is null) return false;
        if (addon->RootNode is null) return false;
        if (addon->RootNode->ChildNode is null) return false;

        return true;
    }

    public static AtkTextNode* MakeTextNode(uint id)
    {
        if (!TryMakeTextNode(id, out var textNode)) return null;

        return textNode;
    }
    public static unsafe NineGridNode? CloneHighlightNineGridNode(
        AtkNineGridNode* clonedNode,
        Vector3 addColor,
        float alpha
        )
    {
        NineGridNode? newNode = null;

        try
        {
            // initialize new custom ninegridnode to show the highlight
            newNode = new NineGridNode()
            {
                NodeFlags = NodeFlags.Enabled | NodeFlags.Visible | NodeFlags.AnchorRight | NodeFlags.AnchorLeft,
                Width = clonedNode->Width,
                Height = clonedNode->Height,
                Scale = new(clonedNode->ScaleX, clonedNode->ScaleY),
                X = clonedNode->X,
                Y = clonedNode->Y,
                ScreenX = clonedNode->ScreenX,
                ScreenY = clonedNode->ScreenY,
                Rotation = clonedNode->Rotation,
                Offsets = new(clonedNode->TopOffset, clonedNode->BottomOffset, clonedNode->LeftOffset, clonedNode->RightOffset),
                BlendMode = clonedNode->BlendMode,
                PartsRenderType = clonedNode->PartsTypeRenderType,
                Color = clonedNode->Color.ToVector4(),
                AddColor = addColor,
                MultiplyColor = Constants.CustomNodeMultiplyColor,
                Alpha = alpha,
            };

            // construct and add texture part to the node
            var hoverNodePart = clonedNode->PartsList->Parts[0];
            var part = new Part()
            {
                U = hoverNodePart.U,
                V = hoverNodePart.V,
                Width = hoverNodePart.Width,
                Height = hoverNodePart.Height,
                Id = 0,
            };

            part.LoadTexture(NineGridTexturePath.ToString());
            newNode.AddPart(part);

            return newNode;
        }
        catch (Exception)
        {
            newNode?.Dispose();
            return null;
        }
    }

    public static void LinkNodeAtEnd(AtkResNode* imageNode, AtkUnitBase* parent)
    {
        var node = parent->RootNode->ChildNode;
        while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

        node->PrevSiblingNode = imageNode;
        imageNode->NextSiblingNode = node;
        imageNode->ParentNode = node->ParentNode;

        parent->UldManager.UpdateDrawNodeList();
    }

    public static void LinkNodeAtEnd<T>(T* atkNode, AtkResNode* parentNode, AtkUnitBase* addon) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        var endNode = parentNode->ChildNode;
        if (endNode == null)
        {
            // Adding to empty res node

            parentNode->ChildNode = node;
            node->ParentNode = parentNode;
            node->PrevSiblingNode = null;
            node->NextSiblingNode = null;
        }
        else
        {
            while (endNode->PrevSiblingNode != null)
            {
                endNode = endNode->PrevSiblingNode;
            }
            node->ParentNode = parentNode;
            node->NextSiblingNode = endNode;
            node->PrevSiblingNode = null;
            endNode->PrevSiblingNode = node;
        }

        addon->UldManager.UpdateDrawNodeList();
    }

    public static void LinkNodeAfterTargetNode(AtkResNode* node, AtkComponentNode* parent, AtkResNode* targetNode)
    {
        var prev = targetNode->PrevSiblingNode;
        node->ParentNode = targetNode->ParentNode;

        targetNode->PrevSiblingNode = node;
        prev->NextSiblingNode = node;

        node->PrevSiblingNode = prev;
        node->NextSiblingNode = targetNode;

        parent->Component->UldManager.UpdateDrawNodeList();

    }

    public static void LinkNodeAfterTargetNode<T>(T* atkNode, AtkUnitBase* parent, AtkResNode* targetNode) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        var prev = targetNode->PrevSiblingNode;
        node->ParentNode = targetNode->ParentNode;

        targetNode->PrevSiblingNode = node;
        prev->NextSiblingNode = node;

        node->PrevSiblingNode = prev;
        node->NextSiblingNode = targetNode;

        parent->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkNode<T>(T* atkNode, AtkComponentNode* componentNode) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        if (node == null) return;

        if (node->ParentNode->ChildNode == node)
        {
            node->ParentNode->ChildNode = node->NextSiblingNode;
        }

        if (node->NextSiblingNode != null && node->NextSiblingNode->PrevSiblingNode == node)
        {
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        }

        if (node->PrevSiblingNode != null && node->PrevSiblingNode->NextSiblingNode == node)
        {
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        }

        componentNode->Component->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkNode<T>(T* atkNode, AtkUnitBase* unitBase) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        if (node == null) return;
        if (node->ParentNode->ChildNode == node)
        {
            node->ParentNode->ChildNode = node->NextSiblingNode;
        }

        if (node->NextSiblingNode != null && node->NextSiblingNode->PrevSiblingNode == node)
        {
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        }

        if (node->PrevSiblingNode != null && node->PrevSiblingNode->NextSiblingNode == node)
        {
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        }

        unitBase->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkNode<T>(T* atkNode, AtkUldManager* uldManager) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        if (node == null) return;

        if (node->ParentNode->ChildNode == node)
        {
            node->ParentNode->ChildNode = node->NextSiblingNode;
        }

        if (node->NextSiblingNode != null && node->NextSiblingNode->PrevSiblingNode == node)
        {
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        }

        if (node->PrevSiblingNode != null && node->PrevSiblingNode->NextSiblingNode == node)
        {
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        }

        uldManager->UpdateDrawNodeList();
    }



    public static void UnlinkAndFreeImageNode(AtkImageNode* node, AtkUnitBase* parent)
    {
        if (node->AtkResNode.PrevSiblingNode is not null)
            node->AtkResNode.PrevSiblingNode->NextSiblingNode = node->AtkResNode.NextSiblingNode;

        if (node->AtkResNode.NextSiblingNode is not null)
            node->AtkResNode.NextSiblingNode->PrevSiblingNode = node->AtkResNode.PrevSiblingNode;

        parent->UldManager.UpdateDrawNodeList();

        FreePartsList(node->PartsList);
        FreeImageNode(node);
    }

    public static void UnlinkAndFreeTextNode(AtkTextNode* node, AtkUnitBase* parent)
    {
        if (node->AtkResNode.PrevSiblingNode is not null)
            node->AtkResNode.PrevSiblingNode->NextSiblingNode = node->AtkResNode.NextSiblingNode;

        if (node->AtkResNode.NextSiblingNode is not null)
            node->AtkResNode.NextSiblingNode->PrevSiblingNode = node->AtkResNode.PrevSiblingNode;

        parent->UldManager.UpdateDrawNodeList();
        FreeTextNode(node);
    }

    #region TryMakeComponents

    public static bool TryMakeTextNode(uint id, [NotNullWhen(true)] out AtkTextNode* textNode)
    {
        textNode = IMemorySpace.GetUISpace()->Create<AtkTextNode>();

        if (textNode is not null)
        {
            textNode->AtkResNode.Type = NodeType.Text;
            textNode->AtkResNode.NodeId = id;
            return true;
        }

        return false;
    }

    public static bool TryMakeImageNode(uint id, NodeFlags resNodeFlags, uint resNodeDrawFlags, byte wrapMode, byte imageNodeFlags, [NotNullWhen(true)] out AtkImageNode* imageNode)
    {
        imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();

        if (imageNode is not null)
        {
            imageNode->AtkResNode.Type = NodeType.Image;
            imageNode->AtkResNode.NodeId = id;
            imageNode->AtkResNode.NodeFlags = resNodeFlags;
            imageNode->AtkResNode.DrawFlags = resNodeDrawFlags;
            imageNode->WrapMode = wrapMode;
            imageNode->Flags = imageNodeFlags;
            return true;
        }

        return false;
    }

    public static bool TryMakeNineGridNode(uint id, [NotNullWhen(true)] out AtkNineGridNode* imageNode)
    {
        imageNode = IMemorySpace.GetUISpace()->Create<AtkNineGridNode>();

        if (imageNode is not null)
        {
            imageNode->AtkResNode.Type = NodeType.NineGrid;
            imageNode->AtkResNode.NodeId = id;
            return true;
        }

        return false;
    }

    public static bool TryMakePartsList(uint id, [NotNullWhen(true)] out AtkUldPartsList* partsList)
    {
        partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);

        if (partsList is not null)
        {
            partsList->Id = id;
            partsList->PartCount = 0;
            partsList->Parts = null;
            return true;
        }

        return false;
    }

    public static bool TryMakePart(ushort u, ushort v, ushort width, ushort height, [NotNullWhen(true)] out AtkUldPart* part)
    {
        part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);

        if (part is not null)
        {
            part->U = u;
            part->V = v;
            part->Width = width;
            part->Height = height;
            return true;
        }

        return false;
    }

    public static bool TryMakeAsset(uint id, [NotNullWhen(true)] out AtkUldAsset* asset)
    {
        asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);

        if (asset is not null)
        {
            asset->Id = id;
            asset->AtkTexture.Ctor();
            return true;
        }

        return false;
    }

    #endregion

    #region AddComponents

    public static void AddPartsList(AtkImageNode* imageNode, AtkUldPartsList* partsList)
    {
        imageNode->PartsList = partsList;
    }

    public static void AddPartsList(AtkCounterNode* counterNode, AtkUldPartsList* partsList)
    {
        counterNode->PartsList = partsList;
    }

    public static void AddPart(AtkUldPartsList* partsList, AtkUldPart* part)
    {
        // copy pointer to old array
        var oldPartArray = partsList->Parts;

        // allocate space for new array
        var newSize = partsList->PartCount + 1;
        var newArray = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart) * newSize, 8);

        if (oldPartArray is not null)
        {
            // copy each member of old array2
            foreach (var index in Enumerable.Range(0, (int)partsList->PartCount))
            {
                Buffer.MemoryCopy(oldPartArray + index, newArray + index, sizeof(AtkUldPart), sizeof(AtkUldPart));
            }

            // free old array
            IMemorySpace.Free(oldPartArray, (ulong)sizeof(AtkUldPart) * partsList->PartCount);
        }

        // add new part
        Buffer.MemoryCopy(part, newArray + (newSize - 1), sizeof(AtkUldPart), sizeof(AtkUldPart));
        partsList->Parts = newArray;
        partsList->PartCount = newSize;
    }

    public static void AddAsset(AtkUldPart* part, AtkUldAsset* asset)
    {
        part->UldAsset = asset;
    }

    #endregion

    #region FreeNodeComponents
    public static int nodeSize(NodeType type)
    {
        return type switch
        {
            NodeType.Text => sizeof(AtkTextNode),
            NodeType.Res => sizeof(AtkResNode),
            NodeType.ClippingMask => sizeof(AtkClippingMaskNode),
            NodeType.NineGrid => sizeof(AtkNineGridNode),
            NodeType.Collision => sizeof(AtkCollisionNode),
            NodeType.Counter => sizeof(AtkCounterNode),
            NodeType.Image => sizeof(AtkImageNode),
            _ => 0,
        };
    }

    public static void FreeImageNode(AtkImageNode* node)
    {
        node->AtkResNode.Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkImageNode));
    }

    public static void FreeTextNode(AtkTextNode* node)
    {
        node->AtkResNode.Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkTextNode));
    }

    public static void FreeNineGridNode(AtkNineGridNode* node)
    {
        node->AtkResNode.Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkNineGridNode));
    }

    public static void FreeNode(AtkResNode* node)
    {
        if (node == null) return;

        node->Destroy(false);
        IMemorySpace.Free(node, (ulong)nodeSize(node->Type));
    }

    public static void FreePartsList(AtkUldPartsList* partsList)
    {
        foreach (var index in Enumerable.Range(0, (int)partsList->PartCount))
        {
            var part = &partsList->Parts[index];

            FreeAsset(part->UldAsset);
            FreePart(part);
        }

        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
    }

    public static void FreePart(AtkUldPart* part)
    {
        IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
    }

    public static void FreeAsset(AtkUldAsset* asset)
    {
        IMemorySpace.Free(asset, (ulong)sizeof(AtkUldAsset));
    }

    #endregion
}
