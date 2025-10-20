using BisBuddy.Gear;
using BisBuddy.Ui;
using Dalamud.Interface;
using System;
using System.Numerics;

namespace BisBuddy.Services.Configuration;

/// <summary>
/// Determines how certain UI elements should be themed/displayed
/// </summary>
[Serializable]
public class UiTheme
{
    public Vector4 DeleteColor { get; set; } = new Vector4(
        x: 0.6f, // Red
        y: 0.1f, // Green
        z: 0.1f, // Blue
        w: 1.0f  // Alpha
    );

    /// <summary>
    /// The color of text used to indicate that an item is obtained and all materia are melded.
    /// </summary>
    public Vector4 ObtainedCompleteTextColor { get; set; } = new(
        x: 0.2f, // Red
        y: 1.0f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtained and all materia are melded.
    /// </summary>
    public GameIcon ObtainedCompleteIcon { get; set; } = GameIcon.BlueCheck;

    /// <summary>
    /// The color of text used to indicate that an item is obtained but not all materia are melded.
    /// </summary>
    public Vector4 ObtainedPartialTextColor { get; set; } = new(
        x: 1.0f, // Red
        y: 1.0f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtained but not all materia are melded.
    /// </summary>
    public GameIcon ObtainedPartialIcon { get; set; } = GameIcon.YellowWarningTriangle;

    /// <summary>
    /// The color of text used to indicate that an item is obtainable, but not yet obtained.
    /// </summary>
    public Vector4 ObtainableTextColor { get; set; } = new(
        x: 0.2f, // Red
        y: 0.8f, // Green
        z: 1.0f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtainable, but not yet obtained.
    /// </summary>
    public GameIcon ObtainableIcon { get; set; } = GameIcon.BlueQuestionSquare;

    /// <summary>
    /// The color of text used to indicate that an item is currently not obtained.
    /// </summary>
    public Vector4 UnobtainedTextColor { get; set; } = new(
        x: 1.0f, // Red
        y: 0.2f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is currently not obtained.
    /// </summary>
    public GameIcon UnobtainedIcon { get; set; } = GameIcon.RedCross;

    /// <summary>
    /// The color that the edge of a materia slot is when it isn't an advanced/overmeld slot
    /// </summary>
    public Vector4 MateriaSlotNormalColor { get; set; } = new(
        x: 0.2f, // Red
        y: 0.8f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );


    /// <summary>
    /// The color that the edge of a materia slot is when it is an advanced/overmeld slot
    /// </summary>
    public Vector4 MateriaSlotAdvancedColor { get; set; } = new(
        x: 0.8f, // Red
        y: 0.2f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// If true, gearsets will show the color accent on the header when it is viewed.
    /// </summary>
    public bool ShowGearsetColorAccentFlag { get; set; } = true;

    /// <summary>
    /// Returns the text color and icon associated with a given collection status type for
    /// an <see cref="ICollectableItem"/>
    /// </summary>
    /// <param name="status">The status of the <see cref="ICollectableItem"/>
    /// to get the text color for</param>
    /// <returns>The color to render text and the game icon associated with that type</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the provided status does not have
    /// a color/icon associated with it</exception>
    public (Vector4 TextColor, GameIcon Icon) GetCollectionStatusTheme(CollectionStatusType status) =>
        status switch
        {
            CollectionStatusType.ObtainedComplete => (ObtainedCompleteTextColor, ObtainedCompleteIcon),
            CollectionStatusType.ObtainedPartial => (ObtainedPartialTextColor, ObtainedPartialIcon),
            CollectionStatusType.Obtainable => (ObtainableTextColor, ObtainableIcon),
            CollectionStatusType.NotObtainable => (UnobtainedTextColor, UnobtainedIcon),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    public void SetCollectionStatusTheme(CollectionStatusType status, Vector4 textColor, GameIcon icon)
    {
        switch (status)
        {
            case CollectionStatusType.ObtainedComplete:
                ObtainedCompleteTextColor = textColor;
                ObtainedCompleteIcon = icon;
                break;
            case CollectionStatusType.ObtainedPartial:
                ObtainedPartialTextColor = textColor;
                ObtainedPartialIcon = icon;
                break;
            case CollectionStatusType.Obtainable:
                ObtainableTextColor = textColor;
                ObtainableIcon = icon;
                break;
            case CollectionStatusType.NotObtainable:
                UnobtainedTextColor = textColor;
                UnobtainedIcon = icon;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    public Vector4 ButtonColor { get; set; } = new(
        x: 0.2f,
        y: 0.2f,
        z: 0.2f,
        w: 1.0f
        );

    public Vector4 ButtonHovered { get; set; } = new(
        x: 0.3f,
        y: 0.3f,
        z: 0.3f,
        w: 1.0f
        );

    public Vector4 ButtonActive { get; set; } = new(
        x: 0.4f,
        y: 0.4f,
        z: 0.4f,
        w: 1.0f
        );
}
