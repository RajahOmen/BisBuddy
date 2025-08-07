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
    public GameIcon UnobtainedIcon { get; set; } = GameIcon.RedCrossSquare;

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
}
