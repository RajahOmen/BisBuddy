using BisBuddy.Gear;
using BisBuddy.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Numerics;

namespace BisBuddy.Services.Configuration;

/// <summary>
/// Determines how certain UI elements should be themed/displayed
/// </summary>
[Serializable]
public partial class UiTheme : ObservableObject
{
    [ObservableProperty]
    private Vector4 deleteColor = new(
        x: 0.6f, // Red
        y: 0.1f, // Green
        z: 0.1f, // Blue
        w: 1.0f  // Alpha
    );

    /// <summary>
    /// The color of text used to indicate that an item is obtained and all materia are melded.
    /// </summary>
    [ObservableProperty]
    private Vector4 obtainedCompleteTextColor = new(
        x: 0.2f, // Red
        y: 1.0f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtained and all materia are melded.
    /// </summary>
    [ObservableProperty]
    private GameIcon obtainedCompleteIcon = GameIcon.BlueCheck;

    /// <summary>
    /// The color of text used to indicate that an item is obtained but not all materia are melded.
    /// </summary>
    [ObservableProperty]
    private Vector4 obtainedPartialTextColor = new(
        x: 1.0f, // Red
        y: 1.0f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtained but not all materia are melded.
    /// </summary>
    [ObservableProperty]
    private GameIcon obtainedPartialIcon = GameIcon.YellowWarningTriangle;

    /// <summary>
    /// The color of text used to indicate that an item is obtainable, but not yet obtained.
    /// </summary>
    [ObservableProperty]
    private Vector4 obtainableTextColor = new(
        x: 0.2f, // Red
        y: 0.8f, // Green
        z: 1.0f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is obtainable, but not yet obtained.
    /// </summary>
    [ObservableProperty]
    private GameIcon obtainableIcon = GameIcon.BlueQuestionSquare;


    /// <summary>
    /// The icon used to indicate that an item is currently not obtained, but some progress has been made.
    /// </summary>
    [ObservableProperty]
    private Vector4 notObtainablePartialTextColor = new(
        x: 1.0f, // Red
        y: 0.7f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is currently not obtained, but some progress has been made.
    /// </summary>
    [ObservableProperty]
    private GameIcon notObtainablePartialIcon = GameIcon.YellowWarningTriangle;

    /// <summary>
    /// The color of text used to indicate that an item is currently not obtained.
    /// </summary>
    [ObservableProperty]
    private Vector4 unobtainedTextColor = new(
        x: 1.0f, // Red
        y: 0.2f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// The icon used to indicate that an item is currently not obtained.
    /// </summary>
    [ObservableProperty]
    private GameIcon unobtainedIcon = GameIcon.RedCross;

    /// <summary>
    /// The color that the edge of a materia slot is when it isn't an advanced/overmeld slot
    /// </summary>
    [ObservableProperty]
    private Vector4 materiaSlotNormalColor = new(
        x: 0.2f, // Red
        y: 0.8f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );


    /// <summary>
    /// The color that the edge of a materia slot is when it is an advanced/overmeld slot
    /// </summary>
    [ObservableProperty]
    private Vector4 materiaSlotAdvancedColor = new(
        x: 0.8f, // Red
        y: 0.2f, // Green
        z: 0.2f, // Blue
        w: 1.0f  // Alpha
        );

    /// <summary>
    /// If true, gearsets will show the color accent on the header when it is viewed.
    /// </summary>
    [ObservableProperty]
    private bool showGearsetColorAccentFlag = true;

    [ObservableProperty]
    private Vector4 buttonColor = new(
        x: 0.2f,
        y: 0.2f,
        z: 0.2f,
        w: 1.0f
        );

    [ObservableProperty]
    private Vector4 buttonHovered = new(
        x: 0.3f,
        y: 0.3f,
        z: 0.3f,
        w: 1.0f
        );

    [ObservableProperty]
    private Vector4 buttonActive = new(
        x: 0.4f,
        y: 0.4f,
        z: 0.4f,
        w: 1.0f
        );

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
            CollectionStatusType.NotObtainablePartial => (NotObtainablePartialTextColor, NotObtainablePartialIcon),
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
            case CollectionStatusType.NotObtainablePartial:
                NotObtainablePartialTextColor = textColor;
                NotObtainablePartialIcon = icon;
                break;
            case CollectionStatusType.NotObtainable:
                UnobtainedTextColor = textColor;
                UnobtainedIcon = icon;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}
